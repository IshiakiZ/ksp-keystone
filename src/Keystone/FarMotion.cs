using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Keystone
{
    /// <summary>
    /// The motion of what lies beyond the near camera, written into the near camera's picture of how far each pixel has moved,
    /// wherever that picture is kept (a smoothing between frames asks for it: TUFX's temporal antialiasing; and Keystone's lens
    /// when it smears). The game draws that picture from the near camera's own solid things alone: on OpenGL, where the scene is
    /// drawn by a near camera (to 400 m) and a far one, the far camera's ground, sea and clouds and the sky were all taken for
    /// things on the near camera's farthest plane, and on every system the sea, which is see-through, for the bottom under it.
    /// Climbing fast, the smoothing then fetched each pixel's past from pixels away, threw it out, and the far sea swung from
    /// frame to frame in the smoothing's eight-frame shake (the owner, 2026-10-18: "the ocean still flickers a whole bunch
    /// when you're going up"; filmed at 14-16 km by the main chat, gone with the smoothing put to another kind).
    ///
    /// So once the near camera has made that picture, and before anything see-through is drawn (Volumetric Explosions then
    /// writes its smoke's own motion over it), one pass (tools/shaderpack/farmotion.glsl, in GameData/Keystone/PluginData/
    /// farmotion.bundle) writes, where the near camera's own solid things are not the nearest thing, the motion of what is: the
    /// far camera's (from its depths, kept as it drew them), the sea's level, or clouds a mod hands over (Clouds), each as a
    /// place fixed to the planet; and of the sky, which only the camera's turning moves. Nothing is done where no such picture
    /// is kept. (research/rendering/experiments/far-motion.md)
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class FarMotion : MonoBehaviour
    {
        /// <summary>Whether the motion beyond the near camera is written (on unless a mod or the player says otherwise).</summary>
        public static bool On = true;
#if DEV
        /// <summary>(Development build, for timing it: "off" writes no far motion, anything else does.)</summary>
        public static string Switch(string on) { On = on == null || on.Trim() != "off"; return "far motion " + (On ? "on" : "off"); }
#endif

        /// <summary>What the last frame did, in words (for a page of settings or the development build).</summary>
        public static string Said { get; private set; } = "not begun";

        /// <summary>
        /// For a mod that lays clouds over the far scene (Natural Weather): this frame's picture of them as laid (a: how much shows
        /// through) and how far off along the ray they are (r, in km), for the pixels where they hide most of what is behind them
        /// to be followed as the clouds and not as what is behind. To be given every frame, before the near camera draws.
        /// </summary>
        public static void Clouds(Texture picture, Texture distance)
        {
            cloudsPicture = picture; cloudsDistance = distance; cloudsFrame = Time.frameCount;
        }
        static Texture cloudsPicture, cloudsDistance;
        static int cloudsFrame = -10;

        /// <summary>
        /// For a mod that keeps the far camera's depths itself, copied as that camera's solid things are done, into a picture of one
        /// float a pixel at the camera's size (Natural Ocean does, for its sea): handed here every frame, it is what this pass reads,
        /// and this copies none of its own. The same depths copied twice at the same moment were a whole picture copied once too
        /// often every frame, and the far camera's own picture put away and taken up again once more. Null when it stops.
        /// </summary>
        public static void FarDepthKept(RenderTexture depths)
        {
            keptDepth = depths; keptFrame = depths != null ? Time.frameCount : -10;
        }
        static RenderTexture keptDepth;
        static int keptFrame = -10;

#if DEV
        /// <summary>(Development build: 0 as it should be; 1 nothing written, the game's motion as it was; 2 every pixel written, the near camera's own solid things too, for looking at with Unity's overlay of motion.)</summary>
        public static float Test;
#endif

        // (when in the near camera's drawing: once its picture of motion is made, which is after its solid things, and before
        // anything see-through is drawn)
        const CameraEvent At = CameraEvent.AfterImageEffectsOpaque;

        static Shader shader;
        static bool tried;
        static Mesh sheet;
        Material pass;
        CommandBuffer nearCommands, farCommands;
        Camera nearOn, farOn;
        RenderTexture farDepth;
        // (what the pass reads this frame: the far camera, and its depths as this keeps them or as another mod does: FarDepthKept)
        Camera farRead;
        RenderTexture farDepthRead;

        // (a frame ago: what took a place, from the camera, to the picture; where the camera and the planet were, and how it was turned)
        bool known;
        string knownOn;
        Matrix4x4 seenWas;
        Vector3d eyeWas, planetWas;
        QuaternionD turnWas;

        static readonly int idLook = Shader.PropertyToID("_FmLook"), idRight = Shader.PropertyToID("_FmRight"), idUp = Shader.PropertyToID("_FmUp"), idAhead = Shader.PropertyToID("_FmAhead"),
            idNearZ = Shader.PropertyToID("_FmNearZ"), idFarZ = Shader.PropertyToID("_FmFarZ"),
            idNowX = Shader.PropertyToID("_FmNowX"), idNowY = Shader.PropertyToID("_FmNowY"), idNowW = Shader.PropertyToID("_FmNowW"),
            idWasX = Shader.PropertyToID("_FmWasX"), idWasY = Shader.PropertyToID("_FmWasY"), idWasW = Shader.PropertyToID("_FmWasW"),
            idGoesX = Shader.PropertyToID("_FmGoesX"), idGoesY = Shader.PropertyToID("_FmGoesY"), idGoesZ = Shader.PropertyToID("_FmGoesZ"),
            idSea = Shader.PropertyToID("_FmSea"), idSeaAt = Shader.PropertyToID("_FmSeaAt"), idClouds = Shader.PropertyToID("_FmClouds"),
            idFar = Shader.PropertyToID("_FmFar"), idCloudsPic = Shader.PropertyToID("_FmCloudsPic"), idCloudsDist = Shader.PropertyToID("_FmCloudsDist");

        void OnEnable() { Camera.onPreRender += BeforeDrawing; }
        void OnDisable() { Camera.onPreRender -= BeforeDrawing; Detach(); known = false; }

        void OnDestroy()
        {
            Camera.onPreRender -= BeforeDrawing;
            Detach();
            if (pass != null) Destroy(pass);
            if (farDepth != null) { farDepth.Release(); Destroy(farDepth); }
            nearCommands?.Release(); farCommands?.Release();
        }

        static bool Load()
        {
            if (tried) return shader != null;
            tried = true;
            try
            {
                // (the OpenGL one is packed by hand from the GLSL; the others built in the Unity editor from it turned into HLSL: tools/unityshaders)
                bool openGL = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore;
                string path = Kit.Folder + (openGL ? "farmotion.bundle" : "farmotion-" + (Application.platform == RuntimePlatform.WindowsPlayer ? "windows" : Application.platform == RuntimePlatform.LinuxPlayer ? "linux" : "mac") + ".bundle");
                if (!File.Exists(path)) { Said = "no shader for " + SystemInfo.graphicsDeviceType + " (" + Path.GetFileName(path) + ")"; Kit.Log("Keystone", "the motion beyond the near camera is not written: " + Said); return false; }
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) { Said = "its shader did not load"; return false; }
                foreach (Shader one in bundle.LoadAllAssets<Shader>()) if (one != null && one.isSupported) shader = one;
                bundle.Unload(false);
                if (shader == null) { Said = "its shader does not run here"; return false; }
                sheet = new Mesh { name = "Keystone: the motion sheet" };
                sheet.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(3f, -1f, 0f), new Vector3(-1f, 3f, 0f) };
                sheet.triangles = new[] { 0, 1, 2 };
                sheet.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
                Kit.Log("Keystone", "the motion beyond the near camera is written for the smoothing between frames");
            }
            catch (System.Exception ex) { shader = null; Said = "its shader did not load (" + ex.Message + ")"; Kit.Log("Keystone", "the motion beyond the near camera is not written: " + Said); }
            return shader != null;
        }

        void LateUpdate()
        {
            Camera near = Names.CameraNamed("Camera 00");
            bool wanted = On && near != null && (near.depthTextureMode & DepthTextureMode.MotionVectors) != 0 && FlightGlobals.currentMainBody != null && !MapView.MapIsEnabled;
#if DEV
            if (Test == 1f) wanted = false;
#endif
            if (!wanted || !Load()) { Detach(); known = false; if (shader != null || tried) Said = On ? "no picture of motion is kept" : "off"; return; }
            Camera far = Names.CameraNamed("Camera 01");
            if (far == near) far = null;
            Attach(near, far);
        }

        void Attach(Camera near, Camera far)
        {
            if (pass == null) { pass = new Material(shader) { name = "Keystone: the motion beyond the near camera" }; pass.SetVector("_FmSheet", new Vector4(1f, 1f, 0.5f, 1f)); }
            if (near != nearOn)
            {
                if (nearOn != null && nearCommands != null) nearOn.RemoveCommandBuffer(At, nearCommands);
                if (nearCommands == null) nearCommands = new CommandBuffer { name = "Keystone: the motion beyond the near camera" };
                nearCommands.Clear();
                nearCommands.SetRenderTarget(BuiltinRenderTextureType.MotionVectors);
                nearCommands.DrawMesh(sheet, Matrix4x4.identity, pass, 0, 0);
                nearCommands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                near.AddCommandBuffer(At, nearCommands);
                nearOn = near;
                known = false;
            }
            // (the far camera's depths as another mod keeps them, where one does: then this keeps none of its own)
            RenderTexture shared = far != null && keptDepth != null && Time.frameCount - keptFrame <= 1
                && keptDepth.width == Mathf.Max(far.pixelWidth, 1) && keptDepth.height == Mathf.Max(far.pixelHeight, 1) ? keptDepth : null;
            if (shared != null)
            {
                if (farOn != null && farCommands != null) farOn.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, farCommands);
                farOn = null;
                farRead = far; farDepthRead = shared;
                return;
            }
            bool farResized = far != null && (farDepth == null || farDepth.width != Mathf.Max(far.pixelWidth, 1) || farDepth.height != Mathf.Max(far.pixelHeight, 1));
            if (far != farOn || farResized)
            {
                if (farOn != null && farCommands != null) farOn.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, farCommands);
                farOn = null;
                if (far != null)
                {
                    if (farResized)
                    {
                        if (farDepth != null) { farDepth.Release(); Destroy(farDepth); }
                        farDepth = new RenderTexture(Mathf.Max(far.pixelWidth, 1), Mathf.Max(far.pixelHeight, 1), 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                        { name = "Keystone: the far camera's depths, for the motion beyond the near camera", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, useMipMap = false };
                        farDepth.Create();
                    }
                    if (farCommands == null) farCommands = new CommandBuffer { name = "Keystone: the far camera's depths, kept for the motion beyond the near camera" };
                    farCommands.Clear();
                    farCommands.Blit(BuiltinRenderTextureType.Depth, farDepth);
                    farCommands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                    far.AddCommandBuffer(CameraEvent.AfterForwardOpaque, farCommands);
                    farOn = far;
                }
            }
            farRead = farOn; farDepthRead = farOn != null ? farDepth : null;
        }

        void Detach()
        {
            if (nearOn != null && nearCommands != null) nearOn.RemoveCommandBuffer(At, nearCommands);
            if (farOn != null && farCommands != null) farOn.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, farCommands);
            nearOn = null; farOn = null; farRead = null; farDepthRead = null;
        }

        static Vector4 Row(Matrix4x4 m, int i) => m.GetRow(i);

        // (what turns a camera's depth into how far ahead along its axis: 1 / (x d + y), as Unity's _ZBufferParams' last two)
        static Vector2 Depths(Camera cam)
        {
            float n = Mathf.Max(cam.nearClipPlane, 1e-3f), f = cam.farClipPlane;
            return SystemInfo.usesReversedZBuffer ? new Vector2((f / n - 1f) / f, 1f / f) : new Vector2((1f - f / n) / f, f / n / f);
        }

        /// <summary>
        /// Just before the near camera draws (Smooth Running has carried it and the craft on to the frame's own moment by then): its
        /// axes and projection, what takes a place from it to the picture now and a frame ago, and how the planet has moved and
        /// turned in the scene since, from which a place fixed to it is found where it was.
        /// </summary>
        void BeforeDrawing(Camera cam)
        {
            if (cam == null || cam != nearOn || pass == null) return;
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body == null) return;
            Matrix4x4 lens = cam.nonJitteredProjectionMatrix, view = cam.worldToCameraMatrix;
            view.m03 = 0f; view.m13 = 0f; view.m23 = 0f;
            Matrix4x4 sees = lens * view;
            Vector3d eye = cam.transform.position, planet = body.position;
            QuaternionD turn = body.rotation;
            // (a place fixed to the planet, given from the camera now, h: it was at was(h) = G h + k from the camera then, where G is
            // the planet's turn since and k = (planet - eye) then + G (eye - planet) now, in double precision: both are hundreds
            // of kilometres, their difference metres. The game's shifting of the whole scene at once moves eye and planet alike and
            // changes nothing here; a camera put elsewhere, or a different world, is no motion.)
            Vector3d gx = new Vector3d(1, 0, 0), gy = new Vector3d(0, 1, 0), gz = new Vector3d(0, 0, 1), k = Vector3d.zero;
            Matrix4x4 seenThen = sees;
            if (known && knownOn == body.bodyName)
            {
                QuaternionD g = turnWas == turn ? QuaternionD.identity : turnWas * QuaternionD.Inverse(turn);
                gx = g * gx; gy = g * gy; gz = g * gz;
                k = (planetWas - eyeWas) + g * (eye - planet);
                seenThen = seenWas;
                if (k.magnitude > 5000.0) { gx = new Vector3d(1, 0, 0); gy = new Vector3d(0, 1, 0); gz = new Vector3d(0, 0, 1); k = Vector3d.zero; seenThen = sees; }
            }
            Transform t = cam.transform;
            pass.SetVector(idLook, new Vector4(lens.m00, lens.m11, lens.m02, lens.m12));
            pass.SetVector(idRight, t.right); pass.SetVector(idUp, t.up); pass.SetVector(idAhead, t.forward);
            Vector2 nz = Depths(cam);
            Camera far = farRead;
            RenderTexture depths = farDepthRead;
            pass.SetVector(idNearZ, new Vector4(nz.x, nz.y, far != null && depths != null ? 1f : 0f, SystemInfo.usesReversedZBuffer ? 1f : 0f));
            if (far != null && depths != null) { Vector2 fz = Depths(far); pass.SetVector(idFarZ, new Vector4(fz.x, fz.y, 0f, 0f)); pass.SetTexture(idFar, depths); }
            else pass.SetTexture(idFar, Texture2D.whiteTexture);
            pass.SetVector(idNowX, Row(sees, 0)); pass.SetVector(idNowY, Row(sees, 1)); pass.SetVector(idNowW, Row(sees, 3));
            pass.SetVector(idWasX, Row(seenThen, 0)); pass.SetVector(idWasY, Row(seenThen, 1)); pass.SetVector(idWasW, Row(seenThen, 3));
            pass.SetVector(idGoesX, new Vector4((float)gx.x, (float)gy.x, (float)gz.x, (float)k.x));
            pass.SetVector(idGoesY, new Vector4((float)gx.y, (float)gy.y, (float)gz.y, (float)k.y));
            pass.SetVector(idGoesZ, new Vector4((float)gx.z, (float)gy.z, (float)gz.z, (float)k.z));
            Vector3d toMiddle = planet - eye;
            pass.SetVector(idSea, new Vector4((float)toMiddle.x, (float)toMiddle.y, (float)toMiddle.z, body.ocean ? (float)body.Radius : 0f));
            float every = 0f;
#if DEV
            every = Test == 2f ? 1f : 0f;
#endif
            pass.SetVector(idSeaAt, new Vector4((float)(toMiddle.magnitude - body.Radius), every, 0f, 0f));
            bool clouds = cloudsFrame == Time.frameCount && cloudsPicture != null && cloudsDistance != null;
            pass.SetVector(idClouds, new Vector4(clouds ? 1f : 0f, 0.5f, 0f, 0f));
            pass.SetTexture(idCloudsPic, clouds ? cloudsPicture : Texture2D.whiteTexture);
            pass.SetTexture(idCloudsDist, clouds ? cloudsDistance : Texture2D.blackTexture);
            Said = "written" + (far != null ? " (two cameras)" : " (one camera)") + (clouds ? ", the clouds followed" : "") + (known ? "; the planet moved " + k.magnitude.ToString("F1") + " m against the camera since the last frame" : "; begun");
            seenWas = sees; eyeWas = eye; planetWas = planet; turnWas = turn; known = true; knownOn = body.bodyName;
        }
    }
}
