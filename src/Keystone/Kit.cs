using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("Keystone", 0, 4)]
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
    /// </summary>
    public static class Kit
    {
        public const string Version = "0.4.0";

        static readonly List<Mod> mods = new List<Mod>();

        /// <summary>The mods that have said they are here, in the order they did.</summary>
        public static IList<Mod> Mods => mods;

        /// <summary>Where the settings are kept: a folder the game does not read as part of its own data, so writing there never makes it rebuild anything.</summary>
        public static string Folder => KSPUtil.ApplicationRootPath + "GameData/Keystone/PluginData/";

        /// <summary>
        /// A mod says it is here. (One that says so again takes the place of its old self: that happens when
        /// a development build swaps a new copy of a mod in while the game runs.)
        /// </summary>
        public static Mod Register(string name, string version, string about = null)
        {
            var mod = new Mod(name, version, about);
            int at = mods.FindIndex(m => m.Name == name);
            if (at >= 0) mods[at] = mod;
            else mods.Add(mod);
            Log("Keystone", name + " " + version + " is here");
            return mod;
        }

        public static Mod Find(string name) => mods.Find(m => m.Name == name);

        public static void Log(string who, string text) => Debug.Log("[" + who + "] " + text);
    }

    /// <summary>One mod, as the base knows it: its name, and the things that can be set about it.</summary>
    public sealed class Mod
    {
        public readonly string Name, Version, About;
        readonly List<Setting> settings = new List<Setting>();
        readonly ConfigNode saved;
        float saveAt = -1f;

        public IList<Setting> Settings => settings;

        /// <summary>Something about this mod was changed by the player (or put back to what it was to begin with).</summary>
        public event Action Changed;

        /// <summary>More that the mod draws on its page itself, under its settings (with the game's own GUILayout calls): a list, a readout.</summary>
        public Action Panel;

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
            catch (Exception ex) { Kit.Log("Keystone", "could not write " + Path + ": " + ex.Message); }
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
