using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>What one mod was measured to cost, on average over the last few seconds.</summary>
    public sealed class Cost
    {
        /// <summary>The mod, as the list of mods has it (nothing: work that could not be put down to any mod).</summary>
        public Listed Who;
        /// <summary>Milliseconds of the game's main thread a frame: its own work only (what it called in another measured mod is that mod's).</summary>
        public double Ms;
        /// <summary>The most it took in any one frame, in milliseconds.</summary>
        public double Worst;
        /// <summary>How many times a frame its measured methods were called.</summary>
        public double Calls;
        /// <summary>Bytes of managed memory it made a frame (less than 0: this cannot be told here).</summary>
        public double Bytes = -1;
        /// <summary>Calls that came on other threads than the game's own, which are counted but not timed.</summary>
        public double Elsewhere;
    }

    /// <summary>Something that can measure what each mod costs (see KeystoneHarmony, the companion that does it where the Harmony mod is installed).</summary>
    public interface IMeter
    {
        string Name { get; }
        /// <summary>Whether it can, as things are.</summary>
        bool Works { get; }
        /// <summary>If it cannot, why, in words for the player.</summary>
        string Why { get; }
        /// <summary>Whether it is measuring now.</summary>
        bool Running { get; }
        /// <summary>Begin measuring (this may hold the game still for a moment, while it is set up).</summary>
        void Start();
        /// <summary>Stop measuring, and leave nothing of it in the game.</summary>
        void Stop();
        /// <summary>What each mod cost, the dearest first.</summary>
        IList<Cost> Costs { get; }
        /// <summary>A frame's length, milliseconds, on average over the same time.</summary>
        double Frame { get; }
        /// <summary>What the measuring itself costs, milliseconds a frame, worked out from what one measured call costs.</summary>
        double Itself { get; }
        /// <summary>A few words on what is measured and how (how many methods, how long setting it up took).</summary>
        string Report { get; }
    }

    /// <summary>
    /// What each mod costs. Measuring needs a way to time every mod's own methods as the game calls them, which needs the
    /// Harmony mod: Keystone's companion for it (KeystoneHarmony.dll, loaded by the game only where Harmony is
    /// installed) fills in Other. Measuring is off until the player switches it on, and stops when the window closes.
    ///
    /// What each mod loaded through the game from its own folder (pictures, models, sounds) needs nothing: see Loaded.
    /// </summary>
    public static class Meter
    {
        /// <summary>The way of measuring, if a companion that is installed has offered one.</summary>
        public static IMeter Other;

        public static IMeter Best => Other != null && Other.Works ? Other : null;

        public static string WhyNot => Best != null ? null : Other == null ? "Measuring what each mod costs needs the Harmony mod (HarmonyKSP), which is not installed." : Other.Why;

        /// <summary>What a mod loaded through the game from its folders: how many of each kind of thing, and roughly how much memory they take.</summary>
        public sealed class Load
        {
            public Listed Who;
            public int Pictures, Models, Sounds;
            /// <summary>Bytes, estimated from each thing's size and form (pictures as the graphics card keeps them, sounds as read in, models' vertices).</summary>
            public long PictureBytes, ModelBytes, SoundBytes;
            public long Bytes => PictureBytes + ModelBytes + SoundBytes;
        }

        static List<Load> loads;
        static float loadsAt = -100f;

        /// <summary>
        /// What each mod loaded through the game from its own folders (the game's database of pictures, models and sounds
        /// knows which folder each came from), the most first. Worked out again at most every ten seconds. What a mod makes
        /// for itself while it runs (its own textures, buffers, threads) cannot be told apart from the game's, and is not here.
        /// </summary>
        public static IList<Load> Loaded
        {
            get
            {
                if (loads == null || Time.realtimeSinceStartup - loadsAt > 10f) { loads = Count(); loadsAt = Time.realtimeSinceStartup; }
                return loads;
            }
        }

        static List<Load> Count()
        {
            var byFolder = new Dictionary<string, Listed>(StringComparer.OrdinalIgnoreCase);
            foreach (Listed one in Listing.All) foreach (string folder in one.Folders) if (folder.Length > 0 && !byFolder.ContainsKey(folder)) byFolder[folder] = one;
            var found = new Dictionary<Listed, Load>();
            Load For(string url)
            {
                int cut = (url ?? "").IndexOf('/');
                string folder = cut < 0 ? url ?? "" : url.Substring(0, cut);
                if (!byFolder.TryGetValue(folder, out Listed who)) return null;
                if (!found.TryGetValue(who, out Load load)) found[who] = load = new Load { Who = who };
                return load;
            }
            GameDatabase data = GameDatabase.Instance;
            if (data == null) return new List<Load>();
            try
            {
                foreach (GameDatabase.TextureInfo info in data.databaseTexture)
                {
                    if (info == null || info.texture == null) continue;
                    Load load = For(info.name);
                    if (load == null) continue;
                    load.Pictures++;
                    load.PictureBytes += Measured(info.texture, PictureBytes(info.texture));
                }
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not count the pictures: " + ex.Message); }
            try
            {
                foreach (GameObject model in data.databaseModel)
                {
                    if (model == null) continue;
                    Load load = For(model.name);
                    if (load == null) continue;
                    load.Models++;
                    foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                        if (filter != null && filter.sharedMesh != null) load.ModelBytes += Measured(filter.sharedMesh, MeshBytes(filter.sharedMesh));
                    foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (skin != null && skin.sharedMesh != null) load.ModelBytes += Measured(skin.sharedMesh, MeshBytes(skin.sharedMesh));
                }
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not count the models: " + ex.Message); }
            try
            {
                foreach (AudioClip clip in data.databaseAudio)
                {
                    if (clip == null) continue;
                    Load load = For(clip.name);
                    if (load == null) continue;
                    load.Sounds++;
                    load.SoundBytes += Measured(clip, (long)clip.samples * Math.Max(1, clip.channels) * 2);
                }
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not count the sounds: " + ex.Message); }
            var list = new List<Load>(found.Values);
            list.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            return list;
        }

        /// <summary>
        /// What Unity says a thing takes in memory, where it says (it does in this game, though its profiler is not there);
        /// else the estimate.
        /// </summary>
        static long Measured(UnityEngine.Object thing, long estimate)
        {
            try
            {
                long said = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(thing);
                if (said > 0) return said;
            }
            catch (Exception) { }
            return estimate;
        }

        /// <summary>A picture's size as the graphics card keeps it, more or less: its pixels in its form, with its smaller copies.</summary>
        public static long PictureBytes(Texture2D t)
        {
            double perPixel;
            switch (t.format)
            {
                case TextureFormat.DXT1: case TextureFormat.DXT1Crunched: case TextureFormat.BC4: case TextureFormat.ETC_RGB4: case TextureFormat.ETC2_RGB: perPixel = 0.5; break;
                case TextureFormat.DXT5: case TextureFormat.DXT5Crunched: case TextureFormat.BC5: case TextureFormat.BC7: case TextureFormat.BC6H: case TextureFormat.ETC2_RGBA8: perPixel = 1; break;
                case TextureFormat.Alpha8: case TextureFormat.R8: perPixel = 1; break;
                case TextureFormat.RGB565: case TextureFormat.ARGB4444: case TextureFormat.RGBA4444: case TextureFormat.R16: case TextureFormat.RG16: case TextureFormat.RHalf: perPixel = 2; break;
                case TextureFormat.RGB24: case TextureFormat.RGBA32: case TextureFormat.ARGB32: case TextureFormat.BGRA32: case TextureFormat.RFloat: case TextureFormat.RGHalf: perPixel = 4; break;
                case TextureFormat.RGBAHalf: case TextureFormat.RGFloat: perPixel = 8; break;
                case TextureFormat.RGBAFloat: perPixel = 16; break;
                default: perPixel = 4; break;
            }
            double bytes = (double)t.width * t.height * perPixel * (t.mipmapCount > 1 ? 4.0 / 3.0 : 1.0);
            return (long)bytes;
        }

        static long MeshBytes(Mesh mesh)
        {
            // (each vertex's place, normal, tangent, colour and texture places, and three indices a triangle)
            try { return (long)mesh.vertexCount * 56 + (mesh.isReadable ? (long)mesh.triangles.Length * 4 : (long)mesh.vertexCount * 6); }
            catch (Exception) { return (long)mesh.vertexCount * 62; }
        }

        /// <summary>Bytes in words: "12.4 MB".</summary>
        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString(bytes < 100L * 1024 * 1024 ? "F1" : "F0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
        }

#if DEV
        /// <summary>
        /// (For the development build: what this player offers for measuring without patching anything: Unity's own samplers,
        /// and whether the runtime can say how much one thread has made.)
        /// </summary>
        public static string Probe()
        {
            var s = new System.Text.StringBuilder();
            s.Append("debug build: ").Append(Debug.isDebugBuild).Append("; profiler supported: ").Append(UnityEngine.Profiling.Profiler.supported).Append(", enabled: ").Append(UnityEngine.Profiling.Profiler.enabled);
            foreach (string name in new[] { "BehaviourUpdate", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "FixedBehaviourUpdate", "Camera.Render", "GC.Alloc" })
            {
                try
                {
                    UnityEngine.Profiling.Recorder r = UnityEngine.Profiling.Recorder.Get(name);
                    s.Append("\nrecorder ").Append(name).Append(": valid ").Append(r != null && r.isValid).Append(", enabled ").Append(r != null && r.enabled).Append(", last ").Append(r != null ? r.elapsedNanoseconds : -1).Append(" ns");
                }
                catch (Exception ex) { s.Append("\nrecorder ").Append(name).Append(": ").Append(ex.GetType().Name); }
            }
            System.Reflection.MethodInfo perThread = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            s.Append("\nGC.GetAllocatedBytesForCurrentThread: ").Append(perThread != null ? "present" : "absent");
            if (perThread != null)
            {
                try
                {
                    long before = (long)perThread.Invoke(null, null);
                    var junk = new byte[200000];
                    long after = (long)perThread.Invoke(null, null);
                    s.Append(", 200000 bytes made read as ").Append(after - before).Append(junk.Length > 0 ? "" : "");
                }
                catch (Exception ex) { s.Append(", ").Append(ex.GetType().Name).Append(": ").Append(ex.Message); }
            }
            long a = GC.GetTotalMemory(false);
            var more = new byte[200000];
            long b = GC.GetTotalMemory(false);
            s.Append("\nGC.GetTotalMemory: ").Append(a).Append(", after 200000 bytes made: +").Append(b - a).Append(more.Length > 0 ? "" : "");
            try { s.Append("\nProfiler.GetMonoUsedSizeLong: ").Append(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()).Append(", runtime size of a texture: ").Append(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(Texture2D.whiteTexture)); }
            catch (Exception ex) { s.Append("\nProfiler sizes: ").Append(ex.GetType().Name); }
            s.Append("\nmeter: ").Append(Other == null ? "none offered" : Other.Name + (Other.Works ? " (works)" : " (does not work: " + Other.Why + ")"));
            return s.ToString();
        }
#endif
    }

#if DEV
    /// <summary>
    /// (For the development build: something that is busy on purpose, for checking the meter against: it spends so many
    /// milliseconds of every frame in its Update, doing nothing. Its time is Keystone's.)
    /// </summary>
    public sealed class Busy : MonoBehaviour
    {
        static float ms;

        void Update()
        {
            if (ms <= 0f) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.Elapsed.TotalMilliseconds < ms) { }
        }

        /// <summary>Be busy for this many milliseconds a frame (0: stop, and go, with every idle one made by Many).</summary>
        public static string Spin(string how)
        {
            float.TryParse(how, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ms);
            Busy[] all = FindObjectsOfType<Busy>();
            if (ms <= 0f) { foreach (Busy one in all) if (one != null) Destroy(one.gameObject); return "not busy (" + all.Length + " gone)"; }
            if (all.Length == 0) { var go = new GameObject("Keystone busy (a test)"); DontDestroyOnLoad(go); go.AddComponent<Busy>(); }
            return "busy for " + ms + " ms a frame";
        }

        /// <summary>Make so many components whose Update does nothing at all (0: take them away): for finding what one measured call costs, as Unity makes it.</summary>
        public static string Many(string how)
        {
            int.TryParse(how, out int n);
            foreach (Idle one in FindObjectsOfType<Idle>()) if (one != null) Destroy(one.gameObject);
            if (n <= 0) return "none";
            var go = new GameObject("Keystone idle (a test, many)");
            DontDestroyOnLoad(go);
            for (int i = 0; i < n; i++) go.AddComponent<Idle>();
            return n + " idle";
        }
    }

    /// <summary>(For the development build: nothing, every frame. See Busy.Many.)</summary>
    public sealed class Idle : MonoBehaviour
    {
        void Update() { }
    }
#endif
}
