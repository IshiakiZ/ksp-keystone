#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace KeystoneHarmony
{
    /// <summary>
    /// (Development build, for the performance mod: what the game's own code costs its main thread, method by method.
    /// Keystone's meter leaves the game's own libraries out by design; this times them: every Unity message (Update,
    /// FixedUpdate, LateUpdate, OnGUI, the cameras' ones) of every class the game itself has, the part modules' OnUpdate and
    /// OnFixedUpdate, and its coroutines. Each method's time is its own and what it called (a method inside another timed
    /// one is counted in both: read as "where the time is", not as a sum). Through dev_call:
    /// KeystoneHarmony.StockMeter.Start, then Report (which takes every wrapper away).)
    /// </summary>
    public static class StockMeter
    {
        const string Id = "keystone.stockmeter.dev";
        static Harmony harmony;
        static readonly Dictionary<MethodBase, long> ticks = new Dictionary<MethodBase, long>();
        static readonly Dictionary<MethodBase, int> calls = new Dictionary<MethodBase, int>();
        static readonly Dictionary<MethodBase, long> worst = new Dictionary<MethodBase, long>();
        static readonly Dictionary<MethodBase, long> made = new Dictionary<MethodBase, long>();
        static bool countMade;
        static int frameAtStart;
        static readonly Stopwatch since = new Stopwatch();
        static string said = "";

        static readonly HashSet<string> Messages = new HashSet<string>
        {
            "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnPreCull", "OnPreRender", "OnPostRender", "OnRenderImage", "OnRenderObject", "OnWillRenderObject",
            "OnUpdate", "OnFixedUpdate",
        };

        public static string Start(string arg)
        {
            Stop();
            harmony = new Harmony(Id);
            var watch = Stopwatch.StartNew();
            int done = 0, failed = 0;
            MethodInfo prefix = typeof(StockMeter).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo postfix = typeof(StockMeter).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = a.GetName().Name;
                if (name != "Assembly-CSharp" && name != "Assembly-CSharp-firstpass") continue;
                Type[] types;
                try { types = a.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, t => t != null); }
                foreach (Type t in types)
                {
                    if (t == null || t.ContainsGenericParameters) continue;
                    var methods = new List<MethodBase>();
                    if (typeof(MonoBehaviour).IsAssignableFrom(t))
                        foreach (MethodInfo m in t.GetMethods(Own))
                            if (Messages.Contains(m.Name) && m.GetParameters().Length == 0) methods.Add(m);
                    if (t.Name.IndexOf(">d__", StringComparison.Ordinal) > 0 && typeof(IEnumerator).IsAssignableFrom(t) && !typeof(IEnumerable).IsAssignableFrom(t))
                    {
                        MethodInfo next = t.GetMethod("MoveNext", Own);
                        if (next != null) methods.Add(next);
                    }
                    foreach (MethodBase m in methods)
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters) continue;
                        try
                        {
                            if (m.GetMethodBody() == null) continue;
                            harmony.Patch(m, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                            done++;
                        }
                        catch (Exception) { failed++; }
                    }
                }
            }
            ticks.Clear(); calls.Clear(); worst.Clear();
            frameAtStart = Time.frameCount;
            since.Reset(); since.Start();
            said = done + " of the game's methods timed (" + failed + " could not be), set up in " + watch.ElapsedMilliseconds + " ms";
            return said;
        }

        static void Prefix(out long __state) { __state = Stopwatch.GetTimestamp(); if (countMade && System.Threading.Thread.CurrentThread.ManagedThreadId == mainThread) heapAt.Push(GC.GetTotalMemory(false)); }
        static int mainThread = -1;
        static readonly Stack<long> heapAt = new Stack<long>();

        static void Postfix(long __state, MethodBase __originalMethod)
        {
            long spent = Stopwatch.GetTimestamp() - __state;
            if (countMade && heapAt.Count > 0 && System.Threading.Thread.CurrentThread.ManagedThreadId == mainThread)
            {
                // (what the heap grew by over the call, counting only growth: a collection inside it shrinks the heap. Coarse: the
                // heap takes memory a block at a time, so a call is charged for a block now and then; added up it is fair.)
                long grew = GC.GetTotalMemory(false) - heapAt.Pop();
                if (grew > 0) { made.TryGetValue(__originalMethod, out long m0); made[__originalMethod] = m0 + grew; }
            }
            ticks.TryGetValue(__originalMethod, out long had);
            ticks[__originalMethod] = had + spent;
            calls.TryGetValue(__originalMethod, out int n);
            calls[__originalMethod] = n + 1;
            worst.TryGetValue(__originalMethod, out long most);
            if (spent > most) worst[__originalMethod] = spent;
        }

        static void Stop()
        {
            if (harmony != null) { try { harmony.UnpatchAll(Id); } catch (Exception) { } }
            harmony = null;
        }

        public static string Report(string arg)
        {
            int frames = Math.Max(1, Time.frameCount - frameAtStart);
            double seconds = since.Elapsed.TotalSeconds;
            Stop();
            int top = 40;
            // ("worst" first: by each method's dearest single call, for the frames that stall)
            bool byWorst = arg != null && arg.StartsWith("worst");
            if (byWorst) arg = arg.Substring(5).Trim();
            bool byMade = arg != null && arg.StartsWith("made");
            if (byMade) arg = arg.Substring(4).Trim();
            if (!string.IsNullOrEmpty(arg)) int.TryParse(arg, out top);
            var rows = new List<KeyValuePair<MethodBase, long>>(ticks);
            if (byWorst) rows.Sort((x, y) => worst[y.Key].CompareTo(worst[x.Key]));
            else if (byMade) rows.Sort((x, y) => Made(y.Key).CompareTo(Made(x.Key)));
            else rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            double perTick = 1000.0 / Stopwatch.Frequency;
            var b = new StringBuilder();
            b.Append(said).Append("; ").Append(frames).Append(" frames in ").Append(seconds.ToString("F1")).Append(" s (").Append((seconds * 1000.0 / frames).ToString("F2")).Append(" ms a frame)\n");
            for (int i = 0; i < rows.Count && i < top; i++)
            {
                MethodBase m = rows[i].Key;
                string who = m.DeclaringType != null ? m.DeclaringType.FullName : "?";
                b.Append("  ").Append((rows[i].Value * perTick / frames).ToString("F3").PadLeft(8)).Append(" ms  ").Append(((double)calls[m] / frames).ToString("F1").PadLeft(7)).Append(" calls  worst ")
                 .Append((worst[m] * perTick).ToString("F2").PadLeft(6)).Append(" ms  ").Append(countMade ? (Made(m) / 1024.0 / frames).ToString("F1").PadLeft(6) + " KB made  " : "").Append(who).Append(".").Append(m.Name).Append("\n");
            }
            ticks.Clear(); calls.Clear(); worst.Clear(); made.Clear(); countMade = false; heapAt.Clear();
            return b.ToString();
        }

        // ---- named methods, any library: "Namespace.Type:Method,Namespace.Type:Method,..." (every overload of each)
        static long Made(MethodBase m) => made.TryGetValue(m, out long b) ? b : 0;

        /// <summary>(As Named or Start, and what each call makes for the collector counted too: "made" then the list, or "made" alone for the game's own.)</summary>
        public static string Made(string list)
        {
            string said2 = string.IsNullOrEmpty(list) ? Start("") : Named(list);
            made.Clear(); heapAt.Clear(); countMade = true; mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            return said2 + "; what each makes counted";
        }

        public static string Named(string list)
        {
            Stop();
            harmony = new Harmony(Id);
            MethodInfo prefix = typeof(StockMeter).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo postfix = typeof(StockMeter).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
            int done = 0; var missing = new List<string>();
            foreach (string one in (list ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] tm = one.Trim().Split(':');
                // (the newest library that has it: a development build swaps its mods in as a new library each time, and the old
                // copies of the same classes stay loaded, idle)
                Type t = null;
                if (tm.Length == 2)
                {
                    Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = all.Length - 1; i >= 0 && t == null; i--) { try { t = all[i].GetType(tm[0], false); } catch (Exception) { } }
                }
                bool any = false;
                if (t != null)
                    foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (m.Name == tm[1] && !m.IsAbstract && !m.ContainsGenericParameters)
                        {
                            try { harmony.Patch(m, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix)); done++; any = true; } catch (Exception) { }
                        }
                if (!any) missing.Add(one);
            }
            ticks.Clear(); calls.Clear(); worst.Clear();
            frameAtStart = Time.frameCount;
            since.Reset(); since.Start();
            said = done + " methods timed" + (missing.Count > 0 ? " (not found: " + string.Join(", ", missing) + ")" : "");
            return said;
        }

        // ---- the terrain's spheres, one by one (the quad sphere's coroutine is the dearest of the game's own at the pad)
        static readonly Dictionary<string, long> pqsTicks = new Dictionary<string, long>();
        static readonly Dictionary<string, int> pqsCalls = new Dictionary<string, int>();

        public static string PqsStart(string arg)
        {
            Stop();
            harmony = new Harmony(Id);
            MethodInfo prefix = typeof(StockMeter).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo post = typeof(StockMeter).GetMethod(nameof(PqsPostfix), BindingFlags.Static | BindingFlags.NonPublic);
            int done = 0;
            foreach (string name in new[] { "UpdateQuads", "UpdateVisual", "Mod_OnPreUpdate", "Mod_OnUpdateFinished" })
            {
                MethodInfo m = typeof(PQS).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) continue;
                harmony.Patch(m, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(post));
                done++;
            }
            pqsTicks.Clear(); pqsCalls.Clear();
            frameAtStart = Time.frameCount;
            var b = new StringBuilder(done + " methods timed; the spheres:");
            foreach (PQS q in UnityEngine.Object.FindObjectsOfType<PQS>())
                b.Append("\n  ").Append(q.name).Append(" active ").Append(q.isActive).Append(" disabled ").Append(q.isDisabled).Append(" alive ").Append(q.isAlive).Append(" frameTimeDelta ").Append(q.frameTimeDelta).Append(" target ").Append(q.target != null ? q.target.name : "none");
            return b.ToString();
        }

        static void PqsPostfix(long __state, MethodBase __originalMethod, PQS __instance)
        {
            long spent = Stopwatch.GetTimestamp() - __state;
            string key = (__instance != null ? __instance.name : "?") + "." + __originalMethod.Name;
            pqsTicks.TryGetValue(key, out long had); pqsTicks[key] = had + spent;
            pqsCalls.TryGetValue(key, out int n); pqsCalls[key] = n + 1;
        }

        public static string PqsReport(string arg)
        {
            int frames = Math.Max(1, Time.frameCount - frameAtStart);
            Stop();
            var rows = new List<KeyValuePair<string, long>>(pqsTicks);
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            double perTick = 1000.0 / Stopwatch.Frequency;
            var b = new StringBuilder(frames + " frames\n");
            for (int i = 0; i < rows.Count && i < 30; i++)
                b.Append("  ").Append((rows[i].Value * perTick / frames).ToString("F3").PadLeft(8)).Append(" ms  ").Append(((double)pqsCalls[rows[i].Key] / frames).ToString("F1").PadLeft(6)).Append(" calls  ").Append(rows[i].Key).Append("\n");
            return b.ToString();
        }
    }

    /// <summary>(For the development build, through dev_call: Keystone's own "What each mod costs" switched on and read without its
    /// window: "start", then after some seconds "read" (each mod's milliseconds and memory made a frame, the dearest first), "stop".)</summary>
    public static class ModCosts
    {
        public static string Do(string what)
        {
            Keystone.IMeter m = Keystone.Meter.Best;
            if (m == null) return "no meter: " + Keystone.Meter.WhyNot;
            switch ((what ?? "").Trim())
            {
                case "start": if (!m.Running) m.Start(); return "measuring: " + m.Report;
                case "stop": if (m.Running) m.Stop(); return "stopped";
                case "slowest": return m is HarmonyMeter h ? h.Slowest() : "not the Harmony meter";
                default: if ((what ?? "").StartsWith("split")) { HarmonyMeter.Split = what.Substring(5).Trim(); if (m.Running) m.Stop(); m.Start(); return "measuring " + (HarmonyMeter.Split.Length > 0 ? HarmonyMeter.Split + " method by method" : "every mod whole") + ": " + m.Report; } break;
            }
            var b = new System.Text.StringBuilder("frame " + m.Frame.ToString("F2") + " ms, the measuring itself " + m.Itself.ToString("F2") + " ms; " + m.Report);
            var costs = new System.Collections.Generic.List<Keystone.Cost>(m.Costs);
            costs.Sort((x, y) => Math.Max(y.Bytes, 0).CompareTo(Math.Max(x.Bytes, 0)));
            b.Append("\n  by memory made a frame:");
            foreach (Keystone.Cost c in costs) if (c.Bytes > 0) b.Append("\n    ").Append(c.Who != null ? c.Who.Name : "(no mod)").Append(": ").Append((c.Bytes / 1024.0).ToString("F1")).Append(" KB, ").Append(c.Ms.ToString("F2")).Append(" ms");
            costs.Sort((x, y) => y.Ms.CompareTo(x.Ms));
            b.Append("\n  by time a frame:");
            int n = 0;
            foreach (Keystone.Cost c in costs) { if (n++ >= 24) break; b.Append("\n    ").Append(c.Who != null ? c.Who.Name : "(no mod)").Append(": ").Append(c.Ms.ToString("F2")).Append(" ms (worst ").Append(c.Worst.ToString("F1")).Append("), ").Append((Math.Max(c.Bytes, 0) / 1024.0).ToString("F1")).Append(" KB"); }
            return b.ToString();
        }
    }
}
#endif
