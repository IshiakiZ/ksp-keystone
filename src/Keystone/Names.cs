using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// Things' names and the cameras by name, without making garbage. Unity makes a new string every time an object's name is
    /// asked for, and a new array every time Camera.allCameras is: asked in every camera's callbacks and every frame (as the mods
    /// did, telling the flight's cameras apart by name), about 8-9 KB of garbage a frame, half a megabyte a second at sixty frames,
    /// for the collector, whose pauses are the game's hitches (found by the rendering research's audit, 2026-10-18). Here each
    /// object's name is asked of Unity once and kept by the object's number; the cameras are gone through in an array kept for it.
    /// (A name changed after it was first asked for is not seen: the cameras' and materials' names asked for here do not change.)
    /// </summary>
    public static class Names
    {
        static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        static Camera[] cameras = new Camera[16];

        /// <summary>An object's name, kept after the first time it is asked for.</summary>
        public static string Of(Object o)
        {
            if ((object)o == null) return null;
#if DEV
            if (plain) return o ? o.name : null;
#endif
            int id = o.GetInstanceID();
            if (names.TryGetValue(id, out string name)) return name;
            if (!o) return null;
            if (names.Count > 8192) names.Clear();          // (objects come and go with scenes: kept from growing without end)
            name = o.name;
            names[id] = name;
            return name;
        }

        /// <summary>Whether a camera has that name (the flight's own: "Camera 00" near, "Camera 01" far, "Camera ScaledSpace", "GalaxyCamera").</summary>
        public static bool Is(Camera camera, string name) => (object)camera != null && Of(camera) == name;

        /// <summary>The first camera of that name (only one that is active and enabled, if asked), or nothing: Camera.allCameras without its new array.</summary>
#if DEV
        static bool plain;
        /// <summary>(Development build: names asked of Unity every time and the cameras found in Camera.allCameras, as before, or kept: to measure the garbage.)</summary>
        public static string Plain() { plain = !plain; return plain ? "names asked of Unity each time, as before" : "names kept"; }
#endif

        public static Camera CameraNamed(string name, bool enabledOnly = true)
        {
#if DEV
            if (plain) { foreach (Camera c in UnityEngine.Camera.allCameras) if (c != null && c.name == name && (!enabledOnly || c.isActiveAndEnabled)) return c; return null; }
#endif
            int count = UnityEngine.Camera.allCamerasCount;
            if (cameras.Length < count) cameras = new Camera[count * 2];
            int got = UnityEngine.Camera.GetAllCameras(cameras);
            Camera found = null;
            for (int i = 0; i < got; i++)
            {
                Camera c = cameras[i];
                if (found == null && c != null && Of(c) == name && (!enabledOnly || c.isActiveAndEnabled)) found = c;
                cameras[i] = null;                          // (nothing kept alive by the array)
            }
            return found;
        }
    }
}
