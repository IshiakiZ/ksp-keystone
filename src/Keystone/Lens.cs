using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Keystone
{
    /// <summary>
    /// What a lens, a shutter and a film are to do to the picture, as a camera's own numbers: a mod works
    /// these out from whatever its settings are and hands them over every frame (see Lens).
    /// </summary>
    public struct Look
    {
        /// <summary>The lens's focal length and the height of the film or sensor behind it, in millimetres.</summary>
        public float focal, sensorHigh;
        /// <summary>The f-number, and the distance in focus in metres: together with the lens they say how blurred what is nearer or further is.</summary>
        public float stop, focus;
        /// <summary>How wide the blur may get: 0 small, 1 medium, 2 large. Wider costs more of the graphics card's time.</summary>
        public int blur;
        /// <summary>The part of each frame the shutter is open for, 0 (nothing smears) to 1.</summary>
        public float open;
        /// <summary>How grainy the film is, 0 to 1.</summary>
        public float grain;
        /// <summary>The lens's own marks, each 0 (none) to 1: darker corners, colours parting towards the corners, and the bend of a wide lens.</summary>
        public float corners, fringe, bend;
    }

    /// <summary>Something that can give a camera's picture a Look.</summary>
    public interface ILens
    {
        /// <summary>What does it, for telling the player.</summary>
        string Name { get; }
        /// <summary>Whether it can, on this computer.</summary>
        bool Works { get; }
        /// <summary>Give the camera's picture this look from now on (called every frame while it is wanted).</summary>
        void Use(Camera camera, Look look);
        /// <summary>Leave the picture alone again.</summary>
        void Off();
    }

    /// <summary>
    /// The lens that every mod on this base can put in front of the game's camera: depth of field, the
    /// smear of what moves while the shutter is open, grain, darker corners, colour fringes and the bend
    /// of a wide lens, done by a shader of the base's own (tools/shaderpack/lens.glsl) once the game has
    /// drawn its picture. It needs no other mod.
    ///
    /// That shader ships as OpenGL text, which is what the game runs on on a Mac and on Linux. Where the
    /// game runs on something else (Direct3D, on Windows) and no bundle built for it is beside the base,
    /// Own does not work; a mod can then use Other, which a companion to this base fills in when the
    /// TUFX mod is installed (Unity's own post-processing does the same things there), or go without.
    /// </summary>
    public static class Lens
    {
        static readonly OwnLens own = new OwnLens();

        /// <summary>This base's own.</summary>
        public static ILens Own => own;

        /// <summary>Another way of doing the same, if some mod that is installed has offered one (see KeystoneTUFX).</summary>
        public static ILens Other;

        /// <summary>(For trying the other way where both work.)</summary>
        public static bool PreferOther;

        /// <summary>The one to use: this base's own where it works, otherwise the other if there is one that does, otherwise nothing.</summary>
        public static ILens Best
        {
            get
            {
                bool other = Other != null && Other.Works;
                if (other && PreferOther) return Other;
                return own.Works ? own : other ? Other : null;
            }
        }

        /// <summary>Why there is none, in words for the player (nothing if there is one).</summary>
        public static string WhyNot => Best != null ? null : own.Why + (Other == null ? "" : " And " + Other.Name + " cannot do it here either.");
    }

    sealed class OwnLens : ILens
    {
        public string Name => "Keystone";

        Shader shader;
        bool tried;
        public string Why = "";

        public bool Works
        {
            get
            {
                if (!tried) { tried = true; Load(); }
                return shader != null;
            }
        }

        void Load()
        {
            try
            {
                bool openGL = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore;
                // (built in the Unity editor for whatever else the game runs on here, if someone has put one beside the base: none ships yet)
                string built = Kit.Folder + "lens-" + (Application.platform == RuntimePlatform.WindowsPlayer ? "windows" : Application.platform == RuntimePlatform.LinuxPlayer ? "linux" : "mac") + ".bundle";
                string path = openGL ? Kit.Folder + "lens.bundle" : built;
                if (!File.Exists(path))
                {
                    Why = openGL ? "Keystone's lens shader (GameData/Keystone/PluginData/lens.bundle) is missing."
                        : "Keystone's lens shader is for OpenGL, and the game is running on " + SystemInfo.graphicsDeviceType + " here.";
                    Kit.Log("Keystone", "no lens of its own: " + Why);
                    return;
                }
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) { Why = "Keystone's lens shader did not load."; Kit.Log("Keystone", "no lens of its own: " + Why); return; }
                Shader[] found = bundle.LoadAllAssets<Shader>();
                bundle.Unload(false);
                foreach (Shader one in found) if (one != null && one.isSupported) shader = one;
                if (shader == null) { Why = "This graphics card cannot run Keystone's lens shader."; Kit.Log("Keystone", "no lens of its own: " + Why); return; }
                Kit.Log("Keystone", "has a lens of its own");
            }
            catch (Exception ex)
            {
                shader = null;
                Why = "Keystone's lens shader did not load (" + ex.Message + ").";
                Kit.Log("Keystone", "no lens of its own: " + Why);
            }
        }

        CommandBuffer commands;
        Camera onCamera;
        Material blurring, finishing;
        RenderTexture scene, blurred;
        Mesh sheet;
        DepthTextureMode asked;                                   // what this has asked the camera to keep that it was not keeping already

        static readonly float[] Widest = { 0.012f, 0.02f, 0.032f };        // the widest blur, as the half-width of its disc over the picture's height: small, medium, large
        static readonly float[] Looks = { 28f, 48f, 80f };                 // and how many places are looked at to make it

        public void Use(Camera camera, Look look)
        {
            if (!Works || camera == null) return;
            if (commands == null)
            {
                commands = new CommandBuffer { name = "Keystone lens" };
                blurring = new Material(shader);
                finishing = new Material(shader);
                foreach (Material one in new[] { blurring, finishing }) one.SetVector("_LensSheet", new Vector4(1f, 1f, 0.5f, 1f));
                blurring.SetVector("_LensStage", new Vector4(1f, 0f, 0f, 0f));
                finishing.SetVector("_LensStage", new Vector4(2f, 0f, 0f, 0f));
                sheet = new Mesh { name = "Keystone sheet" };
                sheet.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
                sheet.triangles = new[] { 0, 1, 2 };
            }
            int wide = camera.pixelWidth, high = camera.pixelHeight;
            if (wide < 16 || high < 16) return;
            // (kept in the camera's own range of brightness: other mods have it draw brighter than white)
            RenderTextureFormat kind = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            if (scene == null || scene.width != wide || scene.height != high || scene.format != kind)
            {
                Release();
                scene = new RenderTexture(wide, high, 0, kind) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Keystone lens: the picture" };
                blurred = new RenderTexture(wide / 2, high / 2, 0, kind) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Keystone lens: out of focus" };
                foreach (Material one in new[] { blurring, finishing }) { one.SetTexture("_LensScene", scene); one.SetTexture("_LensBlur", blurred); }
            }

            // ---- out of focus. A point at a distance d, with the lens focused at s, is drawn as a disc as wide as
            // (focal length squared) / (f-number x (s - focal length)) x |d - s| / d on the film; here in pixels, and its half.
            float focal = Mathf.Max(look.focal, 1f), film = Mathf.Max(look.sensorHigh, 1f), at = Mathf.Max(look.focus, 0.05f) * 1000f;
            float farthest = 0.5f * focal * focal / (Mathf.Max(look.stop, 0.5f) * Mathf.Max(at - focal, 1f)) * high / film;
            int quality = Mathf.Clamp(look.blur, 0, 2);
            float widest = Widest[quality] * high;
            // (nothing in the picture can be blurred by more than what is infinitely far off is, or what is right at the lens:
            // if that is under a pixel, as with a wide lens stopped down, there is nothing to do)
            bool depth = Mathf.Max(farthest, farthest * (at / 250f)) > 0.6f;
            bool moving = look.open > 0.02f;
            var size = new Vector4(1f / wide, 1f / high, wide, high);
            foreach (Material one in new[] { blurring, finishing })
            {
                one.SetVector("_LensSize", size);
                one.SetVector("_LensFocus", new Vector4(at * 0.001f, farthest, widest, widest * widest / (2f * Looks[quality])));
            }
            // ---- the shutter, the lens's own marks and the film
            finishing.SetVector("_LensMove", new Vector4(moving ? Mathf.Clamp01(look.open) : 0f, 10f, 0.06f, depth ? 1f : 0f));
            float bent = 0.28f * Mathf.Clamp01(look.bend);
            finishing.SetVector("_LensMarks", new Vector4(0.55f * Mathf.Clamp01(look.corners), 0.012f * Mathf.Clamp01(look.fringe), bent, 0.38f * Mathf.Clamp01(look.grain)));
            finishing.SetVector("_LensFrame", new Vector4(Time.frameCount % 977, wide / (float)high, Mathf.Max(1f, (1f + 0.8f * Mathf.Clamp01(look.grain)) * high / 1080f), 0f));

            // What the shader reads of the game's own: how far off each pixel is, and how far it has moved since the last frame.
            DepthTextureMode wanted = (depth ? DepthTextureMode.Depth : DepthTextureMode.None) | (moving ? DepthTextureMode.MotionVectors | DepthTextureMode.Depth : DepthTextureMode.None);
            if (camera != onCamera) Detach();
            DepthTextureMode missing = wanted & ~(camera.depthTextureMode & ~asked);
            if (missing != asked)
            {
                camera.depthTextureMode = (camera.depthTextureMode & ~asked) | missing;
                asked = missing;
            }

            commands.Clear();
            commands.Blit(BuiltinRenderTextureType.CameraTarget, scene);
            if (depth)
            {
                commands.SetRenderTarget(blurred);
                commands.DrawMesh(sheet, Matrix4x4.identity, blurring, 0, 0);
            }
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            commands.DrawMesh(sheet, Matrix4x4.identity, finishing, 0, 0);
            if (onCamera != camera)
            {
                camera.AddCommandBuffer(CameraEvent.AfterImageEffects, commands);
                onCamera = camera;
            }
        }

        void Detach()
        {
            if (onCamera != null)
            {
                onCamera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, commands);
                onCamera.depthTextureMode &= ~asked;
            }
            onCamera = null;
            asked = DepthTextureMode.None;
        }

        void Release()
        {
            if (scene != null) { scene.Release(); UnityEngine.Object.Destroy(scene); scene = null; }
            if (blurred != null) { blurred.Release(); UnityEngine.Object.Destroy(blurred); blurred = null; }
        }

        public void Off()
        {
            if (commands == null) return;
            Detach();
            Release();
        }
    }
}
