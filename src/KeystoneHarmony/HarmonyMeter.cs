using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Keystone;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("KeystoneHarmony", 0, 1)]
[assembly: KSPAssemblyDependency("Keystone", 0, 6)]
[assembly: KSPAssemblyDependency("HarmonyKSP", 1, 0)]
#endif

namespace KeystoneHarmony
{
    /// <summary>
    /// Keystone's companion for the Harmony mod: it measures what each mod costs the game's main thread, for the window's
    /// page "What each mod costs". The game loads this library only where Harmony (HarmonyKSP) is installed; Keystone
    /// itself never needs Harmony.
    ///
    /// While the player has measuring switched on (never otherwise), every method of every mod that the game or Unity
    /// calls from outside it is wrapped in a stopwatch: the Unity messages (Update, LateUpdate, FixedUpdate, OnGUI, the
    /// cameras' ones), the part modules' OnUpdate and OnFixedUpdate, coroutines, whatever a mod has hung on another mod's
    /// or Unity's events (Camera.onPreCull, say), and the patches mods have put into the game with Harmony. Time spent in
    /// one mod's method while another mod's measured method is running is the inner one's, not both. Switching measuring
    /// off takes every wrapper away again.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class HarmonyAtStart : MonoBehaviour
    {
        void Awake()
        {
            // (a development build swapping a new copy in: whatever an old copy left wrapped, and its runner, go first)
            try { new Harmony(HarmonyMeter.Id).UnpatchAll(HarmonyMeter.Id); } catch (Exception) { }
            foreach (GameObject left in FindObjectsOfType<GameObject>()) if (left != null && left.name == HarmonyMeter.RunnerName) Destroy(left);
            if (!(Meter.Other is HarmonyMeter)) Meter.Other = new HarmonyMeter();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// What the stopwatches add up. Only the game's main thread is timed; a measured method called on another thread is
    /// counted. A stack of what is running says whose time it is: when a measured method begins inside another mod's,
    /// the outer one's clock stops until the inner one ends.
    /// </summary>
    static class Clock
    {
        public static readonly long[] Ticks = new long[Slots.Count];
        public static readonly long[] Calls = new long[Slots.Count];
        public static readonly long[] Elsewhere = new long[Slots.Count];
        public static readonly long[] Made = new long[Slots.Count];
        const int Deepest = 256;
        static readonly int[] stack = new int[Deepest];
        static readonly long[] since = new long[Deepest];
        static readonly long[] madeSince = new long[Deepest];
        static int depth;
        public static int Broken;                         // how often the stack was found not empty at the start of a frame (a method that threw, and never ended)
        [ThreadStatic] static bool main;
        /// <summary>How much this thread has made, where the runtime can say; else the heap in use (Coarse).</summary>
        public static Func<long> PerThread;
        /// <summary>(PerThread is the heap in use, not what this thread made: it grows a block at a time, so a call is charged a
        /// block now and then and most calls nothing, which over the window's frames adds up fairly; a collection shrinks it,
        /// and what goes down is left out. Only the main thread is counted, so another thread's making is charged to whatever
        /// the main thread was running then: small beside the main thread's own.)</summary>
        public static bool Coarse;

        public static void Begin()
        {
            main = true;
            depth = 0; Broken = 0;
            Array.Clear(Ticks, 0, Ticks.Length); Array.Clear(Calls, 0, Calls.Length); Array.Clear(Elsewhere, 0, Elsewhere.Length); Array.Clear(Made, 0, Made.Length);
        }

        /// <summary>At the start of each frame, from outside any measured method: nothing should be running.</summary>
        public static void FrameBegins()
        {
            if (depth != 0) { Broken++; depth = 0; }
        }

        public static void Enter(int slot)
        {
            if (!main) { Elsewhere[slot]++; return; }
            long now = Stopwatch.GetTimestamp();
            int d = depth;
            Func<long> made = PerThread;
            long bytes = made != null ? made() : 0L;
            if (d > 0 && d <= Deepest)
            {
                int outer = stack[d - 1];
                Ticks[outer] += now - since[d - 1];
                if (made != null) Made[outer] += Math.Max(0L, bytes - madeSince[d - 1]);
            }
            if (d < Deepest) { stack[d] = slot; since[d] = now; madeSince[d] = bytes; }
            depth = d + 1;
            Calls[slot]++;
        }

        public static void Leave(int slot)
        {
            if (!main) return;
            long now = Stopwatch.GetTimestamp();
            Func<long> made = PerThread;
            long bytes = made != null ? made() : 0L;
            int d = depth - 1;
            if (d < 0) { depth = 0; return; }
            if (d < Deepest)
            {
                Ticks[stack[d]] += now - since[d];
                if (made != null) Made[stack[d]] += Math.Max(0L, bytes - madeSince[d]);
            }
            depth = d;
            if (d > 0 && d <= Deepest) { since[d - 1] = now; madeSince[d - 1] = bytes; }
        }
    }

    /// <summary>A method that does nothing, for finding out what one measured call costs.</summary>
    static class Calibration
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Nothing(int x) => x + 1;
    }

    public sealed class HarmonyMeter : IMeter
    {
        public string Name => "Harmony";
        public bool Works => true;
        public string Why => "";
        public bool Running => runner != null;

        internal const string Id = "keystone.meter", RunnerName = "Keystone meter";
        const int Window = 180;                           // frames averaged over (three seconds at sixty a second)

        Harmony harmony;
        Runner runner;
#if DEV
        /// <summary>(Development: the mods of these names (with commas between) measured method by method, each its own line, to find which of their methods cost.)</summary>
        public static string Split = "";
        readonly Dictionary<string, Listed> splitLines = new Dictionary<string, Listed>();
#endif
        readonly List<Listed> owners = new List<Listed>();          // slot n + 2 is owners[n]'s (but the last, shared by all past it: see Shared): slot 0 is the calibration's, slot 1 work put down to no mod
        readonly List<Cost> costs = new List<Cost>();
        double frame, itself;
        double perCall, inner;                            // what one measured call costs in all (ms), and the part of it that lands inside what is measured
        string report = "";

        public IList<Cost> Costs => costs;
        public double Frame => frame;
        public double Itself => itself;
        public string Report => report;

        public void Start()
        {
            if (runner != null) return;
            var timer = Stopwatch.StartNew();
            harmony = new Harmony(Id);
            owners.Clear();
            Clock.Begin();
            Clock.PerThread = AllocationCounter();

            // What one measured call costs: a method that does nothing, timed without and then with the wrapping.
            const int Tries = 40000;
            int x = 0;
            var watch = Stopwatch.StartNew();
            for (int n = 0; n < Tries; n++) x = Calibration.Nothing(x);
            double bare = watch.Elapsed.TotalMilliseconds;
            MethodInfo nothing = typeof(Calibration).GetMethod("Nothing");
            bool calibrated = Wrap(nothing, 0);
            Clock.Begin();
            watch = Stopwatch.StartNew();
            for (int n = 0; n < Tries; n++) x = Calibration.Nothing(x);
            double wrapped = watch.Elapsed.TotalMilliseconds;
            perCall = calibrated ? Math.Max(0.0, (wrapped - bare) / Tries) : 0.0;
            inner = calibrated ? Clock.Ticks[0] * 1000.0 / Stopwatch.Frequency / Tries : 0.0;

            int wrappedCount = 0, failed = 0;
            foreach (KeyValuePair<MethodBase, int> target in Targets())
            {
                if (Wrap(target.Key, target.Value)) wrappedCount++;
                else failed++;
            }
            Clock.Begin();
            var go = new GameObject(RunnerName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            runner.Meter = this;
            report = wrappedCount + " methods of " + owners.Count + " mods timed (" + failed + " could not be); set up in " + timer.ElapsedMilliseconds + " ms; one timed call costs "
                     + (perCall * 1000.0).ToString("F2") + " microseconds" + (Clock.PerThread == null ? "; memory made cannot be told apart by mod here" : Clock.Coarse ? "; memory made is counted per mod roughly (from the heap's growth)" : "; memory made is counted per mod");
            Kit.Log("Keystone", "measuring: " + report);
        }

        public void Stop()
        {
            if (runner != null)
            {
                Runner r = runner;
                runner = null;
                if (r != null && r.gameObject != null) UnityEngine.Object.Destroy(r.gameObject);
            }
            if (harmony != null)
            {
                try { harmony.UnpatchAll(Id); }
                catch (Exception ex) { Kit.Log("Keystone", "could not take the measuring away: " + ex.Message); }
                harmony = null;
            }
            costs.Clear();
        }

        bool Wrap(MethodBase method, int slot)
        {
            try
            {
                Type each = Slots.Each[Math.Min(slot, Slots.Count - 1)];
                harmony.Patch(method, prefix: new HarmonyMethod(each.GetMethod("Prefix")), finalizer: new HarmonyMethod(each.GetMethod("Finalizer")));
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>GC.GetAllocatedBytesForCurrentThread, where this runtime has it and it really counts.</summary>
        static Func<long> AllocationCounter()
        {
            Clock.Coarse = false;
            try
            {
                MethodInfo m = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
                if (m != null)
                {
                    var counter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), m);
                    long before = counter();
                    var junk = new byte[100000];
                    long after = counter();
                    if (after - before >= junk.Length) return counter;
                }
            }
            catch (Exception) { }
            // (Unity 2019's Mono cannot say what a thread has made: the heap in use instead, as the game's own meter has it)
            try
            {
                Func<long> heap = () => GC.GetTotalMemory(false);
                long before = heap();
                var junk = new byte[100000];
                long after = heap();
                if (after - before >= junk.Length / 2) { Clock.Coarse = true; return heap; }
            }
            catch (Exception) { }
            return null;
        }

        // ---------------------------------------------------------------- what is timed

        static readonly HashSet<string> Messages = new HashSet<string>
        {
            "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage", "OnRenderObject", "OnWillRenderObject",
            "OnUpdate", "OnFixedUpdate", "OnLateUpdate",
        };

        /// <summary>The libraries of mods: those the game loaded from GameData, and Keystone's own (in a development build, the one library everything is in, this included).</summary>
        static List<Assembly> Libraries()
        {
            var list = new List<Assembly>();
            foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
            {
                if (a == null || a.assembly == null) continue;
                string name = a.assembly.GetName().Name;
                if (name == "Assembly-CSharp" || name == "Assembly-CSharp-firstpass" || name == "0Harmony" || name.StartsWith("MonoMod", StringComparison.Ordinal)) continue;
                if (!list.Contains(a.assembly)) list.Add(a.assembly);
            }
            Assembly keystone = typeof(Kit).Assembly;
            if (!list.Contains(keystone)) list.Add(keystone);
            return list;
        }

        /// <summary>This companion's own code, which is never timed (it is what does the timing).</summary>
        static bool Ours(Type t) => t != null && (t.Namespace == "KeystoneHarmony" || t.Assembly == typeof(Harmony).Assembly);

        static Type[] TypesOf(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return Array.FindAll(ex.Types, t => t != null); }
            catch (Exception) { return new Type[0]; }
        }

        static bool Timeable(MethodBase m)
        {
            if (m == null || m.DeclaringType == null || m.IsAbstract || m.ContainsGenericParameters || m.DeclaringType.ContainsGenericParameters) return false;
            if (Ours(m.DeclaringType)) return false;
            try { return m.GetMethodBody() != null; }
            catch (Exception) { return false; }
        }

        /// <summary>Every method to time, with the slot of the mod it belongs to.</summary>
        Dictionary<MethodBase, int> Targets()
        {
            var found = new Dictionary<MethodBase, int>();
            List<Assembly> libraries = Libraries();
            var mine = new HashSet<Assembly>(libraries);
            void Add(MethodBase m)
            {
                if (m == null || found.ContainsKey(m) || !Timeable(m) || !mine.Contains(m.DeclaringType.Assembly)) return;
                int slot = SlotOf(m.DeclaringType);
#if DEV
                // (the mod named in Split measured method by method, each its own line, in the slots left over)
                if (Split.Length > 0 && slot >= 2 && slot - 2 < owners.Count && Array.IndexOf(Split.Split(','), owners[slot - 2].Name) >= 0)
                {
                    string line = owners[slot - 2].Name + ": " + m.DeclaringType.Name + "." + m.Name;
                    if (!splitLines.TryGetValue(line, out Listed own)) { own = new Listed { Id = ":split:" + line, Name = line }; splitLines[line] = own; }
                    int at = owners.IndexOf(own);
                    if (at < 0) { owners.Add(own); at = owners.Count - 1; }
                    slot = Math.Min(at + 2, Slots.Count - 1);
                }
#endif
                found[m] = slot;
            }
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Assembly a in libraries)
                foreach (Type t in TypesOf(a))
                {
                    if (t.ContainsGenericParameters || Ours(t)) continue;
                    // Unity's messages and the part modules' own, on anything Unity drives
                    if (typeof(MonoBehaviour).IsAssignableFrom(t))
                        foreach (MethodInfo m in t.GetMethods(Own))
                            if (Messages.Contains(m.Name) && m.GetParameters().Length <= 2) Add(m);
                    // coroutines: what the compiler makes of a method that yields an IEnumerator (and not an IEnumerable, which is gone through in a loop)
                    if (t.Name.IndexOf(">d__", StringComparison.Ordinal) > 0 && typeof(IEnumerator).IsAssignableFrom(t) && !typeof(IEnumerable).IsAssignableFrom(t))
                        Add(t.GetMethod("MoveNext", Own));
                    // whatever is hung on a mod's own events
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                        try { if (f.GetValue(null) is Delegate d) foreach (Delegate one in d.GetInvocationList()) Add(one.Method); }
                        catch (Exception) { }
                    }
                }
            // whatever mods have hung on Unity's own events
            foreach (Delegate d in new Delegate[] { Camera.onPreCull, Camera.onPreRender, Camera.onPostRender })
                if (d != null) foreach (Delegate one in d.GetInvocationList()) Add(one.Method);
            // the patches mods have put into the game with Harmony
            try
            {
                foreach (MethodBase original in Harmony.GetAllPatchedMethods())
                {
                    Patches info = Harmony.GetPatchInfo(original);
                    if (info == null) continue;
                    foreach (IEnumerable<Patch> kind in new[] { info.Prefixes, info.Postfixes, info.Finalizers })
                        if (kind != null) foreach (Patch p in kind) if (p.owner != Id) Add(p.PatchMethod);
                }
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not look through other mods' patches: " + ex.Message); }
            return found;
        }

        /// <summary>The slot of the mod a type belongs to: by its library, or (one library holding several mods, as a development build's does) by its namespace.</summary>
        int SlotOf(Type t)
        {
            Listed who = Listing.Of(t.Assembly);
            if (who == null || (who.Base && t.Namespace != null && !t.Namespace.StartsWith("Keystone", StringComparison.Ordinal)))
            {
                string space = (t.Namespace ?? "").Split('.')[0];
                if (space.Length > 0)
                    foreach (Listed one in Listing.All)
                        foreach (string name in one.Names)
                            if (name.Length > 3 && (space.StartsWith(name, StringComparison.OrdinalIgnoreCase) || name.StartsWith(space, StringComparison.OrdinalIgnoreCase))) { who = one; goto found; }
            }
        found:
            if (who == null) return 1;
            int at = owners.IndexOf(who);
            if (at < 0) { owners.Add(who); at = owners.Count - 1; }
            return Math.Min(at + 2, Slots.Count - 1);
        }

        // ---------------------------------------------------------------- adding it up

        sealed class Sample { public long[] Ticks = new long[Slots.Count], Calls = new long[Slots.Count], Elsewhere = new long[Slots.Count], Made = new long[Slots.Count]; public float Length; }

        readonly Queue<Sample> samples = new Queue<Sample>();
        readonly long[] lastTicks = new long[Slots.Count], lastCalls = new long[Slots.Count], lastElsewhere = new long[Slots.Count], lastMade = new long[Slots.Count];
        float addedAt;

        /// <summary>At the end of each frame: this frame's share of each mod's clock.</summary>
        internal void FrameEnds(float length)
        {
            var s = samples.Count >= Window ? samples.Dequeue() : new Sample();
            for (int n = 0; n < Slots.Count; n++)
            {
                s.Ticks[n] = Clock.Ticks[n] - lastTicks[n]; lastTicks[n] = Clock.Ticks[n];
                s.Calls[n] = Clock.Calls[n] - lastCalls[n]; lastCalls[n] = Clock.Calls[n];
                s.Elsewhere[n] = Clock.Elsewhere[n] - lastElsewhere[n]; lastElsewhere[n] = Clock.Elsewhere[n];
                s.Made[n] = Clock.Made[n] - lastMade[n]; lastMade[n] = Clock.Made[n];
            }
            s.Length = length;
            samples.Enqueue(s);
#if DEV
            // (the length given is the frame before's, Unity's own delta time: so it goes with what was measured in the frame before)
            if (havePrevious) Keep(length, previousTicks, previousCalls);
            Array.Copy(s.Ticks, previousTicks, Slots.Count); Array.Copy(s.Calls, previousCalls, Slots.Count);
            havePrevious = true;
#endif
            if (Time.realtimeSinceStartup - addedAt > 0.5f) { addedAt = Time.realtimeSinceStartup; AddUp(); }
        }

#if DEV
        // (the five slowest frames since last asked, each with what every mod took in it)
        sealed class Kept { public float Length; public float At; public readonly long[] Ticks = new long[Slots.Count], Calls = new long[Slots.Count]; }
        readonly List<Kept> slowest = new List<Kept>();
        bool havePrevious;
        readonly long[] previousTicks = new long[Slots.Count], previousCalls = new long[Slots.Count];

        void Keep(float length, long[] ticks, long[] calls)
        {
            if (slowest.Count >= 5 && length <= slowest[slowest.Count - 1].Length) return;
            Kept k = slowest.Count >= 5 ? slowest[slowest.Count - 1] : new Kept();
            if (slowest.Count >= 5) slowest.RemoveAt(slowest.Count - 1);
            k.Length = length; k.At = Time.realtimeSinceStartup;
            Array.Copy(ticks, k.Ticks, Slots.Count); Array.Copy(calls, k.Calls, Slots.Count);
            int at = 0;
            while (at < slowest.Count && slowest[at].Length >= length) at++;
            slowest.Insert(at, k);
        }

        /// <summary>(Development: each mod's time in the five slowest frames since this was last asked, the slowest first.)</summary>
        public string Slowest()
        {
            double toMs = 1000.0 / Stopwatch.Frequency;
            var t = new System.Text.StringBuilder();
            float now = Time.realtimeSinceStartup;
            foreach (Kept k in slowest)
            {
                double all = 0;
                var lines = new List<KeyValuePair<double, string>>();
                for (int n = 1; n < Slots.Count; n++)
                {
                    double ms = Math.Max(0.0, k.Ticks[n] * toMs - k.Calls[n] * inner);
                    if (ms < 0.05) continue;
                    all += ms;
                    string who = n == Slots.Count - 1 && owners.Count > Slots.Count - 2 ? Shared().Name : n >= 2 && n - 2 < owners.Count ? owners[n - 2].Name : "not any mod's";
                    lines.Add(new KeyValuePair<double, string>(ms, who));
                }
                lines.Sort((a, b) => b.Key.CompareTo(a.Key));
                if (t.Length > 0) t.Append("\n");
                t.Append("a frame of " + (k.Length * 1000f).ToString("F1") + " ms, " + (now - k.At).ToString("F1") + " s ago: " + all.ToString("F1") + " ms of it in what was measured:");
                int shown = 0;
                foreach (KeyValuePair<double, string> line in lines) { if (shown++ >= 8) break; t.Append("\n    ").Append(line.Value).Append(": ").Append(line.Key.ToString("F1")).Append(" ms"); }
            }
            slowest.Clear();
            return t.Length > 0 ? t.ToString() : "no frames yet";
        }
#endif

        void AddUp()
        {
            int frames = samples.Count;
            if (frames == 0) return;
            double toMs = 1000.0 / Stopwatch.Frequency;
            var sum = new double[Slots.Count]; var worst = new double[Slots.Count]; var calls = new double[Slots.Count]; var elsewhere = new double[Slots.Count]; var made = new double[Slots.Count];
            double length = 0, allCalls = 0;
            foreach (Sample s in samples)
            {
                length += s.Length;
                for (int n = 1; n < Slots.Count; n++)
                {
                    // (less what the stopwatch itself adds inside each call)
                    double ms = Math.Max(0.0, s.Ticks[n] * toMs - s.Calls[n] * inner);
                    sum[n] += ms;
                    if (ms > worst[n]) worst[n] = ms;
                    calls[n] += s.Calls[n]; elsewhere[n] += s.Elsewhere[n]; made[n] += s.Made[n];
                    allCalls += s.Calls[n];
                }
            }
            frame = length * 1000.0 / frames;
            itself = allCalls / frames * perCall;
            costs.Clear();
            for (int n = 1; n < Slots.Count; n++)
            {
                if (calls[n] == 0 && elsewhere[n] == 0) continue;
                costs.Add(new Cost
                {
                    Who = n == Slots.Count - 1 && owners.Count > Slots.Count - 2 ? Shared() : n >= 2 && n - 2 < owners.Count ? owners[n - 2] : null,
                    Ms = sum[n] / frames, Worst = worst[n], Calls = calls[n] / frames, Elsewhere = elsewhere[n] / frames,
                    Bytes = Clock.PerThread != null ? made[n] / frames : -1,
                });
            }
            costs.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        }

        Listed shared;

        /// <summary>
        /// The last slot's line when more mods are measured than there are slots: every mod past the last but one, together, and
        /// named as such (it was named after the first of them, which was then given all the others' time).
        /// </summary>
        Listed Shared()
        {
            string name = (owners.Count - (Slots.Count - 3)) + " more mods together";
            if (shared == null || shared.Name != name) shared = new Listed { Id = ":shared:", Name = name };
            return shared;
        }

        /// <summary>What runs once a frame while measuring: the stack checked at the frame's start, and the frame added up at its end. (Its own methods are not measured.)</summary>
        sealed class Runner : MonoBehaviour
        {
            public HarmonyMeter Meter;
            readonly WaitForEndOfFrame end = new WaitForEndOfFrame();

            void Start() => StartCoroutine(Ends());

            void Update() => Clock.FrameBegins();

            IEnumerator Ends()
            {
                while (true)
                {
                    yield return end;
                    Meter?.FrameEnds(Time.unscaledDeltaTime);
                }
            }

            void OnDestroy()
            {
                // (a development build swapping a new copy in, or the game closing: the wrappers go with it)
                if (Meter != null && Meter.runner == this) Meter.Stop();
            }
        }
    }
}
