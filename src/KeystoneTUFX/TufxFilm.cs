using System.Collections.Generic;
using System.IO;
using Keystone;
using UnityEngine;
using UnityEngine.Rendering;
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

        // (the game's main camera, looked for once a second: Camera.main searches the scene by tag every time it is asked)
        Camera mainCam;
        float mainAt = -10f;

        public bool Wide
        {
            get
            {
                if (mainCam == null || Time.unscaledTime - mainAt > 1f) { mainCam = Camera.main; mainAt = Time.unscaledTime; }
                return mainCam != null && mainCam.allowHDR;
            }
        }

#if DEV
        /// <summary>(Development build: how often a grade was given and how often left as it was, since this was last asked.)</summary>
        public static string Counts() { string said = "given " + given_ + ", left as it was " + skipped_; given_ = skipped_ = 0; return said; }
        static int given_, skipped_;
#endif

        // (since Keystone 0.9.0: what was last set, to set it again only when something has changed)
        Grade given;
        bool givenWide, haveGiven;
        int givenAt;

        public void Use(Grade g)
        {
            if (volume == null) { Begin(); haveGiven = false; }
            bool wide = Wide;
            // (Setting every one of the post-processing's values cost 36-40 µs a frame, the most of Keystone's own work, and a
            // look held steady asks for the same every frame: so they are set again only when something has changed, or a
            // second has gone by. The ground's facing, for the creases' darkening, is kept up every frame: cameras come and go.)
            if (haveGiven && wide == givenWide && Time.frameCount - givenAt < 60 && Same(g, given))
            {
#if DEV
                skipped_++;
#endif
                GroundFacing.Keep(g.nooks > 0.005f && !SystemInfo.supportsComputeShaders);
                return;
            }
#if DEV
            given_++;
#endif
            given = g; givenWide = wide; givenAt = Time.frameCount; haveGiven = true;
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
            // (and where that is the kind that reads which way each surface faces, the ground's facing put in for it: see GroundFacing)
            GroundFacing.Keep(nooks.active && !SystemInfo.supportsComputeShaders);

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

        /// <summary>
        /// Whether two grades come to the same picture, to two thousandths (of a stop of exposure: a seventh of a percent of
        /// brightness, nothing anyone can see; an eye that follows the view asks for a little more or less every frame). Every
        /// field of Grade: one added there must be added here.
        /// </summary>
        static bool Same(in Grade a, in Grade b)
        {
            return a.byHand == b.byHand && a.curve == b.curve && a.negative == b.negative
                && Near(a.exposure, b.exposure) && Near(a.contrast, b.contrast) && Near(a.colour, b.colour) && Near(a.warmth, b.warmth)
                && Near(a.dark, b.dark) && Near(a.bright, b.bright) && Near(a.glow, b.glow) && Near(a.glowFrom, b.glowFrom) && Near(a.glowWide, b.glowWide)
                && Near(a.glowTint, b.glowTint) && Near(a.corners, b.corners) && Near(a.fringe, b.fringe) && Near(a.nooks, b.nooks) && Near(a.grain, b.grain)
                && Near(a.poster, b.poster) && Near(a.posterAt, b.posterAt) && Near(a.inkDark, b.inkDark) && Near(a.inkBright, b.inkBright);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 2e-3f;
        static bool Near(Color a, Color b) => Near(a.r, b.r) && Near(a.g, b.g) && Near(a.b, b.b) && Near(a.a, b.a);

        public void Off()
        {
            haveGiven = false;
            GroundFacing.Keep(false);
            if (volume != null) Object.Destroy(volume.gameObject);
            if (profile != null) Object.Destroy(profile);
            volume = null; profile = null;
        }
    }
}

namespace KeystoneTUFX
{
    /// <summary>
    /// The way the ground faces, put into the picture Unity's darkening of creases reads, where that darkening is the kind a
    /// graphics card without compute shaders gets (the game's OpenGL on a Mac). That kind takes which way each surface faces
    /// from a picture Unity draws by drawing everything again with a shader of its own, and the game's ground is not drawn
    /// into it: flat ground was taken to face the camera, stood up in front of itself, and was darkened out to some seventy
    /// metres, in a straight line across the picture (seen first in a film of a crash on the runway, 2026-10-17). So on each
    /// camera that keeps that picture, once it is made, the ground's facing is worked out from the depths and put in
    /// (tools/shaderpack/dnfill.glsl, in GameData/Keystone/PluginData/dnfill.bundle). The other kind, with compute shaders
    /// (Windows), reads depths alone and needs none of this.
    /// </summary>
    static class GroundFacing
    {
        sealed class On { public CommandBuffer Commands; public Material Mender; }
        static readonly Dictionary<Camera, On> cameras = new Dictionary<Camera, On>();
        static readonly List<Camera> gone = new List<Camera>();
        static readonly int copy = Shader.PropertyToID("_DnCopy"), proj = Shader.PropertyToID("_DnProj"), texel = Shader.PropertyToID("_DnTexel");
        const string Named = "Keystone: the ground's facing, for the darkening of creases";
        static Shader shader;
        static bool tried;
        static Mesh sheet;
        static Camera[] all;
        static int lookAgain;

#if DEV
        public static string Said = "never asked";
#endif
        public static void Keep(bool wanted)
        {
#if DEV
            Said = (wanted ? "wanted" : "not wanted") + "; shader " + (shader != null ? "loaded" : tried ? "not loaded" : "not tried") + "; " + cameras.Count + " camera(s)";
#endif
            if (!wanted || !Load()) { Off(); return; }
            // (new cameras looked for twice a second, not every frame: the list of every camera is made anew each time it is asked
            // for, and each asked for its post-processing; with the film given its grade only when it changes, that was most of
            // Keystone's own work in a frame, some 35 µs. The cameras already found are kept up every frame.)
            if (Time.frameCount >= lookAgain || cameras.Count == 0 && Time.frameCount >= lookAgain - 25)
            {
                lookAgain = Time.frameCount + 30;
                int count = Camera.allCamerasCount;
                if (all == null || all.Length < count) all = new Camera[count + 4];
                count = Camera.GetAllCameras(all);
                for (int i = 0; i < count; i++)
                {
                    Camera cam = all[i];
                    if (cam != null && !cameras.ContainsKey(cam) && (cam.depthTextureMode & DepthTextureMode.DepthNormals) != 0 && cam.GetComponent<PostProcessLayer>() != null) Add(cam);
                }
                System.Array.Clear(all, 0, all.Length);
            }
            gone.Clear();
            foreach (KeyValuePair<Camera, On> one in cameras)
            {
                Camera cam = one.Key;
                if (cam == null) { gone.Add(cam); continue; }
                Matrix4x4 m = cam.projectionMatrix;
                one.Value.Mender.SetVector(proj, new Vector4(m[0, 0], m[1, 1], m[0, 2], m[1, 2]));
                one.Value.Mender.SetVector(texel, new Vector4(1f / Mathf.Max(cam.pixelWidth, 1), 1f / Mathf.Max(cam.pixelHeight, 1), SystemInfo.usesReversedZBuffer ? 1f : 0f, 0f));
            }
            foreach (Camera cam in gone) cameras.Remove(cam);
        }

        static void Add(Camera cam)
        {
            if (sheet == null)
            {
                sheet = new Mesh { name = "Keystone ground facing sheet" };
                sheet.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
                sheet.triangles = new[] { 0, 1, 2 };
            }
            var mender = new Material(shader);
            mender.SetVector("_DnSheet", new Vector4(1f, 1f, 0.5f, 1f));
            var commands = new CommandBuffer { name = Named };
            // (any left on the camera by an earlier copy of this code taken off first: a development build swapped in while the
            // game runs starts with none of its own, and the camera had nineteen of these after a night of it, each drawn)
            foreach (CommandBuffer left in cam.GetCommandBuffers(CameraEvent.AfterDepthNormalsTexture))
                if (left.name == Named) cam.RemoveCommandBuffer(CameraEvent.AfterDepthNormalsTexture, left);
            commands.GetTemporaryRT(copy, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
            commands.Blit(BuiltinRenderTextureType.DepthNormals, copy);
            commands.SetGlobalTexture(copy, copy);
            commands.SetRenderTarget(BuiltinRenderTextureType.DepthNormals);
            commands.DrawMesh(sheet, Matrix4x4.identity, mender);
            commands.ReleaseTemporaryRT(copy);
            cam.AddCommandBuffer(CameraEvent.AfterDepthNormalsTexture, commands);
            cameras[cam] = new On { Commands = commands, Mender = mender };
        }

        public static void Off()
        {
            foreach (KeyValuePair<Camera, On> one in cameras)
            {
                if (one.Key != null) one.Key.RemoveCommandBuffer(CameraEvent.AfterDepthNormalsTexture, one.Value.Commands);
                if (one.Value.Mender != null) Object.Destroy(one.Value.Mender);
            }
            cameras.Clear();
        }

        static bool Load()
        {
            if (tried) return shader != null;
            tried = true;
            try
            {
                string path = Kit.Folder + "dnfill.bundle";
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore || !File.Exists(path)) return false;
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) return false;
                foreach (Shader one in bundle.LoadAllAssets<Shader>()) if (one != null && one.isSupported) shader = one;
                bundle.Unload(false);
                if (shader != null) Kit.Log("Keystone", "the ground's facing is put in for the darkening of creases (no compute shaders here)");
            }
            catch (System.Exception ex) { shader = null; Kit.Log("Keystone", "the ground's facing could not be put in for the darkening of creases: " + ex.Message); }
            return shader != null;
        }
    }
}

#if DEV
namespace KeystoneTUFX
{
    /// <summary>(Development build: Unity's post-processing overlays shown in the game's cameras, for finding a fault: "AmbientOcclusion", "Normals", "Depth", "None".)</summary>
    public static class TufxDebug
    {
        public static string Overlay(string which)
        {
            DebugOverlay mode;
            try { mode = (DebugOverlay)System.Enum.Parse(typeof(DebugOverlay), string.IsNullOrEmpty(which) ? "None" : which.Trim(), true); }
            catch (System.Exception) { return "no such overlay: " + which; }
            int n = 0;
            foreach (PostProcessLayer layer in Object.FindObjectsOfType<PostProcessLayer>())
            {
                PostProcessDebug shown = layer.GetComponent<PostProcessDebug>();
                if (shown == null) shown = layer.gameObject.AddComponent<PostProcessDebug>();
                shown.postProcessLayer = layer;
                shown.debugOverlay = mode;
                n++;
            }
            return "overlay " + mode + " on " + n + " camera(s)";
        }

        /// <summary>(Whether the ground's facing is being put in for the darkening of creases, and on how many cameras.)</summary>
        public static string Facing() => GroundFacing.Said;

        /// <summary>(Every camera's antialiasing set by hand, for now: "TemporalAntialiasing", "SubpixelMorphologicalAntialiasing",
        /// "FastApproximateAntialiasing", "None". TUFX's profile is not touched, and it may put its own back.)</summary>
        public static string Aa(string which)
        {
            // ("restore": each camera's own, as first found)
            if ((which ?? "").Trim() == "restore")
            {
                string back = "";
                foreach (KeyValuePair<PostProcessLayer, PostProcessLayer.Antialiasing> o in own) if (o.Key != null) { o.Key.antialiasingMode = o.Value; back += o.Key.name + " " + o.Value + "; "; }
                own.Clear();
                return "put back: " + back;
            }
            // ("list": each camera's now; "Camera 00=TemporalAntialiasing;GalaxyCamera=None": those cameras each set)
            if ((which ?? "").Trim() == "list")
            {
                string now = "";
                foreach (PostProcessLayer layer in Object.FindObjectsOfType<PostProcessLayer>()) now += layer.name + " " + layer.antialiasingMode + "; ";
                return now;
            }
            if ((which ?? "").Contains("="))
            {
                string set = "";
                foreach (string pair in which.Split(';'))
                {
                    string[] kv = pair.Split('=');
                    if (kv.Length != 2) continue;
                    PostProcessLayer.Antialiasing m;
                    try { m = (PostProcessLayer.Antialiasing)System.Enum.Parse(typeof(PostProcessLayer.Antialiasing), kv[1].Trim(), true); } catch (System.Exception) { continue; }
                    foreach (PostProcessLayer layer in Object.FindObjectsOfType<PostProcessLayer>()) if (layer.name == kv[0].Trim()) { layer.antialiasingMode = m; set += layer.name + " " + m + "; "; }
                }
                return "set: " + set;
            }
            PostProcessLayer.Antialiasing mode;
            try { mode = (PostProcessLayer.Antialiasing)System.Enum.Parse(typeof(PostProcessLayer.Antialiasing), (which ?? "").Trim(), true); }
            catch (System.Exception) { return "no such antialiasing: " + which; }
            string was = "";
            foreach (PostProcessLayer layer in Object.FindObjectsOfType<PostProcessLayer>())
            {
                if (!own.ContainsKey(layer)) own[layer] = layer.antialiasingMode;
                was += layer.name + " " + layer.antialiasingMode + "; "; layer.antialiasingMode = mode;
            }
            return "was: " + was + "now " + mode;
        }

        static readonly Dictionary<PostProcessLayer, PostProcessLayer.Antialiasing> own = new Dictionary<PostProcessLayer, PostProcessLayer.Antialiasing>();
    }
}
#endif
