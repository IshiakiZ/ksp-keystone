#if DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace KeystoneHarmony
{
    /// <summary>
    /// (Development: a tracer, for finding what a stall of the game is made of (2026-10-18). Every method of what is named is
    /// wrapped, and each call's beginning and end on the main thread noted with the time, and each frame's beginning; only the
    /// frames slower than a limit are kept. "take" writes them to ~/.ksp-ai-bridge/trace.txt for tools/dev/trace.py, which lays
    /// the slowest out as trees of calls.
    ///
    /// "patch NAMES": with commas between, "ns:Root" for every class of that namespace (a mod's: the newest copy of it, in a
    /// development build that has reloaded it), or a class's full name ("ModuleEngines", "KSP.UI.Screens.StageManager"), with a
    /// "!" before it for even its smallest methods (Unity's own classes, whose methods mostly only hand on to the engine). Wrap
    /// only where no worker thread is running what is wrapped (a quiet scene), and know that a wrapper can fail the runtime's
    /// own check as it is compiled, which stops the game (once, 2026-10-18: the compiler's own methods are now left out). The
    /// wrapping is done a few dozen methods a frame ("status" says how far). "on MS" keeps the frames longer than MS
    /// milliseconds from now on, "off" stops, "take" writes, "unpatch" takes every wrapper away.)
    /// </summary>
    public static class Trace
    {
        const string Id = "keystone.trace";
        static Harmony harmony;
        static readonly Dictionary<MethodBase, int> index = new Dictionary<MethodBase, int>();
        static readonly List<MethodBase> methods = new List<MethodBase>();
        static readonly List<MethodBase> queue = new List<MethodBase>();
        static int failed;
        static string status = "nothing wrapped";

        // ---- what is noted: for each, the method's number plus one (minus that for its end, nought for a frame's beginning) and when
        const int Most = 3000000;
        static int[] what;
        static long[] when;
        static int count, frameAt, frames, kept;
        static long frameBegan;
        static double keepOver = 50.0;
        static volatile bool on;
        static int mainThread;
        static GameObject runner;

        public static string Do(string arg)
        {
            arg = (arg ?? "").Trim();
            string word = arg.Split(' ')[0], rest = arg.Length > word.Length ? arg.Substring(word.Length).Trim() : "";
            switch (word)
            {
                case "patch": return Patch(rest);
                case "status": return status + "; " + methods.Count + " wrapped, " + queue.Count + " to go, " + failed + " could not be";
                case "on":
                    if (what == null) { what = new int[Most]; when = new long[Most]; }
                    keepOver = rest.Length > 0 && double.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ms) ? ms : 50.0;
                    mainThread = Thread.CurrentThread.ManagedThreadId;
                    count = frameAt = frames = kept = 0;
                    frameBegan = Stopwatch.GetTimestamp();
                    Mark();
                    on = true;
                    return "noting the frames longer than " + keepOver + " ms";
                case "off": on = false; return "stopped: " + frames + " frames, " + kept + " kept, " + count + " things noted";
                case "take": return Take();
                case "unpatch":
                    on = false;
                    queue.Clear();
                    if (harmony != null) { try { harmony.UnpatchAll(Id); } catch (Exception ex) { return "could not unwrap: " + ex.Message; } }
                    index.Clear(); methods.Clear();
                    if (runner != null) UnityEngine.Object.Destroy(runner);
                    runner = null;
                    status = "nothing wrapped";
                    return "unwrapped";
            }
            return "patch NAMES, status, on MS, off, take, unpatch";
        }

        // ---------------------------------------------------------------- wrapping

        static string Patch(string names)
        {
            if (harmony == null) harmony = new Harmony(Id);
            var found = new List<MethodBase>();
            Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
            foreach (string one in names.Split(','))
            {
                string name = one.Trim();
                if (name.Length == 0) continue;
                if (name.StartsWith("ns:", StringComparison.Ordinal))
                {
                    string root = name.Substring(3);
                    // (the newest library that has classes of this namespace: a development build's reloaded copy)
                    Assembly newest = null;
                    foreach (Assembly a in all)
                    {
                        if (a.IsDynamic) continue;
                        foreach (Type t in TypesOf(a)) if (In(t, root)) { newest = a; break; }
                    }
                    if (newest != null) foreach (Type t in TypesOf(newest)) if (In(t, root)) Methods(t, found, 24);
                }
                else
                {
                    bool small = name.StartsWith("!", StringComparison.Ordinal);
                    if (small) name = name.Substring(1);
                    Type type = null;
                    for (int k = all.Length - 1; k >= 0 && type == null; k--) if (!all[k].IsDynamic) { try { type = all[k].GetType(name, false); } catch (Exception) { } }
                    if (type != null) Methods(type, found, small ? 1 : 24);
                }
            }
            foreach (MethodBase m in found) if (!index.ContainsKey(m) && !queue.Contains(m)) queue.Add(m);
            if (runner == null)
            {
                runner = new GameObject("Keystone: the tracer");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<Runner>();
            }
            status = "wrapping";
            return queue.Count + " methods to wrap";
        }

        static bool In(Type t, string root) => t.Namespace != null && (t.Namespace == root || t.Namespace.StartsWith(root + ".", StringComparison.Ordinal));

        static Type[] TypesOf(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return Array.FindAll(ex.Types, t => t != null); }
            catch (Exception) { return new Type[0]; }
        }

        static void Methods(Type t, List<MethodBase> found, int least)
        {
            if (t.ContainsGenericParameters || t.Namespace == "KeystoneHarmony" || t.Assembly == typeof(Harmony).Assembly) return;
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var list = new List<MethodBase>();
            try { list.AddRange(t.GetMethods(Own)); list.AddRange(t.GetConstructors(Own)); } catch (Exception) { return; }
            foreach (MethodBase m in list)
            {
                if (m.IsAbstract || m.ContainsGenericParameters || (m.IsStatic && m.IsConstructor)) continue;
                // (not what the compiler makes of a lambda or a local function: wrapping one of those, Harmony's wrapper failed the
                // runtime's own check as it compiled, and the game stopped on the spot, 2026-10-18)
                if (m.Name.IndexOf('<') >= 0) continue;
                try
                {
                    MethodBody body = m.GetMethodBody();
                    // (not the smallest, which are called most and do least: a field read or written)
                    if (body == null || body.GetILAsByteArray().Length < least) continue;
                }
                catch (Exception) { continue; }
                found.Add(m);
            }
        }

        static void WrapSome()
        {
            if (queue.Count == 0) return;
            var timer = Stopwatch.StartNew();
            var prefix = new HarmonyMethod(typeof(Trace).GetMethod(nameof(Enter), BindingFlags.Static | BindingFlags.NonPublic));
            var finalizer = new HarmonyMethod(typeof(Trace).GetMethod(nameof(Leave), BindingFlags.Static | BindingFlags.NonPublic));
            while (queue.Count > 0 && timer.ElapsedMilliseconds < 40)
            {
                MethodBase m = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                int k = methods.Count;
                index[m] = k;
                methods.Add(m);
                try { harmony.Patch(m, prefix: prefix, finalizer: finalizer); }
                catch (Exception) { failed++; }
            }
            if (queue.Count == 0) status = "wrapped";
        }

        // ---------------------------------------------------------------- noting

        static void Enter(MethodBase __originalMethod)
        {
            if (!on || count >= Most || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            if (!index.TryGetValue(__originalMethod, out int k)) return;
            what[count] = k + 1; when[count] = Stopwatch.GetTimestamp(); count++;
        }

        static void Leave(MethodBase __originalMethod)
        {
            if (!on || count >= Most || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            if (!index.TryGetValue(__originalMethod, out int k)) return;
            what[count] = -(k + 1); when[count] = Stopwatch.GetTimestamp(); count++;
        }

        /// <summary>A frame begins: the one before is kept only if it was slower than the limit.</summary>
        static void Mark()
        {
            long now = Stopwatch.GetTimestamp();
            if (frames > 0)
            {
                double ms = (now - frameBegan) * 1000.0 / Stopwatch.Frequency;
                if (ms < keepOver) count = frameAt;
                else
                {
                    kept++;
                    // (where it ended: the frames dropped after it are not to be counted into it)
                    if (count < Most) { what[count] = int.MinValue; when[count] = now; count++; }
                }
            }
            frames++;
            if (count < Most) { frameAt = count; what[count] = 0; when[count] = now; count++; }
            frameBegan = now;
        }

        static string Take()
        {
            if (what == null) return "nothing noted";
            bool was = on;
            on = false;
            string to = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".ksp-ai-bridge"), "trace.txt");
            using (var w = new StreamWriter(to))
            {
                w.WriteLine("# trace: " + count + " things noted, " + kept + " of " + frames + " frames kept (longer than " + keepOver + " ms), ticks a second " + Stopwatch.Frequency);
                for (int k = 0; k < methods.Count; k++)
                {
                    MethodBase m = methods[k];
                    w.WriteLine("m " + (k + 1) + " " + (m.DeclaringType != null ? m.DeclaringType.FullName : "?") + ":" + m.Name);
                }
                for (int k = 0; k < count; k++) w.WriteLine(what[k] + " " + when[k]);
            }
            on = was;
            return "written to " + to + ": " + count + " things, " + kept + " frames";
        }

        [DefaultExecutionOrder(-32000)]
        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (on) Mark();
                WrapSome();
            }
        }
    }
}
#endif
