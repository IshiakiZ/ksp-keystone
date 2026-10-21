using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("Keystone", 0, 8)]
#endif

namespace Keystone
{
    /// <summary>
    /// The base the other mods of this family stand on: one place where each says what it is and what
    /// can be set about it. In return it gets its settings kept from one run of the game to the next,
    /// a page in the one settings window they all share (see Host), and a few tools that every one of
    /// them would otherwise write again for itself.
    ///
    /// A mod uses it like this:
    ///
    ///     Mod mod = Kit.Register("Smooth Portraits", "0.1.0", "The crew's portraits, drawn as often as the game itself.");
    ///     Toggle on = mod.Toggle("on", "Smooth portraits", true);
    ///     Slider most = mod.Slider("most", "At most", 60f, 10f, 120f, " a second", 0);
    ///     ... if (on.Value) ... most.Value ...
    ///
    /// Nothing else is asked of it. A setting reads what was saved for it the moment it is made, and is
    /// saved again a second after the player changes it.
    ///
    /// The window opens on a list of every mod that has registered: its name, its version, a line on how
    /// it is doing (its Status, or else whether its settings are as installed) and a switch, which is the
    /// mod's own Toggle "on" (or whichever it names as its Switch). A click on a mod opens its page.
    /// </summary>
    public static class Kit
    {
        public const string Version = "0.9.0";

        static readonly List<Mod> mods = new List<Mod>();

        /// <summary>The mods that have said they are here, in the order they did.</summary>
        public static IList<Mod> Mods => mods;

        static Mod own;
        static Hotkey window;
        static Toggle online;

        /// <summary>Keystone's own few settings (the key that opens the window, looking online): kept like a mod's, in Keystone.cfg, and shown on Keystone's page in the list of mods.</summary>
        internal static Mod Own
        {
            get
            {
                if (own == null)
                {
                    own = new Mod("Keystone", Version, null);
                    window = own.Hotkey("window", "Opens this window", KeyCode.K, true, "The key that opens and closes this window, wherever you are in the game. Click, then press the key you want (with the game's modifier key held, if it is to be with it).");
                    online = own.Toggle("online", "Look for newer versions online", true,
                        "On as installed. About once a day, while this window is open, each mod's own address for its newest version is asked (the copy of its .version file its makers keep online, and its latest release on GitHub), one at a time, and the answers are kept in Keystone's folder. Nothing about you is sent; the sites see that this computer asked, as with any web page. Nothing is downloaded unless you press a mod's Update button.");
                    own.Panel = AllMods;
                }
                return own;
            }
        }

        static string allSaid = "";

        /// <summary>
        /// Keystone's page: every mod set for speed at once (each mod says which of its settings, and to what: Mod.Faster), or
        /// every mod's settings put back as installed. Each mod's page shows what was changed, and its own Defaults button puts
        /// that mod alone back.
        /// </summary>
        static void AllMods()
        {
            GUILayout.Space(6f);
            GUILayout.Label("<b>Every mod at once</b>", Host.Rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Set for speed", "Each mod's dearest settings set to what costs least for what is lost: the clouds drawn less finely, the sun's shafts off, the seaweed off, rocket smoke not hanging as long. Each mod's page shows what changed."), GUILayout.Width(130f)))
            {
                int n = 0, mods = 0;
                foreach (Mod m in Mods) if (m.HasFaster) { int c = m.SetForSpeed(); if (c > 0) { n += c; mods++; } }
                allSaid = n == 0 ? "Every mod was set for speed already." : n + (n == 1 ? " setting" : " settings") + " changed in " + mods + (mods == 1 ? " mod." : " mods.");
            }
            if (GUILayout.Button(new GUIContent("All as installed", "Every mod's settings put back as they come (not the keys, nor looking online: Keystone's own are left as they are)."), GUILayout.Width(130f)))
            {
                int n = 0;
                foreach (Mod m in Mods) { n += m.Changes; m.Reset(); }
                allSaid = n == 0 ? "Every mod's settings were as installed already." : n + (n == 1 ? " setting" : " settings") + " put back.";
            }
            GUILayout.EndHorizontal();
            if (allSaid.Length > 0) GUILayout.Label(allSaid, Host.Small);
            Sets();
        }

        // ---- (since 0.9.0) every mod's settings kept as a set under a name, and put back: to try a look and go back to one's own,
        // or to hand a look to someone else (the file, in Keystone's folder)
        static string setName = "", setSaid = "";
        static string[] setNames;
        static float setsLooked = -100f;

        static string SetsFolder => Folder + "Sets/";

        /// <summary>The sets kept, by name (looked at again every few seconds while the page is open: one may be dropped in by hand).</summary>
        static string[] SetNames()
        {
            if (setNames == null || Time.realtimeSinceStartup - setsLooked > 3f)
            {
                setsLooked = Time.realtimeSinceStartup;
                try
                {
                    setNames = System.IO.Directory.Exists(SetsFolder) ? System.IO.Directory.GetFiles(SetsFolder, "*.cfg") : new string[0];
                    for (int n = 0; n < setNames.Length; n++) setNames[n] = System.IO.Path.GetFileNameWithoutExtension(setNames[n]);
                    Array.Sort(setNames, StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception) { setNames = new string[0]; }
            }
            return setNames;
        }

        static void Sets()
        {
            GUILayout.Space(6f);
            GUILayout.Label(new GUIContent("<b>Sets of settings</b>", "Every mod's settings as they are now, kept under a name, to be put back later: to try a look and come back to your own, or to give a look to a friend (the file is in GameData/Keystone/PluginData/Sets/; one put there by hand shows here)."), Host.Rich);
            GUILayout.BeginHorizontal();
            setName = Host.Typed(Host.TypedName + "setname", setName, 160f, out bool entered, out bool _);
            bool keep = GUILayout.Button(new GUIContent("Keep as a set", "Every mod's settings as they are now kept under this name (one of the same name is written over)."), GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            if (keep || (entered && setName.Trim().Length > 0)) setSaid = Keep(setName);
            string[] names = SetNames();
            for (int n = 0; n < names.Length; n++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[n], Host.Plain, GUILayout.Width(Host.LabelWidth + 30f));
                if (GUILayout.Button(new GUIContent("Put back", "Every mod's settings as this set has them (a mod not in it, or a setting it does not name, is left as it is)."), GUILayout.Width(80f))) setSaid = PutBack(names[n]);
                GUILayout.EndHorizontal();
            }
            if (names.Length == 0) GUILayout.Label("No set kept yet.", Host.Small);
            if (setSaid.Length > 0) GUILayout.Label(setSaid, Host.Small);
        }

        /// <summary>Every mod's settings (not Keystone's own: its keys and looking online are this computer's) kept as a set under a name.</summary>
        internal static string Keep(string name)
        {
            string file = Clean(name);
            if (file.Length == 0) return "Give the set a name first.";
            try
            {
                var root = new ConfigNode();
                root.AddValue("name", (name ?? "").Trim());
                root.AddValue("made", DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
                int count = 0;
                lock (mods)
                    foreach (Mod m in mods)
                    {
                        ConfigNode one = root.AddNode("MOD");
                        one.AddValue("name", m.Name ?? "");
                        one.AddValue("version", m.Version ?? "");
                        foreach (Setting setting in m.Settings)
                        {
                            if (setting == null || setting is Hotkey || string.IsNullOrEmpty(setting.Key)) continue;
                            string value = setting.Write();
                            if (value == null) continue;
                            one.AddValue(setting.Key, value);
                            count++;
                        }
                    }
                System.IO.Directory.CreateDirectory(SetsFolder);
                root.Save(SetsFolder + file + ".cfg");
                setsLooked = -100f;
                return "Kept \"" + name.Trim() + "\": " + count + " settings of " + mods.Count + " mods.";
            }
            catch (Exception ex) { return "Could not keep it: " + ex.Message; }
        }

        /// <summary>Every mod's settings as a kept set has them.</summary>
        internal static string PutBack(string name)
        {
            try
            {
                ConfigNode root = ConfigNode.Load(SetsFolder + Clean(name) + ".cfg");
                if (root == null) return "No set called \"" + name + "\".";
                int changed = 0, mods = 0;
                foreach (ConfigNode one in root.GetNodes("MOD"))
                {
                    Mod m = Find(one.GetValue("name") ?? "");
                    if (m == null) continue;
                    int before = changed;
                    foreach (Setting setting in m.Settings)
                    {
                        if (setting is Hotkey) continue;
                        string value = one.GetValue(setting.Key);
                        if (value != null && setting.Apply(value)) changed++;
                    }
                    if (changed > before) mods++;
                }
                return changed == 0 ? "Every setting was as \"" + name + "\" has it already." : changed + (changed == 1 ? " setting" : " settings") + " changed in " + mods + (mods == 1 ? " mod" : " mods") + ", as \"" + name + "\" has them.";
            }
            catch (Exception ex) { return "Could not put it back: " + ex.Message; }
        }

#if DEV
        /// <summary>(Development build) Keep and PutBack, from outside; Drop deletes a kept set.</summary>
        public static string KeepSet(string name) => Keep(name);
        public static string PutBackSet(string name) => PutBack(name);
        public static string DropSet(string name)
        {
            string path = SetsFolder + Clean(name) + ".cfg";
            if (!System.IO.File.Exists(path)) return "no such set";
            System.IO.File.Delete(path); setsLooked = -100f;
            return "dropped";
        }
#endif

        /// <summary>A name made fit to be a file's.</summary>
        static string Clean(string name)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in (name ?? "").Trim()) b.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '_');
            return b.ToString().Trim();
        }

        /// <summary>The key that opens and closes the window.</summary>
        internal static Hotkey WindowKey { get { if (window == null) _ = Own; return window; } }

        /// <summary>Whether the player allows looking online (see Online).</summary>
        internal static Toggle OnlineSwitch { get { if (online == null) _ = Own; return online; } }

        /// <summary>Every mod and Keystone's own settings: for what looks through all of them (keys that clash, saving).</summary>
        internal static IEnumerable<Mod> All()
        {
            for (int n = 0; n < mods.Count; n++) yield return mods[n];
            yield return Own;
        }

        /// <summary>Where the settings are kept: a folder the game does not read as part of its own data, so writing there never makes it rebuild anything.</summary>
        public static string Folder => KSPUtil.ApplicationRootPath + "GameData/Keystone/PluginData/";

        /// <summary>
        /// A mod says it is here. (One that says so again takes the place of its old self: that happens when
        /// a development build swaps a new copy of a mod in while the game runs.)
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static Mod Register(string name, string version, string about = null)
        {
            var mod = new Mod(name, version, about);
            // (where it was called from: which library, and so which folder in GameData, is the mod's)
            try { mod.From = new System.Diagnostics.StackFrame(1, false).GetMethod()?.DeclaringType; }
            catch (Exception) { }
            lock (mods)
            {
                int at = mods.FindIndex(m => m.Name == name);
                if (at >= 0) mods[at] = mod;
                else mods.Add(mod);
            }
            Log("Keystone", name + " " + version + " is here");
            Listen();
            return mod;
        }

        // ---- (since 0.9.0) errors that come of a registered mod's own code, counted on its row in the window (see Host.Badges)
        static bool listening;

        static void Listen()
        {
            if (listening) return;
            listening = true;
            Application.logMessageReceivedThreaded += Heard;
        }

        /// <summary>
        /// Every error and exception the game logs (on any thread): put down to the registered mod whose code it came out of, the
        /// first frame of the stack that is in one of their namespaces (a mod's error passing through another's code is the
        /// first's). The player then sees on the mod's row that something went wrong, and what, without opening the log.
        /// </summary>
        static void Heard(string text, string trace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            Mod who = null;
            int first = int.MaxValue;
            lock (mods)
            {
                foreach (Mod m in mods)
                {
                    // (the game logs errors without their stack, as a rule: a mod of the family begins its own lines with its name in
                    // brackets, "[Natural Light] ...", and that says whose it is; an exception's stack says it of anything)
                    if (text != null && text.StartsWith("[" + m.Name + "]", StringComparison.Ordinal)) { who = m; break; }
                    if (string.IsNullOrEmpty(trace)) continue;
                    string ns = m.From != null ? m.From.Namespace : null;
                    if (string.IsNullOrEmpty(ns)) continue;
                    int at = trace.IndexOf(ns + ".", StringComparison.Ordinal);
                    if (at >= 0 && at < first) { first = at; who = m; }
                }
            }
            who?.NoteError(text);
        }

        public static Mod Find(string name) { lock (mods) return mods.Find(m => m.Name == name); }

        public static void Log(string who, string text) => Debug.Log("[" + who + "] " + text);
    }

    /// <summary>One mod, as the base knows it: its name, and the things that can be set about it.</summary>
    public sealed class Mod
    {
        public readonly string Name, Version, About;
        readonly List<Setting> settings = new List<Setting>();
        readonly ConfigNode saved;
        float saveAt = -1f;
        int failed;                    // writes that failed since the last change

        /// <summary>The code that registered it (its library is the mod's).</summary>
        internal Type From;

        /// <summary>(Since Keystone 0.9.) How many errors its own code has logged this run, and the last of them (its first line).</summary>
        public int Errors => errors;
        public string LastError { get { lock (errorGate) return lastError; } }
        int errors;
        string lastError = "";
        readonly object errorGate = new object();

        internal void NoteError(string text)
        {
            System.Threading.Interlocked.Increment(ref errors);
            string line = (text ?? "").Replace("\r", "").Split('\n')[0].Trim();
            if (line.Length > 160) line = line.Substring(0, 157) + "...";
            lock (errorGate) lastError = line;
        }

        public IList<Setting> Settings => settings;

        /// <summary>A key a mod uses that is not one of its settings (it cannot be changed in the window), for the list of every key.</summary>
        internal struct FixedKey { public KeyCode Code; public bool Modifier; public string Does; }
        readonly List<FixedKey> fixedKeys = new List<FixedKey>();
        internal List<FixedKey> FixedKeys { get { lock (fixedKeys) return new List<FixedKey>(fixedKeys); } }

        /// <summary>
        /// (Since Keystone 0.9.) A key this mod reads by itself, not one of its settings, with the game's modifier key held or
        /// not and what it does: so that the window's page of every mod's keys has it too, and checks it against the others and
        /// the game's. Call it from a method of its own, in a try: a Keystone older than 0.9.0 has no such call.
        /// </summary>
        public void Uses(KeyCode code, bool modifier, string does)
        {
            if (code == KeyCode.None) return;
            lock (fixedKeys) fixedKeys.Add(new FixedKey { Code = code, Modifier = modifier, Does = does ?? "" });
        }

        readonly List<KeyValuePair<Setting, string>> faster = new List<KeyValuePair<Setting, string>>();

        /// <summary>
        /// What one of this mod's settings is to be when the player asks for every mod to be set for speed (Keystone's page, "Every
        /// mod set for speed"): as the setting writes it to its file ("False", "0", "0.6"). Only the settings that cost the most
        /// and lose the least. (Since Keystone 0.9.)
        /// </summary>
        public void Faster(Setting setting, string value) { if (setting != null && !string.IsNullOrEmpty(value)) faster.Add(new KeyValuePair<Setting, string>(setting, value)); }

        /// <summary>This mod's settings set for speed, as it said with Faster. How many were changed.</summary>
        internal int SetForSpeed()
        {
            int n = 0;
            foreach (KeyValuePair<Setting, string> f in faster) if (f.Key.Apply(f.Value)) n++;
            return n;
        }

        internal bool HasFaster => faster.Count > 0;

        /// <summary>Something about this mod was changed by the player (or put back to what it was to begin with).</summary>
        public event Action Changed;

        /// <summary>More that the mod draws on its page itself, under its settings (with the game's own GUILayout calls): a list, a readout.</summary>
        public Action Panel;

        /// <summary>
        /// A few words on how the mod is doing just now, for its line in the list of mods: what it is doing ("2 engines
        /// are leaving smoke"), or why it cannot ("needs Volumetric Explosions"). Asked about once a frame while the list
        /// is showing, so keep it cheap: hand back words kept up to date elsewhere. Nothing, or empty: the list says
        /// whether the mod's settings are as installed. (Since Keystone 0.6.)
        /// </summary>
        public Func<string> Status;

        Toggle switchChosen;
        bool switchNamed;

        /// <summary>
        /// The setting the switch beside the mod in the list of mods is: the mod's own Toggle with the key "on", unless
        /// another is named here (or none: null). The switch is that setting itself, not a second one beside it, so
        /// flicking it in the list is flicking it on the mod's page. (Since Keystone 0.6.)
        /// </summary>
        public Toggle Switch
        {
            get
            {
                if (switchNamed) return switchChosen;
                for (int n = 0; n < settings.Count; n++)
                    if (settings[n] is Toggle toggle && toggle.Key == "on") return toggle;
                return null;
            }
            set { switchChosen = value; switchNamed = true; }
        }

        /// <summary>How many of its settings the player has changed from what they are when first installed (the switch left out).</summary>
        public int Changes
        {
            get
            {
                Toggle on = Switch;
                int changed = 0;
                for (int n = 0; n < settings.Count; n++)
                    if (settings[n] != on && settings[n].Key != null && !settings[n].AsInstalled) changed++;
                return changed;
            }
        }

        internal Mod(string name, string version, string about)
        {
            Name = name; Version = version; About = about;
            try
            {
                if (File.Exists(Path)) saved = ConfigNode.Load(Path);
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not read " + Path + ": " + ex.Message); }
        }

        string Path => Kit.Folder + Name.Replace(" ", "") + ".cfg";

        T Add<T>(T setting) where T : Setting
        {
            setting.Owner = this;
            string text = saved != null ? saved.GetValue(setting.Key) : null;
            if (text != null)
            {
                try { setting.Read(text); }
                catch (Exception) { }                    // (a value from some other version that this one cannot read: keep what it starts with)
            }
            settings.Add(setting);
            return setting;
        }

        /// <summary>Something that is on or off.</summary>
        public Toggle Toggle(string key, string label, bool value, string hint = null) => Add(new Toggle(key, label, hint, value));

        /// <summary>A number between two ends, shown with so many decimals and a unit after it.</summary>
        public Slider Slider(string key, string label, float value, float least, float most, string unit = "", int decimals = 1, string hint = null) =>
            Add(new Slider(key, label, hint, value, least, most, unit, decimals));

        /// <summary>One of a few named things.</summary>
        public Choice Choice(string key, string label, int value, string[] options, string hint = null) => Add(new Choice(key, label, hint, value, options));

        /// <summary>A colour.</summary>
        public Tint Tint(string key, string label, Color value, string hint = null) => Add(new Tint(key, label, hint, value));

        /// <summary>A line of words between settings, to head a group of them.</summary>
        public Heading Heading(string label) => Add(new Heading(label));

        /// <summary>
        /// A key, alone or with the game's modifier key held (Option on a Mac, Alt elsewhere), that the player can change
        /// on the mod's page. Ask its Pressed (or Held) in Update. (Since Keystone 0.6.)
        /// </summary>
        public Hotkey Hotkey(string key, string label, KeyCode value, bool modifier = true, string hint = null) => Add(new Hotkey(key, label, hint, value, modifier));

        /// <summary>Other things worth keeping that are not settings with a place in the window (a list the mod draws for itself, say): kept under a name, as text.</summary>
        public string Kept(string key) => keeps.TryGetValue(key, out string text) ? text : saved != null ? saved.GetValue(key) : null;

        readonly Dictionary<string, string> keeps = new Dictionary<string, string>();

        /// <summary>Keep some text under a name (a name without spaces). Nothing for the text takes it out of the file again.</summary>
        public void Keep(string key, string text)
        {
            keeps[key] = text;
            Touched(false);
        }

        internal void Touched(bool tell = true)
        {
            saveAt = Time.realtimeSinceStartup + 1f;
            failed = 0;
            if (tell) Changed?.Invoke();
        }

        /// <summary>Everything back to what it is when first installed.</summary>
        public void Reset()
        {
            foreach (Setting setting in settings) setting.Reset();
            Touched();
        }

        internal void SaveIfDue(bool now = false)
        {
            if (saveAt < 0f || (!now && Time.realtimeSinceStartup < saveAt)) return;
            saveAt = -1f;
            try
            {
                var node = new ConfigNode(Name.Replace(" ", ""));
                node.AddValue("version", Version);
                foreach (Setting setting in settings)
                    if (setting.Key != null) node.AddValue(setting.Key, setting.Write());
                // (what the mod keeps for itself: what it has handed over this time, and whatever was in the file before that it has not)
                if (saved != null)
                    foreach (ConfigNode.Value value in saved.values)
                        if (value.name != "version" && !node.HasValue(value.name) && !keeps.ContainsKey(value.name)) node.AddValue(value.name, value.value);
                foreach (KeyValuePair<string, string> keep in keeps)
                    if (keep.Value != null) node.AddValue(keep.Key, keep.Value);
                Directory.CreateDirectory(Kit.Folder);
                node.Save(Path);
            }
            catch (Exception ex)
            {
                // (on Windows a file that another program has open just then, a virus scanner's or a backup's, cannot be written:
                // tried again a few times, two seconds apart, before it is left until the next change)
                if (!now && ++failed < 5) saveAt = Time.realtimeSinceStartup + 2f;
                Kit.Log("Keystone", "could not write " + Path + ": " + ex.Message + (saveAt > 0f ? " (trying again)" : ""));
            }
        }
    }

    /// <summary>
    /// Getting at the parts of the game that it keeps to itself. Each is looked up by name once; if a later
    /// version of the game no longer has it, that is said in the log once and the caller is given nothing,
    /// so that a mod can carry on without that one thing instead of failing every frame.
    /// </summary>
    public static class Reach
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static FieldInfo Field(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo found = t.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (found != null) return found;
            }
            Kit.Log("Keystone", "this version of the game has no " + type.Name + "." + name);
            return null;
        }

        public static MethodInfo Method(Type type, string name, params Type[] takes)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo found = t.GetMethod(name, Any | BindingFlags.DeclaredOnly, null, takes, null);
                if (found != null) return found;
            }
            Kit.Log("Keystone", "this version of the game has no " + type.Name + "." + name + "()");
            return null;
        }

        /// <summary>A method of the game's own, as something that can be called quickly and often. Nothing if it is not there.</summary>
        public static T Call<T>(Type type, string name, params Type[] takes) where T : class
        {
            MethodInfo method = Method(type, name, takes);
            if (method == null) return null;
            try { return Delegate.CreateDelegate(typeof(T), method) as T; }
            catch (Exception ex)
            {
                Kit.Log("Keystone", type.Name + "." + name + "() is not what it was: " + ex.Message);
                return null;
            }
        }
    }
}
