using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Keystone
{
    /// <summary>
    /// A small picture read back from the graphics card without the game waiting for it. Reading a picture back at once
    /// (ReadPixels) makes the game wait until the card has done everything it was given, the frame's clouds and sea with it:
    /// measured in the game on a Mac (2026-10-17), the clouds' one pixel and the sea's small picture, each every fourth frame,
    /// cost the main thread about 17 ms between them at worst and the frame about 2.5 ms on the whole. Here the read is asked
    /// for, and its answer taken a few frames later, by whichever way this game has:
    ///   * Unity's own asynchronous read, where the card has it (Direct3D on Windows; Metal);
    ///   * on OpenGL (the Mac's, Linux's), where Unity has none, Volumetric Explosions' library when it is installed and loaded
    ///     (its asyncread.c: the copy begun on the render thread into a pixel buffer behind a fence, collected when the fence
    ///     has passed; looked up by name, so neither mod needs the other);
    ///   * otherwise, read at once as before (Ask then reads, and Take has it at once).
    /// An answer comes some frames after it was asked for (about five with the library); what was handed to Ask with it comes
    /// back with it, for what depends on when it was asked.
    /// </summary>
    public sealed class Readback
    {
        // ---- the library's functions (asyncread.c), found once
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int RequestFn(int slot, uint texture, int x, int y, int w, int h, int floats);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long ResultFn(int slot, IntPtr into, int room, out long frames);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr EventFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ReadyFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int VersionFn();
        static bool looked;
        static RequestFn request;
        static ResultFn result;
        static ReadyFn ready;
        static IntPtr eventFn;
        static readonly bool[] slotsTaken = new bool[8];
        static int usingLibrary;
        static Pump pump;

        static void Look()
        {
            looked = true;
            if (SystemInfo.supportsAsyncGPUReadback) return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore) return;
            try
            {
                // (a development build has every mod in its own library; an installed game has Volumetric Explosions in its own)
                Type native = typeof(Readback).Assembly.GetType("VolumetricExplosions.Native", false);
                if (native == null)
                    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (a.GetName().Name != "VolumetricExplosions") continue;
                        native = a.GetType("VolumetricExplosions.Native", false);
                        if (native != null) break;
                    }
                MethodInfo symbol = native?.GetMethod("Symbol", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (symbol == null) { Kit.Log("Keystone", "pictures are read back at once: Volumetric Explosions is not installed, or is older than 0.7.2"); return; }
                IntPtr F(string name) => (IntPtr)symbol.Invoke(null, new object[] { name });
                IntPtr req = F("ar_request"), res = F("ar_result"), ev = F("ar_event"), rd = F("ar_ready"), ver = F("ar_version");
                if (req == IntPtr.Zero || res == IntPtr.Zero || ev == IntPtr.Zero || rd == IntPtr.Zero || ver == IntPtr.Zero)
                {
                    Kit.Log("Keystone", "pictures are read back at once: Volumetric Explosions' library is not loaded, or has no asynchronous read");
                    return;
                }
                if (((VersionFn)Marshal.GetDelegateForFunctionPointer(ver, typeof(VersionFn)))() < 3) return;
                request = (RequestFn)Marshal.GetDelegateForFunctionPointer(req, typeof(RequestFn));
                result = (ResultFn)Marshal.GetDelegateForFunctionPointer(res, typeof(ResultFn));
                ready = (ReadyFn)Marshal.GetDelegateForFunctionPointer(rd, typeof(ReadyFn));
                eventFn = ((EventFn)Marshal.GetDelegateForFunctionPointer(ev, typeof(EventFn)))();
                Kit.Log("Keystone", "pictures are read back from the graphics card without waiting, by Volumetric Explosions' library");
            }
            catch (Exception ex)
            {
                request = null; result = null; ready = null; eventFn = IntPtr.Zero;
                Kit.Log("Keystone", "pictures are read back at once (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
        }

        /// <summary>
        /// Sends the library's event once a frame, at its end, after every camera has drawn: on the render thread the copies asked
        /// for are begun behind the frame's drawing, and the finished ones collected.
        /// </summary>
        sealed class Pump : MonoBehaviour
        {
            IEnumerator Start()
            {
                var end = new WaitForEndOfFrame();
                while (true)
                {
                    yield return end;
                    if (usingLibrary > 0 && eventFn != IntPtr.Zero) GL.IssuePluginEvent(eventFn, 0);
                }
            }
        }

        // ---- one picture read again and again
        readonly int width, height;
        readonly bool floats;
        readonly int slot = -1;
        int way;                                // 0 at once, 1 Unity's asynchronous read, 2 the library
        readonly byte[] bytes;
        IntPtr room;
        Texture2D atOnce;
        RenderTexture namedFor;
        uint name;
        long asked, taken;
        int errors;
        bool fresh, waiting;
        object takenTag;
        readonly Dictionary<long, object> tags = new Dictionary<long, object>();

        /// <summary>A read of up to so many pixels each way, four floats a pixel (floats) or four bytes.</summary>
        public Readback(int width, int height, bool floats)
        {
            if (!looked) Look();
            this.width = width; this.height = height; this.floats = floats;
            bytes = new byte[width * height * (floats ? 16 : 4)];
            if (SystemInfo.supportsAsyncGPUReadback) way = 1;
            else if (request != null && bytes.Length <= 65536)
            {
                for (int i = 0; i < slotsTaken.Length; i++) if (!slotsTaken[i]) { slotsTaken[i] = true; slot = i; break; }
                if (slot >= 0)
                {
                    way = 2;
                    room = Marshal.AllocHGlobal(bytes.Length);
                    usingLibrary++;
                    if (pump == null) { var holder = new GameObject("Keystone read-back"); UnityEngine.Object.DontDestroyOnLoad(holder); pump = holder.AddComponent<Pump>(); }
                }
            }
        }

        /// <summary>Which way this game reads: for a mod's page.</summary>
        public string Way => way == 1 ? "Unity's asynchronous read" : way == 2 ? (ready != null && ready() < 0 ? "Volumetric Explosions' library, which found no OpenGL" : "Volumetric Explosions' library") : "read at once";

        /// <summary>Whether reads come back later (true), or at once with the game waiting for the card (false: then ask only as often as you must).</summary>
        public bool Later => way != 0
#if DEV
            && !atOnceAll
#endif
            ;
#if DEV
        static bool atOnceAll;
        /// <summary>(Development build: every read made at once, as before, or back to the way this game has: to measure the difference.)</summary>
        public static string AtOnce() { atOnceAll = !atOnceAll; return atOnceAll ? "every picture read back at once" : "pictures read back the way this game can"; }
#endif

        /// <summary>
        /// Ask for a region of a picture (x, y from its first row, in pixels) to be read. Cheap where reads come back later (asked
        /// again before the last came back, the newest is read); where they do not, it reads now and waits. What is given as tag
        /// comes back with the answer.
        /// </summary>
        public void Ask(RenderTexture picture, int x, int y, int w, int h, object tag = null)
        {
            if (picture == null || !picture.IsCreated() || w > width || h > height || w <= 0 || h <= 0) return;
            if (way == 1 && Later)
            {
                if (waiting) return;                // (Unity's: one at a time, the next asked for when it is back)
                waiting = true;
                long n = ++asked;
                AsyncGPUReadback.Request(picture, 0, x, w, y, h, 0, 1, floats ? TextureFormat.RGBAFloat : TextureFormat.RGBA32, r =>
                {
                    waiting = false;
                    if (r.hasError) { if (++errors >= 3) { way = 0; Kit.Log("Keystone", "a picture is read back at once after all: Unity's asynchronous read of it failed"); } return; }
                    var data = r.GetData<byte>();
                    int count = Math.Min(data.Length, bytes.Length);
                    for (int i = 0; i < count; i++) bytes[i] = data[i];
                    taken = n; takenTag = tag; fresh = true;
                });
                return;
            }
            if (way == 2 && Later)
            {
                // (the texture's name on the card, asked of Unity only when the picture is another: asking waits for the render thread)
                if (picture != namedFor) { name = (uint)picture.GetNativeTexturePtr().ToInt64(); namedFor = picture; }
                long n = ++asked;
                tags[n] = tag;
                if (tags.Count > 16) { var old = new List<long>(); foreach (long k in tags.Keys) if (k < n - 12) old.Add(k); foreach (long k in old) tags.Remove(k); }
                request(slot, name, x, y, w, h, floats ? 1 : 0);
                return;
            }
            if (atOnce == null || atOnce.width != w || atOnce.height != h) atOnce = new Texture2D(w, h, floats ? TextureFormat.RGBAFloat : TextureFormat.RGBA32, false, true);
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = picture;
            atOnce.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
            RenderTexture.active = before;
            var raw = atOnce.GetRawTextureData<byte>();
            int c = Math.Min(raw.Length, bytes.Length);
            for (int i = 0; i < c; i++) bytes[i] = raw[i];
            taken = ++asked; takenTag = tag; fresh = true;
        }

        /// <summary>
        /// Whether an answer has come back since the last Take; if so it is in Bytes (or Pixel / Colors), and what was handed to
        /// Ask with it in tag.
        /// </summary>
        public bool Take(out object tag)
        {
            if (way == 2 && Later)
            {
                long n = result(slot, room, bytes.Length, out long frames);
                if (n > taken)
                {
                    Marshal.Copy(room, bytes, 0, bytes.Length);
                    taken = n; Frames = (int)frames;
                    tags.TryGetValue(n, out takenTag);
                    fresh = true;
                }
                else if (taken == 0 && (asked > 90 || ready() < 0))
                {
                    // (nothing has ever come back: the library could not read on this card; read at once from here on)
                    Kit.Log("Keystone", "a picture is read back at once after all: Volumetric Explosions' library " + (ready() < 0 ? "found no OpenGL" : "never answered"));
                    Free();
                    way = 0;
                }
            }
            tag = takenTag;
            if (!fresh) return false;
            fresh = false;
            return true;
        }

        /// <summary>The last answer, as the card has it: four floats or four bytes a pixel, row by row from the first.</summary>
        public byte[] Bytes => bytes;

        /// <summary>How many frames the last answer took to come back (the library's way only).</summary>
        public int Frames { get; private set; }

        /// <summary>One pixel of the last answer, of a read of four floats a pixel.</summary>
        public Color Pixel(int i = 0)
        {
            int at = i * 16;
            return new Color(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8), BitConverter.ToSingle(bytes, at + 12));
        }

        /// <summary>No longer wanted: its place in the library is given up.</summary>
        public void Free()
        {
            if (slot >= 0 && room != IntPtr.Zero)
            {
                slotsTaken[slot] = false;
                usingLibrary--;
                Marshal.FreeHGlobal(room);
                room = IntPtr.Zero;
            }
            if (atOnce != null) { UnityEngine.Object.Destroy(atOnce); atOnce = null; }
        }
    }
}
