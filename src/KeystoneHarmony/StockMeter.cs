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
            ticks.Clear(); calls.Clear();
            frameAtStart = Time.frameCount;
            since.Reset(); since.Start();
            said = done + " of the game's methods timed (" + failed + " could not be), set up in " + watch.ElapsedMilliseconds + " ms";
            return said;
        }

        static void Prefix(out long __state) { __state = Stopwatch.GetTimestamp(); }

        static void Postfix(long __state, MethodBase __originalMethod)
        {
            long spent = Stopwatch.GetTimestamp() - __state;
            ticks.TryGetValue(__originalMethod, out long had);
            ticks[__originalMethod] = had + spent;
            calls.TryGetValue(__originalMethod, out int n);
            calls[__originalMethod] = n + 1;
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
            if (!string.IsNullOrEmpty(arg)) int.TryParse(arg, out top);
            var rows = new List<KeyValuePair<MethodBase, long>>(ticks);
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            double perTick = 1000.0 / Stopwatch.Frequency;
            var b = new StringBuilder();
            b.Append(said).Append("; ").Append(frames).Append(" frames in ").Append(seconds.ToString("F1")).Append(" s (").Append((seconds * 1000.0 / frames).ToString("F2")).Append(" ms a frame)\n");
            for (int i = 0; i < rows.Count && i < top; i++)
            {
                MethodBase m = rows[i].Key;
                string who = m.DeclaringType != null ? m.DeclaringType.FullName : "?";
                b.Append("  ").Append((rows[i].Value * perTick / frames).ToString("F3").PadLeft(8)).Append(" ms  ").Append(((double)calls[m] / frames).ToString("F1").PadLeft(7)).Append(" calls  ").Append(who).Append(".").Append(m.Name).Append("\n");
            }
            ticks.Clear(); calls.Clear();
            return b.ToString();
        }

        // ---- named methods, any library: "Namespace.Type:Method,Namespace.Type:Method,..." (every overload of each)
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
            ticks.Clear(); calls.Clear();
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
}
#endif
