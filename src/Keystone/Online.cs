using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Keystone
{
    /// <summary>
    /// What can be learnt online about the mods installed: whether a newer version is out, and what is new in it.
    /// On as installed since Keystone 0.8 (the owner asked that the mods fetch GitHub for new releases), and a switch
    /// (Keystone's own setting "online"). Then, at most once a day for each mod,
    /// and one request at a time, it asks two kinds of public address, with plain GET requests that carry nothing about
    /// the player: the copy of a mod's .version file its makers keep online (the address its .version file or CKAN
    /// names, as KSP-AVC asks it), and GitHub's page of a repository's latest release. What it learns is kept in
    /// GameData/Keystone/PluginData/online.json, and shown with each mod. It downloads nothing itself: a mod's newest
    /// release is fetched only when the player presses its Update button (Updater).
    /// Everything else in the window works the same with no network at all.
    /// </summary>
    public static class Online
    {
        const double Hours = 20;                 // (a mod is looked at again only after this long: about once a day)
        const float Apart = 1.5f;                // seconds between one request and the next
        const int Longest = 900;                 // characters of a release's notes kept

        static bool looking;
        static float lookedNowAt = -1000f;
        static DateTime githubResting = DateTime.MinValue;
        static string state = "";

        static string File => Kit.Folder + "online.json";

        /// <summary>Whether the player allows it.</summary>
        public static bool Allowed => Kit.OnlineSwitch != null && Kit.OnlineSwitch.Value;

        /// <summary>What it is doing, or did last, in a few words.</summary>
        public static string State => state;

        /// <summary>Whether it is looking just now.</summary>
        public static bool Looking => looking;

        /// <summary>What was learnt before, from the file: put into the list as it is made.</summary>
        internal static void Remembered(List<Listed> list)
        {
            if (!System.IO.File.Exists(File)) return;
            int at = 0;
            if (!(Json.Read(System.IO.File.ReadAllText(File), ref at) is Dictionary<string, object> kept)) return;
            if (!(kept.TryGetValue("mods", out object mods) && mods is Dictionary<string, object> each)) return;
            foreach (Listed one in list)
            {
                if (!(each.TryGetValue(one.Id, out object found) && found is Dictionary<string, object> learnt)) continue;
                one.Latest = Text(learnt, "latest"); one.LatestNotes = Text(learnt, "notes"); one.LatestPage = Text(learnt, "page"); one.LatestFrom = Text(learnt, "from");
                one.LatestZip = Text(learnt, "zip");
                if (long.TryParse(Text(learnt, "zipBytes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes)) one.LatestZipBytes = bytes;
                if (long.TryParse(Text(learnt, "at"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) && ticks > 0) one.LatestAt = new DateTime(ticks, DateTimeKind.Utc);
            }
            if (kept.TryGetValue("state", out object said) && said is string words && state.Length == 0) state = words;
        }

        static string Text(Dictionary<string, object> map, string key) => map.TryGetValue(key, out object value) && value is string s ? s : "";

        static void Keep(IList<Listed> list)
        {
            try
            {
                var s = new StringBuilder("{\n  \"state\": " + Json.Quote(state) + ",\n  \"mods\": {");
                bool first = true;
                foreach (Listed one in list)
                {
                    if (one.LatestAt == default(DateTime)) continue;
                    s.Append(first ? "\n" : ",\n").Append("    ").Append(Json.Quote(one.Id)).Append(": { \"latest\": ").Append(Json.Quote(one.Latest)).Append(", \"from\": ").Append(Json.Quote(one.LatestFrom))
                     .Append(", \"page\": ").Append(Json.Quote(one.LatestPage)).Append(", \"at\": \"").Append(one.LatestAt.Ticks.ToString(CultureInfo.InvariantCulture)).Append("\", \"notes\": ").Append(Json.Quote(one.LatestNotes))
                     .Append(", \"zip\": ").Append(Json.Quote(one.LatestZip)).Append(", \"zipBytes\": \"").Append(one.LatestZipBytes.ToString(CultureInfo.InvariantCulture)).Append("\" }");
                    first = false;
                }
                s.Append("\n  }\n}\n");
                Directory.CreateDirectory(Kit.Folder);
                System.IO.File.WriteAllText(File, s.ToString());
            }
            catch (Exception ex) { Kit.Log("Keystone", "could not keep what was learnt online: " + ex.Message); }
        }

        /// <summary>
        /// Look, in the background, if the player allows it: for every mod not looked at in the last day (or, with now, for
        /// every mod, if the last look asked for was more than ten minutes ago).
        /// </summary>
        internal static void Look(MonoBehaviour host, bool now)
        {
            if (!Allowed || looking || host == null) return;
            if (now)
            {
                if (Time.realtimeSinceStartup - lookedNowAt < 600f) { state = "looked less than ten minutes ago: try again later"; return; }
                lookedNowAt = Time.realtimeSinceStartup;
            }
            host.StartCoroutine(Run(now));
        }

        static IEnumerator Run(bool now)
        {
            looking = true;
            IList<Listed> list = Listing.All;
            var due = new List<Listed>();
            foreach (Listed one in list)
                if ((Address(one.Remote).Length > 0 || one.GitHub.Length > 0) && (now || (DateTime.UtcNow - one.LatestAt).TotalHours > Hours)) due.Add(one);
            int done = 0, newer = 0, failed = 0;
            foreach (Listed one in due)
            {
                if (!Allowed) break;                                        // (switched off half way: stop)
                state = "looking: " + (done + 1) + " of " + due.Count + " (" + one.Name + ")";
                string latest = "", notes = "", page = "", from = "", zip = "";
                long zipBytes = 0;
                bool heard = false;
                // Its .version file as its makers keep it online.
                string remote = Address(one.Remote);
                if (remote.Length > 0)
                {
                    using (UnityWebRequest ask = Ask(remote, false))
                    {
                        yield return ask.SendWebRequest();
                        if (Fine(ask))
                        {
                            int at = 0;
                            if (Json.Read(ask.downloadHandler.text, ref at) is Dictionary<string, object> said)
                            {
                                latest = VersionIn(said);
                                if (latest.Length > 0) { from = "its .version file online"; heard = true; }
                            }
                        }
                    }
                    yield return new WaitForSecondsRealtime(Apart);
                }
                // Its latest release on GitHub: the version, a few lines of what is new, and its page.
                if (one.GitHub.Length > 0 && DateTime.UtcNow > githubResting)
                {
                    using (UnityWebRequest ask = Ask("https://api.github.com/repos/" + one.GitHub + "/releases/latest", true))
                    {
                        yield return ask.SendWebRequest();
                        if (Fine(ask))
                        {
                            int at = 0;
                            if (Json.Read(ask.downloadHandler.text, ref at) is Dictionary<string, object> release)
                            {
                                string tag = Text(release, "tag_name");
                                if (latest.Length == 0 && tag.Length > 0) { latest = tag; from = "GitHub"; }
                                notes = Notes(Text(release, "body"));
                                page = Text(release, "html_url");
                                // (its download, for Updater: the zip named after one of the mod's folders, or the only zip there is)
                                if (release.TryGetValue("assets", out object assets) && assets is List<object> files)
                                {
                                    int zips = 0;
                                    foreach (object f in files)
                                    {
                                        if (!(f is Dictionary<string, object> asset)) continue;
                                        string name = Text(asset, "name"), url = Text(asset, "browser_download_url");
                                        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                                        zips++;
                                        bool named = one.Folders.Exists(folder => folder.Length > 0 && name.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
                                        if (named || zip.Length == 0)
                                        {
                                            zip = url;
                                            long.TryParse(Text(asset, "size"), NumberStyles.Integer, CultureInfo.InvariantCulture, out zipBytes);
                                            if (named) break;
                                        }
                                    }
                                    if (zips > 1 && !one.Folders.Exists(folder => folder.Length > 0 && Path.GetFileName(zip).StartsWith(folder, StringComparison.OrdinalIgnoreCase))) { zip = ""; zipBytes = 0; }
                                }
                                heard |= tag.Length > 0;
                            }
                        }
                        else if (ask.responseCode == 403 || ask.responseCode == 429)
                            githubResting = DateTime.UtcNow.AddHours(1);              // (GitHub allows sixty such questions an hour from one place without signing in: rest)
                    }
                    yield return new WaitForSecondsRealtime(Apart);
                }
                done++;
                if (heard)
                {
                    one.Latest = Strip(latest); one.LatestNotes = notes; one.LatestPage = page; one.LatestFrom = from; one.LatestAt = DateTime.UtcNow;
                    one.LatestZip = zip; one.LatestZipBytes = zipBytes;
                    if (one.Behind) newer++;
                }
                else failed++;
            }
            state = due.Count == 0 ? "nothing to look for" : "looked at " + done + (done == 1 ? " mod" : " mods") + " on " + DateTime.Now.ToString("d MMM, HH:mm", CultureInfo.InvariantCulture)
                    + ": " + (newer == 0 ? "no newer versions" : newer + (newer == 1 ? " newer version" : " newer versions")) + (failed > 0 ? "; " + failed + " could not be reached" : "")
                    + (DateTime.UtcNow < githubResting ? " (GitHub asked to wait an hour)" : "");
            Keep(list);
            looking = false;
        }

        /// <summary>An address that may be asked: https only (and, for trying things on a development build, a file on this computer).</summary>
        static string Address(string address)
        {
            address = (address ?? "").Trim();
            if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return address;
#if DEV
            if (address.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return address;
#endif
            return "";
        }

        static UnityWebRequest Ask(string address, bool github)
        {
            UnityWebRequest ask = UnityWebRequest.Get(address);
            ask.timeout = 20;
            ask.redirectLimit = 5;
            if (!address.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                // (GitHub wants every request to say what is asking; this says which mod, and nothing about the player)
                ask.SetRequestHeader("User-Agent", "Keystone-KSP-mod/" + Kit.Version);
                if (github) ask.SetRequestHeader("Accept", "application/vnd.github+json");
            }
            return ask;
        }

        static bool Fine(UnityWebRequest ask) => !ask.isNetworkError && !ask.isHttpError && ask.downloadHandler != null && !string.IsNullOrEmpty(ask.downloadHandler.text);

        static string VersionIn(Dictionary<string, object> said)
        {
            if (!said.TryGetValue("VERSION", out object value)) return "";
            if (value is string text) return text.Trim();
            if (!(value is Dictionary<string, object> parts)) return "";
            string major = Text(parts, "MAJOR"), minor = Text(parts, "MINOR"), patch = Text(parts, "PATCH"), build = Text(parts, "BUILD");
            if (major.Length == 0) return "";
            return major + "." + (minor.Length > 0 ? minor : "0") + (patch.Length > 0 ? "." + patch : "") + (build.Length > 0 && build != "0" ? "." + build : "");
        }

        static string Strip(string version)
        {
            version = (version ?? "").Trim();
            if (version.Length > 1 && (version[0] == 'v' || version[0] == 'V') && char.IsDigit(version[1])) version = version.Substring(1);
            return version;
        }

        /// <summary>The first lines of a release's notes, without the marks that make headings and lists on GitHub.</summary>
        static string Notes(string body)
        {
            var s = new StringBuilder();
            foreach (string raw in (body ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim().TrimStart('#', '*', '-', '>', ' ').Replace("**", "").Replace("`", "").Trim();
                if (line.Length == 0) continue;
                if (s.Length + line.Length > Longest) { s.Append("..."); break; }
                if (s.Length > 0) s.Append('\n');
                s.Append(line);
            }
            return s.ToString();
        }

#if DEV
        /// <summary>
        /// (For the development build: the whole look, with every request sent to a file on this computer instead of the
        /// network: each mod's own .version file in GameData, as if it were the copy online. Nothing leaves the computer.)
        /// </summary>
        public static string TryOffline()
        {
            // (every address pointed at a file here before the switch goes on: switching it on with the window open looks at once)
            int pointed = 0;
            foreach (Listed one in Listing.All)
            {
                one.GitHub = ""; one.Remote = "";
                foreach (string folder in one.Folders)
                {
                    if (folder.Length == 0) continue;
                    string[] files = Directory.GetFiles(KSPUtil.ApplicationRootPath + "GameData/" + folder, "*.version", SearchOption.AllDirectories);
                    if (files.Length > 0) { one.Remote = "file://" + Path.GetFullPath(files[0]); pointed++; break; }
                }
            }
            lookedNowAt = -1000f;
            if (!Allowed) Kit.OnlineSwitch.Value = true;
            Look(Host.Instance, true);
            return "looking at " + pointed + " local .version files as if online";
        }

        /// <summary>(For the development build: after TryOffline, the switch off again, the real addresses back, and what was learnt forgotten.)</summary>
        public static string Forget()
        {
            if (Allowed) Kit.OnlineSwitch.Value = false;
            try { if (System.IO.File.Exists(File)) System.IO.File.Delete(File); } catch (Exception) { }
            state = "";
            Listing.Again();
            return "forgotten";
        }
#endif
    }
}
