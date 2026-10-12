using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// One mod as the window lists it: every mod installed is one of these, Keystone's and everyone else's, with
    /// whatever can be known of it without asking anyone (what CKAN wrote down when it installed it, the mod's own
    /// .version file, its libraries, its folders) and, where the player has allowed it, what was learnt online.
    /// </summary>
    public sealed class Listed
    {
        /// <summary>How it is known: its CKAN identifier, its folder, or its name.</summary>
        public string Id = "";
        public string Name = "", Version = "", Author = "", About = "", Licence = "";
        /// <summary>The name it registered with Keystone under, if it is one of Keystone's mods (with a page of settings, and a switch).</summary>
        public string ModName;
        /// <summary>Keystone's mod, if it is one (with a page of settings, and a switch).</summary>
        public Mod Mod => ModName != null ? Kit.Find(ModName) : null;
        /// <summary>Keystone itself (its own settings are on its page).</summary>
        public bool Base;
        /// <summary>Something other mods are built on, and not a thing to play with in itself (CKAN says so).</summary>
        public bool Library;
        /// <summary>Installed by CKAN, and (AsDependency) only because another mod needed it.</summary>
        public bool ByCkan, AsDependency;
        /// <summary>Something wrong, or worth saying: "its library did not load", "configs only".</summary>
        public string Note = "";
        /// <summary>The versions of the game it says it is made for (either may be empty).</summary>
        public string KspMin = "", KspMax = "";
        /// <summary>Its addresses, each as {what it is, the address}: its home page, its source, where to report a fault.</summary>
        public readonly List<string[]> Links = new List<string[]>();
        /// <summary>Its folders in GameData ("" for a library lying in GameData itself).</summary>
        public readonly List<string> Folders = new List<string>();
        /// <summary>Its libraries the game has loaded, each with the version it declares.</summary>
        public readonly List<string> Libraries = new List<string>();
        /// <summary>What it needs, and whether that is installed.</summary>
        public readonly List<Need> Needs = new List<Need>();
        /// <summary>The mods installed that need it.</summary>
        public readonly List<Listed> NeededBy = new List<Listed>();
        /// <summary>Where a newer version could be learnt of: the copy of its .version file its makers keep online, and its GitHub repository ("owner/name").</summary>
        public string Remote = "", GitHub = "";
        /// <summary>What was learnt online (see Online): the newest version, a few lines of what is new in it, its page, and when this was learnt.</summary>
        public string Latest = "", LatestNotes = "", LatestPage = "", LatestFrom = "";
        public DateTime LatestAt;
        /// <summary>Its libraries as the game has them (for whoever wants to know more: what they cost, for one).</summary>
        public readonly List<Assembly> Assemblies = new List<Assembly>();

        /// <summary>The names it answers to when another mod says it needs something: CKAN identifiers, what it provides, its libraries' names.</summary>
        public readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal bool HasVersionFile;

        /// <summary>A newer version is known to be out (only ever after an online look the player allowed).</summary>
        public bool Behind => Latest.Length > 0 && Listing.Newer(Latest, Version) > 0;

        /// <summary>Whether something it needs is missing.</summary>
        public bool Missing
        {
            get
            {
                foreach (Need need in Needs) if (need.Met == null && !need.Game) return true;
                return false;
            }
        }

        /// <summary>The game's version is outside what it says it is made for, in words (nothing if it is within, or it does not say).</summary>
        public string OutOfRange
        {
            get
            {
                string game = Listing.GameVersion;
                if (KspMax.Length > 0 && Listing.Newer(game, KspMax, true) > 0) return "made for KSP " + (KspMin.Length > 0 && KspMin != KspMax ? KspMin + " to " : "") + KspMax;
                if (KspMin.Length > 0 && Listing.Newer(KspMin, game, true) > 0) return "needs KSP " + KspMin + " or later";
                return "";
            }
        }
    }

    /// <summary>Something a mod needs: its name, the version asked for, and the mod installed that answers it (nothing: it is missing).</summary>
    public sealed class Need
    {
        public string Name = "", Version = "";
        public Listed Met;
        /// <summary>Part of the game itself (an expansion), which is installed.</summary>
        public bool Game;
    }

    /// <summary>
    /// Every mod installed, found in this order: what CKAN wrote down when it installed mods (its registry, in the game's
    /// CKAN folder, if the player uses CKAN); Keystone's own mods; the .version files in GameData (the form KSP-AVC
    /// reads); the libraries the game has loaded; and whatever folders are left. Each thing found is added to the mod
    /// that owns its file or its folder. Nothing here is fetched from anywhere.
    /// </summary>
    public static class Listing
    {
        static List<Listed> all;
        static int madeWith = -1;
        static string game, gameVersion;

        /// <summary>Whether the player uses CKAN for this game (its registry is in the game's folder).</summary>
        public static bool UsesCkan { get; private set; }

        /// <summary>Every mod installed, Keystone's first and then the rest, each group by name. Made the first time it is asked for, and again when a mod registers later.</summary>
        public static IList<Listed> All
        {
            get
            {
                if (all == null || madeWith != Kit.Mods.Count)
                {
                    madeWith = Kit.Mods.Count; all = Make();
#if DEV
                    if (Unlisted.Count > 0) all.RemoveAll(one => Unlisted.Contains(one.Name));
#endif
                }
                return all;
            }
        }

#if DEV
        /// <summary>For the development build: mods left out of the list by name, for a picture of the window that is to show only some of what is installed (see Host.Unlist).</summary>
        public static readonly HashSet<string> Unlisted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#endif

        /// <summary>Look again (a development build has swapped a mod in; the player asked).</summary>
        public static void Again() => all = null;

        /// <summary>The mod a library belongs to (nothing for the game's own).</summary>
        public static Listed Of(Assembly assembly)
        {
            foreach (Listed one in All) if (one.Assemblies.Contains(assembly)) return one;
            return null;
        }

        /// <summary>The game and its expansions, with their versions.</summary>
        public static string Game => game ?? (game = TheGame());

        /// <summary>The game's own version, as three numbers.</summary>
        public static string GameVersion
        {
            get
            {
                if (gameVersion == null)
                {
                    try { gameVersion = Versioning.version_major + "." + Versioning.version_minor + "." + Versioning.Revision; }
                    catch (Exception) { gameVersion = "1.12.5"; }
                }
                return gameVersion;
            }
        }

        static readonly string[][] Expansions = { new[] { "MakingHistory", "Making History", "MakingHistory-DLC" }, new[] { "Serenity", "Breaking Ground", "BreakingGround-DLC" } };

        static string TheGame()
        {
            string said = "Kerbal Space Program " + GameVersion;
            try
            {
                foreach (string[] one in Expansions)
                    if (global::Expansions.ExpansionsLoader.IsExpansionInstalled(one[0]))
                        said += ", " + one[1] + " " + global::Expansions.ExpansionsLoader.GetExpansionVersion(one[0]);
            }
            catch (Exception) { }
            return said;
        }

        static bool ExpansionInstalled(string ckanName)
        {
            try
            {
                foreach (string[] one in Expansions)
                    if (string.Equals(one[2], ckanName, StringComparison.OrdinalIgnoreCase)) return global::Expansions.ExpansionsLoader.IsExpansionInstalled(one[0]);
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>A path written one way, for comparing: whole, with forward slashes, no slash at the end.</summary>
        static string Plain(string path)
        {
            try { path = Path.GetFullPath(path); } catch (Exception) { }
            return path.Replace('\\', '/').TrimEnd('/');
        }

        // ---------------------------------------------------------------- making the list

        sealed class Making
        {
            public readonly List<Listed> List = new List<Listed>();
            public readonly Dictionary<string, Listed> ByFile = new Dictionary<string, Listed>(StringComparer.OrdinalIgnoreCase);     // "GameData/x/y.dll"
            public readonly Dictionary<string, Listed> ByFolder = new Dictionary<string, Listed>(StringComparer.OrdinalIgnoreCase);   // "x"
            public string Data, Root;

            public void Own(Listed one, string folder)
            {
                if (!one.Folders.Contains(folder)) one.Folders.Add(folder);
                if (folder.Length > 0 && !ByFolder.ContainsKey(folder)) ByFolder[folder] = one;
            }

            /// <summary>The mod a file in GameData belongs to: whoever installed that very file, or whoever has its folder.</summary>
            public Listed Owner(string relative, string folder)
            {
                if (ByFile.TryGetValue(relative, out Listed one)) return one;
                return folder.Length > 0 && ByFolder.TryGetValue(folder, out one) ? one : null;
            }
        }

        static List<Listed> Make()
        {
            var m = new Making { Root = Plain(KSPUtil.ApplicationRootPath) };
            m.Data = m.Root + "/GameData";
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { FromCkan(m); } catch (Exception ex) { Kit.Log("Keystone", "could not read CKAN's registry: " + ex.Message); }
            try { FromKeystone(m); } catch (Exception ex) { Kit.Log("Keystone", "could not list Keystone's mods: " + ex.Message); }
            try { FromVersionFiles(m); } catch (Exception ex) { Kit.Log("Keystone", "could not read the .version files: " + ex.Message); }
            try { FromLibraries(m); } catch (Exception ex) { Kit.Log("Keystone", "could not list the libraries: " + ex.Message); }
            try { FromFolders(m); } catch (Exception ex) { Kit.Log("Keystone", "could not list GameData's folders: " + ex.Message); }
            try { Link(m.List); } catch (Exception ex) { Kit.Log("Keystone", "could not work out what needs what: " + ex.Message); }
            try { Online.Remembered(m.List); } catch (Exception ex) { Kit.Log("Keystone", "could not read what was learnt online: " + ex.Message); }
            // (Keystone's own mods first, Keystone itself after them, then everyone else; each group by name)
            m.List.Sort((x, y) =>
            {
                int gx = x.ModName != null ? 0 : x.Base ? 1 : 2, gy = y.ModName != null ? 0 : y.Base ? 1 : 2;
                return gx != gy ? gx.CompareTo(gy) : string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });
            Kit.Log("Keystone", "found " + m.List.Count + " mods installed in " + timer.ElapsedMilliseconds + " ms");
            return m.List;
        }

        static string Words(object said)
        {
            if (said is string text) return text.Trim();
            if (said is List<object> many)
            {
                var all = new List<string>();
                foreach (object one in many) if (one is string s && s.Trim().Length > 0) all.Add(s.Trim());
                return string.Join(", ", all.ToArray());
            }
            return "";
        }

        static string Get(Dictionary<string, object> map, string key) => map != null && map.TryGetValue(key, out object value) ? Words(value) : "";

        /// <summary>A version as a mod gives it, made fit to show: without CKAN's "epoch" ("1:") or a "v" in front.</summary>
        static string Tidy(string version)
        {
            version = (version ?? "").Trim();
            int colon = version.IndexOf(':');
            if (colon > 0 && colon < 4 && int.TryParse(version.Substring(0, colon), out _)) version = version.Substring(colon + 1);
            if (version.Length > 1 && (version[0] == 'v' || version[0] == 'V') && char.IsDigit(version[1])) version = version.Substring(1);
            return version;
        }

        static readonly string[][] LinkWords =
        {
            new[] { "homepage", "Home page" }, new[] { "repository", "Source" }, new[] { "bugtracker", "Faults" }, new[] { "manual", "Manual" },
            new[] { "spacedock", "SpaceDock" }, new[] { "curse", "CurseForge" }, new[] { "discussions", "Discussions" }, new[] { "store", "Store" },
        };

        static void AddLink(Listed one, string what, string address)
        {
            address = (address ?? "").Trim();
            if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return;
            foreach (string[] had in one.Links) if (had[1] == address || had[0] == what) return;
            one.Links.Add(new[] { what, address });
        }

        /// <summary>"owner/name" from an address on GitHub, or nothing.</summary>
        internal static string GitHubOf(string address)
        {
            const string Site = "github.com/";
            int at = (address ?? "").IndexOf(Site, StringComparison.OrdinalIgnoreCase);
            if (at < 0 || address.IndexOf("raw.githubusercontent", StringComparison.OrdinalIgnoreCase) >= 0) return "";
            string[] parts = address.Substring(at + Site.Length).Split(new[] { '/', '?', '#' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return "";
            string name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1].Substring(0, parts[1].Length - 4) : parts[1];
            return parts[0] + "/" + name;
        }

        /// <summary>What CKAN keeps about the mods it installed: names, what each is, who made it, its licence, its addresses, what it needs, and every file it put in.</summary>
        static void FromCkan(Making m)
        {
            string path = m.Root + "/CKAN/registry.json";
            UsesCkan = File.Exists(path);
            if (!UsesCkan) return;
            int at = 0;
            if (!(Json.Read(File.ReadAllText(path), ref at) is Dictionary<string, object> registry)) return;
            if (!(registry.TryGetValue("installed_modules", out object listed) && listed is Dictionary<string, object> modules)) return;
            foreach (KeyValuePair<string, object> pair in modules)
            {
                if (!(pair.Value is Dictionary<string, object> installed)) continue;
                installed.TryGetValue("source_module", out object source);
                var module = source as Dictionary<string, object>;
                // (the expansions are part of the game, and are shown with it)
                if (Get(module, "kind") == "dlc") continue;
                var one = new Listed { Id = pair.Key, ByCkan = true };
                one.Names.Add(pair.Key);
                one.Name = Get(module, "name");
                if (one.Name.Length == 0) one.Name = pair.Key;
                one.Version = Tidy(Get(module, "version"));
                one.Author = Get(module, "author");
                one.About = Get(module, "abstract");
                one.Licence = Get(module, "license");
                one.AsDependency = installed.TryGetValue("auto_installed", out object auto) && auto is string a && a == "true";
                string ksp = Get(module, "ksp_version");
                one.KspMin = ksp.Length > 0 ? ksp : Get(module, "ksp_version_min");
                one.KspMax = ksp.Length > 0 ? ksp : Get(module, "ksp_version_max");
                if (module != null && module.TryGetValue("tags", out object tags) && tags is List<object> tagList)
                    foreach (object tag in tagList) if (tag is string t && t == "library") one.Library = true;
                if (module != null && module.TryGetValue("provides", out object provides) && provides is List<object> provided)
                    foreach (object name in provided) if (name is string p) one.Names.Add(p);
                if (module != null && module.TryGetValue("resources", out object resources) && resources is Dictionary<string, object> links)
                {
                    foreach (string[] words in LinkWords) AddLink(one, words[1], Get(links, words[0]));
                    one.Remote = Get(links, "remote-avc");
                    one.GitHub = GitHubOf(Get(links, "repository"));
                }
                if (module != null && module.TryGetValue("depends", out object depends) && depends is List<object> needs)
                    foreach (object need in needs)
                    {
                        if (!(need is Dictionary<string, object> said)) continue;
                        if (said.TryGetValue("any_of", out object choices) && choices is List<object> any)
                        {
                            var names = new List<string>();
                            foreach (object choice in any) if (choice is Dictionary<string, object> c && Get(c, "name").Length > 0) names.Add(Get(c, "name"));
                            if (names.Count > 0) one.Needs.Add(new Need { Name = string.Join("|", names.ToArray()) });
                            continue;
                        }
                        string version = Get(said, "version");
                        if (version.Length == 0 && Get(said, "min_version").Length > 0) version = Get(said, "min_version") + " or later";
                        if (Get(said, "name").Length > 0) one.Needs.Add(new Need { Name = Get(said, "name"), Version = Tidy(version) });
                    }
                // Its files: each is its, and so (unless someone else has it already) is each folder in GameData they lie in.
                if (installed.TryGetValue("installed_files", out object files) && files is Dictionary<string, object> fileList)
                    foreach (string file in fileList.Keys)
                    {
                        string relative = file.Replace('\\', '/');
                        if (!relative.StartsWith("GameData/", StringComparison.OrdinalIgnoreCase)) continue;
                        m.ByFile[relative] = one;
                        string inside = relative.Substring("GameData/".Length);
                        int cut = inside.IndexOf('/');
                        string folder = cut < 0 ? inside : inside.Substring(0, cut);
                        bool isFolder = cut < 0 && Directory.Exists(m.Data + "/" + folder);
                        if (cut < 0 && !isFolder) continue;                                  // (a library lying in GameData itself)
                        if (Directory.Exists(m.Data + "/" + folder)) m.Own(one, folder);
                    }
                m.List.Add(one);
            }
        }

        /// <summary>Whether a library is Keystone, one of its companions, or a mod that stands on Keystone (which says so, to be loaded after it).</summary>
        static bool Family(AssemblyLoader.LoadedAssembly a)
        {
            if (a.dllName.StartsWith("Keystone", StringComparison.Ordinal)) return true;
            if (a.dependencies != null)
                foreach (AssemblyLoader.AssemblyDependency need in a.dependencies)
                    if (need != null && need.name == "Keystone") return true;
            return false;
        }

        static IEnumerable<AssemblyLoader.LoadedAssembly> Loaded(Making m)
        {
            foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
                if (a != null && a.assembly != null && !string.IsNullOrEmpty(a.path) && Plain(a.path).StartsWith(m.Data + "/", StringComparison.OrdinalIgnoreCase)) yield return a;
        }

        static string Relative(Making m, string path, out string folder)
        {
            string inside = Plain(path).Substring(m.Data.Length + 1);
            int cut = inside.IndexOf('/');
            folder = cut < 0 ? "" : inside.Substring(0, cut);
            return "GameData/" + inside;
        }

        /// <summary>Keystone itself, and each mod that has registered with it: where each is installed is found from its library.</summary>
        static void FromKeystone(Making m)
        {
            var family = new List<AssemblyLoader.LoadedAssembly>();
            foreach (AssemblyLoader.LoadedAssembly a in Loaded(m)) if (Family(a)) family.Add(a);

            Listed Entry(string folder, string name)
            {
                // (CKAN may have installed it, and knows more of it then: its maker, its licence, its addresses)
                Listed one = folder != null && m.ByFolder.TryGetValue(folder, out Listed known) ? known : null;
                if (one == null)
                {
                    one = new Listed { Id = folder ?? name, Name = name };
                    m.List.Add(one);
                }
                if (folder != null) m.Own(one, folder);
                return one;
            }

            // Keystone itself (and its companions, which lie in its folder)
            Listed keystone = Entry("Keystone", "Keystone");
            keystone.Base = true; keystone.Library = true; keystone.Name = "Keystone"; keystone.Version = Kit.Version;
            keystone.About = "The base the mods with settings here stand on: this window, their kept settings, keys, a lens and a film.";
            keystone.Names.Add("Keystone");

            foreach (Mod mod in Kit.Mods)
            {
                // Its library: the one it registered from (a release install), or else the one in GameData named as its code is (a development build, whose mods are all in one library loaded from elsewhere).
                AssemblyLoader.LoadedAssembly own = null;
                if (mod.From != null)
                {
                    foreach (AssemblyLoader.LoadedAssembly a in family) if (a.assembly == mod.From.Assembly) own = a;
                    string space = (mod.From.Namespace ?? "").Split('.')[0];
                    if (own == null) foreach (AssemblyLoader.LoadedAssembly a in family) if (a.dllName == space || a.name == space) own = a;
                }
                string squeezed = mod.Name.Replace(" ", "");
                if (own == null) foreach (AssemblyLoader.LoadedAssembly a in family) if (string.Equals(a.dllName, squeezed, StringComparison.OrdinalIgnoreCase)) own = a;
                string folder = null;
                if (own != null) Relative(m, own.path, out folder);
                Listed one = Entry(folder, mod.Name);
                one.ModName = mod.Name; one.Name = mod.Name; one.Version = mod.Version;
                if (one.About.Length == 0) one.About = mod.About ?? "";
                one.Names.Add(squeezed);
                if (own != null) { one.Names.Add(own.name); one.Names.Add(own.dllName); }
            }
            // Keystone's companions, and anything else of the family that has no mod registered for it, go with Keystone or with their folder's mod.
            foreach (AssemblyLoader.LoadedAssembly a in family)
            {
                string relative = Relative(m, a.path, out string folder);
                Listed one = m.Owner(relative, folder) ?? (folder.Length == 0 || folder == "Keystone" ? keystone : null);
                if (one == null)
                {
                    // (a library that stands on Keystone with no mod registered for it here: its folder is a mod of its own)
                    one = new Listed { Id = folder, Name = Spelt(folder) };
                    m.List.Add(one);
                    m.Own(one, folder);
                }
                Library(one, a);
                if (one.Version.Length == 0) one.Version = VersionOf(a);
            }
        }

        /// <summary>A name made from a folder's or a library's: "000_Harmony" as "Harmony", "VolumetricExplosions" as "Volumetric Explosions".</summary>
        internal static string Spelt(string name)
        {
            name = (name ?? "").Trim();
            int start = 0;
            while (start < name.Length && (char.IsDigit(name[start]) || name[start] == '_')) start++;
            if (start > 0 && start < name.Length) name = name.Substring(start);
            var s = new StringBuilder(name.Length + 6);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_') { s.Append(' '); continue; }
                bool upper = char.IsUpper(c);
                // (a capital after a small letter begins a word; so does the last of a run of capitals before a small letter: "AIBridge")
                if (i > 0 && upper && (char.IsLower(name[i - 1]) || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])))) s.Append(' ');
                s.Append(c);
            }
            return s.ToString().Replace("  ", " ").Trim();
        }

        static void Library(Listed one, AssemblyLoader.LoadedAssembly a)
        {
            if (one.Assemblies.Contains(a.assembly)) return;
            one.Assemblies.Add(a.assembly);
            string version = VersionOf(a);
            one.Libraries.Add(a.dllName + (version.Length > 0 ? " " + version : ""));
            one.Names.Add(a.name); one.Names.Add(a.dllName);
            // (what Keystone's companions need is theirs, not Keystone's: each is loaded only where what it needs is installed)
            if (one.Base && a.dllName != "Keystone") return;
            if (a.dependencies != null)
                foreach (AssemblyLoader.AssemblyDependency need in a.dependencies)
                {
                    if (need == null || string.IsNullOrEmpty(need.name)) continue;
                    bool had = false;
                    foreach (Need n in one.Needs) if (string.Equals(n.Name, need.name, StringComparison.OrdinalIgnoreCase)) had = true;
                    if (!had) one.Needs.Add(new Need { Name = need.name, Version = need.versionMajor != 0 || need.versionMinor != 0 ? need.versionMajor + "." + need.versionMinor + " or later" : "" });
                }
        }

        /// <summary>Every .version file in GameData (the game's own folders left out): the mod's name, version, what game it is for, and where its makers keep it online.</summary>
        static void FromVersionFiles(Making m)
        {
            foreach (string path in Directory.GetDirectories(m.Data))
            {
                string top = Path.GetFileName(path);
                if (top == "Squad" || top == "SquadExpansion") continue;
                foreach (string file in Files(path, ".version"))
                {
                    int at = 0;
                    Dictionary<string, object> said;
                    try { said = Json.Read(File.ReadAllText(file), ref at) as Dictionary<string, object>; }
                    catch (Exception) { continue; }
                    if (said == null) continue;
                    string relative = Relative(m, file, out string folder);
                    Listed one = m.Owner(relative, folder);
                    if (one != null && one.HasVersionFile && !m.ByFile.ContainsKey(relative)) one = null;      // (a second mod in the same folder)
                    if (one == null)
                    {
                        one = new Listed { Id = Get(said, "NAME").Length > 0 ? Get(said, "NAME") : folder };
                        one.Name = one.Id;
                        m.List.Add(one);
                        m.Own(one, folder);
                    }
                    one.HasVersionFile = true;
                    string version = VersionText(said, "VERSION");
                    if (one.Version.Length == 0) one.Version = Tidy(version);
                    if (Get(said, "NAME").Length > 0) one.Names.Add(Get(said, "NAME"));
                    string ksp = VersionText(said, "KSP_VERSION");
                    if (one.KspMin.Length == 0) one.KspMin = VersionText(said, "KSP_VERSION_MIN").Length > 0 ? VersionText(said, "KSP_VERSION_MIN") : ksp;
                    if (one.KspMax.Length == 0) one.KspMax = VersionText(said, "KSP_VERSION_MAX").Length > 0 ? VersionText(said, "KSP_VERSION_MAX") : ksp;
                    if (one.Remote.Length == 0) one.Remote = Get(said, "URL");
                    if (said.TryGetValue("GITHUB", out object hub) && hub is Dictionary<string, object> github && Get(github, "USERNAME").Length > 0 && Get(github, "REPOSITORY").Length > 0)
                    {
                        string repository = Get(github, "USERNAME") + "/" + Get(github, "REPOSITORY");
                        if (one.GitHub.Length == 0) one.GitHub = repository;
                        AddLink(one, "Source", "https://github.com/" + repository);
                    }
                    AddLink(one, "Download page", Get(said, "DOWNLOAD"));
                    AddLink(one, "Changes", Get(said, "CHANGE_LOG_URL"));
                    if (one.GitHub.Length == 0) one.GitHub = GitHubOf(Get(said, "DOWNLOAD"));
                }
            }
        }

        /// <summary>A version in a .version file: text, or MAJOR, MINOR, PATCH and BUILD.</summary>
        static string VersionText(Dictionary<string, object> said, string key)
        {
            if (!said.TryGetValue(key, out object value)) return "";
            if (value is string text) return text.Trim();
            if (!(value is Dictionary<string, object> parts)) return "";
            string major = Get(parts, "MAJOR"), minor = Get(parts, "MINOR"), patch = Get(parts, "PATCH"), build = Get(parts, "BUILD");
            if (major.Length == 0) return "";
            return major + "." + (minor.Length > 0 ? minor : "0") + (patch.Length > 0 ? "." + patch : "") + (build.Length > 0 && build != "0" ? "." + build : "");
        }

        /// <summary>The libraries the game has loaded from GameData, each added to the mod whose file or folder it is.</summary>
        static void FromLibraries(Making m)
        {
            foreach (AssemblyLoader.LoadedAssembly a in Loaded(m))
            {
                if (Family(a)) continue;
                string relative = Relative(m, a.path, out string folder);
                if (folder == "Squad" || folder == "SquadExpansion") continue;           // (the game's own)
                Listed one = m.Owner(relative, folder);
                if (one == null)
                {
                    one = new Listed { Id = folder.Length > 0 ? folder : a.dllName, Name = Spelt(folder.Length > 0 && !string.Equals(a.dllName, folder, StringComparison.OrdinalIgnoreCase) ? folder : a.name) };
                    m.List.Add(one);
                    m.Own(one, folder);
                }
                Library(one, a);
                if (one.Version.Length == 0 && (one.Libraries.Count == 1 || string.Equals(a.dllName, folder, StringComparison.OrdinalIgnoreCase))) one.Version = VersionOf(a);
            }
        }

        /// <summary>Folders in GameData that nothing so far has owned: a mod of configs and pictures, or one whose library did not load.</summary>
        static void FromFolders(Making m)
        {
            foreach (string path in Directory.GetDirectories(m.Data))
            {
                string folder = Path.GetFileName(path);
                if (folder == "Squad" || folder == "SquadExpansion" || m.ByFolder.ContainsKey(folder)) continue;
                var one = new Listed { Id = folder, Name = Spelt(folder) };
                if (Files(path, ".dll", true).Count > 0) one.Note = "its library did not load (KSP.log says why)";
                else one.Note = Files(path, ".cfg").Count > 0 ? "configs only" : "no library or configs";
                m.List.Add(one);
                m.Own(one, folder);
            }
            // (and a library CKAN installed that did not load: it says so on that mod)
            foreach (Listed one in m.List)
                foreach (KeyValuePair<string, Listed> file in m.ByFile)
                    if (file.Value == one && file.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && file.Key.IndexOf("/PluginData/", StringComparison.OrdinalIgnoreCase) < 0
                        && File.Exists(m.Root + "/" + file.Key) && one.Libraries.Count == 0 && one.Note.Length == 0)
                        one.Note = "its library did not load (KSP.log says why)";
        }

        /// <summary>What needs what: each need is answered by the mod installed that goes by that name.</summary>
        static void Link(List<Listed> list)
        {
            var names = new Dictionary<string, Listed>(StringComparer.OrdinalIgnoreCase);
            foreach (Listed one in list) foreach (string name in one.Names) if (!names.ContainsKey(name)) names[name] = one;
            foreach (Listed one in list)
            {
                foreach (Need need in one.Needs)
                {
                    foreach (string name in need.Name.Split('|'))
                    {
                        if (names.TryGetValue(name, out Listed met) && met != one) { need.Met = met; break; }
                        if (ExpansionInstalled(name)) { need.Game = true; break; }
                    }
                    if (need.Met != null && !need.Met.NeededBy.Contains(one)) need.Met.NeededBy.Add(one);
                }
                // (one need named twice, once by CKAN and once by the library, is shown once)
                var seen = new HashSet<Listed>();
                one.Needs.RemoveAll(n => n.Met != null && !seen.Add(n.Met));
            }
        }

        /// <summary>The files under a folder with an ending (and, with outsidePluginData, not in a PluginData folder, from which the game loads no libraries).</summary>
        static List<string> Files(string folder, string ending, bool outsidePluginData = false)
        {
            var files = new List<string>();
            try
            {
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    if (!file.EndsWith(ending, StringComparison.OrdinalIgnoreCase)) continue;
                    if (outsidePluginData && file.Replace('\\', '/').IndexOf("/PluginData/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    files.Add(file);
                }
            }
            catch (Exception) { }
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        /// <summary>What a library says its version is: the version its makers wrote in for people, else its file version, else the one it gives the game (KSPAssembly), else its own.</summary>
        internal static string VersionOf(AssemblyLoader.LoadedAssembly a)
        {
            try
            {
                foreach (object one in a.assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false))
                {
                    string said = ((AssemblyInformationalVersionAttribute)one).InformationalVersion;
                    // (some add which commit they were built from, after a +)
                    if (!string.IsNullOrEmpty(said)) return said.Split('+')[0].Trim();
                }
                foreach (object one in a.assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false))
                {
                    string said = ((AssemblyFileVersionAttribute)one).Version;
                    if (!string.IsNullOrEmpty(said) && said != "0.0.0.0") return said;
                }
            }
            catch (Exception) { }
            if (a.versionMajor != 0 || a.versionMinor != 0 || a.versionRevision != 0)
                return a.versionMajor + "." + a.versionMinor + (a.versionRevision != 0 ? "." + a.versionRevision : "");
            try
            {
                Version v = a.assembly.GetName().Version;
                if (v != null && (v.Major != 0 || v.Minor != 0 || v.Build > 0 || v.Revision > 0)) return v.ToString();
            }
            catch (Exception) { }
            return "";
        }

        /// <summary>
        /// Whether version a is newer than b: more than 0 if it is, less than 0 if it is older, 0 if the same or if it cannot
        /// be told. Each is read as its numbers in order ("v0.0878", "2.21.0.4", "EVE-1.2.2-1"), compared one by one, as
        /// CKAN does. (shortest: "1.12" is the same as any 1.12.x, as a game version a mod is made for is written.)
        /// </summary>
        public static int Newer(string a, string b, bool shortest = false)
        {
            List<int> x = Numbers(Tidy(a)), y = Numbers(Tidy(b));
            if (x.Count == 0 || y.Count == 0) return 0;
            int n = shortest ? Math.Min(x.Count, y.Count) : Math.Max(x.Count, y.Count);
            for (int i = 0; i < n; i++)
            {
                int p = i < x.Count ? x[i] : 0, q = i < y.Count ? y[i] : 0;
                if (p != q) return p > q ? 1 : -1;
            }
            return 0;
        }

        static List<int> Numbers(string version)
        {
            var numbers = new List<int>();
            int value = 0;
            bool inside = false;
            foreach (char c in version ?? "")
            {
                if (c >= '0' && c <= '9') { value = Math.Min(value * 10 + (c - '0'), 100000000); inside = true; }
                else if (inside) { numbers.Add(value); value = 0; inside = false; }
            }
            if (inside) numbers.Add(value);
            return numbers;
        }
    }

    /// <summary>
    /// Just enough of a reader for JSON to read a .version file, CKAN's registry and GitHub's answers: objects (as
    /// dictionaries whose keys are compared without regard to case), lists, and everything else as its text (numbers
    /// and true and false included, as they are written).
    /// </summary>
    public static class Json
    {
        public static object Read(string s, ref int i)
        {
            Space(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++;
                while (i < s.Length)
                {
                    Space(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    int was = i;
                    string key = Read(s, ref i) as string ?? "";
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    map[key] = Read(s, ref i);
                    if (i == was) i++;                       // (never stand still on something unreadable)
                }
                return map;
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                while (i < s.Length)
                {
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    int was = i;
                    list.Add(Read(s, ref i));
                    if (i == was) i++;
                }
                return list;
            }
            if (c == '"')
            {
                var text = new StringBuilder();
                for (i++; i < s.Length && s[i] != '"'; i++)
                {
                    if (s[i] != '\\' || i + 1 >= s.Length) { text.Append(s[i]); continue; }
                    char e = s[++i];
                    if (e == 'n') text.Append('\n');
                    else if (e == 't') text.Append('\t');
                    else if (e == 'r') { }
                    else if (e == 'u' && i + 4 < s.Length && int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)) { text.Append((char)code); i += 4; }
                    else text.Append(e);
                }
                i++;
                return text.ToString();
            }
            // (a number, true, false or null: as written)
            int from = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && s[i] != ':' && !char.IsWhiteSpace(s[i])) i++;
            string word = s.Substring(from, i - from);
            return word == "null" ? null : word;
        }

        static void Space(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        /// <summary>Text written as a JSON string, quotes and all.</summary>
        public static string Quote(string text)
        {
            var s = new StringBuilder("\"");
            foreach (char c in text ?? "")
            {
                switch (c)
                {
                    case '"': s.Append("\\\""); break;
                    case '\\': s.Append("\\\\"); break;
                    case '\n': s.Append("\\n"); break;
                    case '\r': break;
                    case '\t': s.Append("\\t"); break;
                    default:
                        if (c < ' ') s.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else s.Append(c);
                        break;
                }
            }
            return s.Append('"').ToString();
        }
    }
}
