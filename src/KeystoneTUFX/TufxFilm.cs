using Keystone;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace KeystoneTUFX
{
    /// <summary>
    /// Keystone's film (see Keystone.Film) done by the TUFX mod: Unity's own post-processing has an exposure,
    /// a film curve, contrast and colour, a glow round what is bright, the darkening of creases, darker
    /// corners and colour fringes.
    ///
    /// Like the lens, it is a layer of settings of its own on top of whatever TUFX profile is in use, and
    /// touches only what it has been asked for: where a grade asks for no glow, the profile's own stays.
    /// Whether the picture is kept with more than white in it is the profile's to say ("hdr"), and TUFX's
    /// own profiles do; where one does not, there is nothing brighter than white to bring back, and only
    /// contrast, colour and a rough brightness are done.
    /// </summary>
    public sealed class TufxFilm : IFilm
    {
        public string Name => "TUFX";
        public bool Works => true;
        public string Why => "";

        PostProcessVolume volume;
        PostProcessProfile profile;
        ColorGrading grading;
        Bloom glow;
        AmbientOcclusion nooks;
        Vignette corners;
        ChromaticAberration fringe;
        Grain grain;
        bool tones;

        public bool Wide
        {
            get
            {
                Camera cam = Camera.main;
                return cam != null && cam.allowHDR;
            }
        }

        public void Use(Grade g)
        {
            if (volume == null) Begin();
            bool wide = Wide;
            // Two tones are done with a curve for each of red, green and blue, which Unity's grading only has when it is
            // not keeping more than white: so for as long as two tones are asked for, the picture is graded the plain way
            // (no film curve, and a rough brightness in place of the exposure). Nothing that asks for two tones minds.
            bool twoTone = g.poster > 0.005f;
            bool full = wide && !twoTone;
            grading.gradingMode.Override(full ? GradingMode.HighDefinitionRange : GradingMode.LowDefinitionRange);
            grading.tonemapper.Override(!full || g.curve <= 0 ? Tonemapper.None : g.curve == 1 ? Tonemapper.Neutral : Tonemapper.ACES);
            grading.postExposure.Override(full ? g.exposure : 0f);
            grading.brightness.Override(full ? 0f : Mathf.Clamp(25f * g.exposure, -80f, 80f));
            grading.contrast.Override(Mathf.Clamp(100f * g.contrast, -100f, 100f));
            grading.saturation.Override(Mathf.Clamp(100f * g.colour, -100f, 100f));
            grading.temperature.Override(Mathf.Clamp(60f * g.warmth, -100f, 100f));
            grading.lift.Override(new Vector4(g.dark.r, g.dark.g, g.dark.b, 0f));
            grading.gain.Override(new Vector4(g.bright.r, g.bright.g, g.bright.b, 0f));
            if (twoTone)
            {
                Color low = g.negative ? g.inkBright : g.inkDark, high = g.negative ? g.inkDark : g.inkBright;
                float at = Mathf.Clamp(g.posterAt, 0.05f, 0.95f), half = Mathf.Lerp(0.5f, 0.012f, Mathf.Clamp01(g.poster));
                grading.redCurve.Override(Step(low.r, high.r, at, half));
                grading.greenCurve.Override(Step(low.g, high.g, at, half));
                grading.blueCurve.Override(Step(low.b, high.b, at, half));
                tones = true;
            }
            else if (tones)
            {
                grading.redCurve.overrideState = grading.greenCurve.overrideState = grading.blueCurve.overrideState = false;
                tones = false;
            }

            glow.active = g.glow > 0.005f;
            if (glow.active)
            {
                glow.intensity.Override(4f * g.glow);
                glow.threshold.Override(Mathf.Max(0.1f, g.glowFrom));
                glow.softKnee.Override(0.3f);                                                 // (a wider knee than this puts a veil over everything that is merely light)
                glow.diffusion.Override(Mathf.Clamp(g.glowWide, 1f, 10f));
                glow.color.Override(g.glowTint);
            }

            nooks.active = g.nooks > 0.005f;
            if (nooks.active)
            {
                // (the better kind needs a compute shader, which the game's OpenGL on a Mac has not got)
                nooks.mode.Override(SystemInfo.supportsComputeShaders ? AmbientOcclusionMode.MultiScaleVolumetricObscurance : AmbientOcclusionMode.ScalableAmbientObscurance);
                nooks.intensity.Override(SystemInfo.supportsComputeShaders ? 1.1f * g.nooks : 2.6f * g.nooks);
                nooks.radius.Override(0.55f);
                nooks.quality.Override(AmbientOcclusionQuality.Low);
                nooks.thicknessModifier.Override(1.2f);
            }

            corners.active = g.corners > 0.005f;
            if (corners.active) { corners.intensity.Override(0.42f * g.corners); corners.smoothness.Override(0.42f); }
            fringe.active = g.fringe > 0.005f;
            if (fringe.active) fringe.intensity.Override(0.12f * g.fringe);
            grain.active = g.grain > 0.005f;
            if (grain.active) { grain.intensity.Override(0.5f * g.grain); grain.size.Override(0.8f); }
        }

        /// <summary>A curve that is flat at one value, steep where the tones part, and flat at the other.</summary>
        static Spline Step(float low, float high, float at, float half)
        {
            float from = Mathf.Max(0f, at - half), to = Mathf.Min(1f, at + half), slope = (high - low) / Mathf.Max(to - from, 1e-3f);
            var curve = new AnimationCurve();
            if (from > 0.001f) curve.AddKey(new Keyframe(0f, low, 0f, 0f));
            curve.AddKey(new Keyframe(from, low, 0f, slope));
            curve.AddKey(new Keyframe(to, high, slope, 0f));
            if (to < 0.999f) curve.AddKey(new Keyframe(1f, high, 0f, 0f));
            return new Spline(curve, low, false, new Vector2(0f, 1f));
        }

        void Begin()
        {
            // (one left behind by a copy of this that has since been replaced, in a development build, would go on being obeyed)
            GameObject left = GameObject.Find("Keystone film settings for TUFX");
            if (left != null) Object.Destroy(left);
            var holder = new GameObject("Keystone film settings for TUFX") { layer = 0 };      // (the layer TUFX has the game's cameras look for such settings on)
            Object.DontDestroyOnLoad(holder);
            volume = holder.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 40f;                                                        // over TUFX's own, under the lens's
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            grading = Add<ColorGrading>();
            glow = Add<Bloom>();
            nooks = Add<AmbientOcclusion>();
            corners = Add<Vignette>();
            fringe = Add<ChromaticAberration>();
            grain = Add<Grain>();
            grain.colored.Override(false);
            grain.lumContrib.Override(0.6f);
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
