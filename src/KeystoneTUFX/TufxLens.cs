using Keystone;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

#if !DEV
[assembly: KSPAssembly("KeystoneTUFX", 0, 1)]
[assembly: KSPAssemblyDependency("Keystone", 0, 2)]
[assembly: KSPAssemblyDependency("TUFX", 1, 1)]
#endif

namespace KeystoneTUFX
{
    /// <summary>
    /// Keystone's lens (see Keystone.Lens) done by the TUFX mod, where that is installed: TUFX brings
    /// Unity's own post-processing with it, which has depth of field, motion blur, grain, darker corners,
    /// colour fringes and lens distortion. This is used where Keystone's own shader cannot be (on Windows'
    /// Direct3D, until that shader is built for it). The game only loads this library when TUFX is there.
    ///
    /// It adds its own layer of settings on top of whatever TUFX profile is in use, and takes it away
    /// again when the lens is taken off.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class TufxAtStart : MonoBehaviour
    {
        void Awake()
        {
            if (!(Lens.Other is TufxLens)) Lens.Other = new TufxLens();
            Destroy(gameObject);
        }
    }

    public sealed class TufxLens : ILens
    {
        public string Name => "TUFX";
        public bool Works => true;

        PostProcessVolume volume;
        PostProcessProfile profile;
        DepthOfField depth;
        MotionBlur smear;
        Grain grain;
        Vignette corners;
        ChromaticAberration fringe;
        LensDistortion bend;

        public void Use(Camera camera, Look look)
        {
            if (volume == null) Begin();
            depth.focusDistance.Override(look.focus);
            depth.focalLength.Override(look.focal);
            // (the blur is worked out for film 24 mm high: on another sensor the same lens and f-number blur by 24 mm over its height as much)
            depth.aperture.Override(look.stop * look.sensorHigh / 24f);
            depth.kernelSize.Override(look.blur <= 0 ? KernelSize.Small : look.blur == 1 ? KernelSize.Medium : KernelSize.Large);

            float open = 360f * Mathf.Clamp01(look.open);
            smear.enabled.Override(open > 4f);
            smear.shutterAngle.Override(open);

            grain.enabled.Override(look.grain > 0.01f);
            grain.intensity.Override(0.45f * look.grain);
            grain.size.Override(1f + 0.6f * look.grain);

            corners.enabled.Override(look.corners > 0.01f);
            corners.intensity.Override(0.42f * look.corners);
            fringe.enabled.Override(look.fringe > 0.01f);
            fringe.intensity.Override(0.12f * look.fringe);
            bend.enabled.Override(look.bend > 0.01f);
            bend.intensity.Override(-22f * look.bend);
            bend.scale.Override(1f + 0.04f * look.bend);
        }

        void Begin()
        {
            var holder = new GameObject("Keystone lens settings for TUFX") { layer = 0 };      // (the layer TUFX has the game's cameras look for such settings on)
            volume = holder.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 50f;                                                        // over TUFX's own, which it leaves alone
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            depth = Add<DepthOfField>();
            smear = Add<MotionBlur>();
            smear.sampleCount.Override(12);
            grain = Add<Grain>();
            grain.colored.Override(false);
            grain.lumContrib.Override(0.8f);
            corners = Add<Vignette>();
            corners.smoothness.Override(0.42f);
            fringe = Add<ChromaticAberration>();
            bend = Add<LensDistortion>();
            volume.sharedProfile = profile;
        }

        T Add<T>() where T : PostProcessEffectSettings
        {
            T one = profile.AddSettings<T>();
            one.enabled.Override(true);
            return one;
        }

        public void Off()
        {
            if (volume != null) Object.Destroy(volume.gameObject);
            if (profile != null) Object.Destroy(profile);
            volume = null; profile = null;
        }
    }
}
