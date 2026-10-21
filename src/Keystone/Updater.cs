using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Keystone
{
    /// <summary>
    /// A mod brought up to its newest release from inside the game, by a button on its page (the owner, 2026-10-13: "make
    /// it so all the mods fetch GitHub for new releases, so that you can just press an update button in-game and it'll
    /// download the new version").
    ///
    /// Only when the player presses the button, and only for a mod whose own .version file names its GitHub repository: the
    /// zip of that repository's latest release (as Online found it), fetched over https from github.com and nowhere else,
    /// is read through before anything is touched. Every file in it must lie in the mod's own folders (or be a note beside
    /// them, which is left out); a name that climbs out of its folder, a folder that is not the mod's, a zip that cannot be
    /// read, or one bigger than any mod: and nothing is done. It is unpacked outside GameData (the game would load a copy of
    /// a library it finds there), and then put in place of the mod's folders. The folders it replaces are kept, never
    /// deleted, in Keystone/Replaced beside GameData; what the old ones kept in their PluginData that the new ones lack (the
    /// player's settings, caches) is carried over.
    ///
    /// When: a library the game has loaded cannot be replaced on Windows while the game runs. There the new version waits,
    /// unpacked, and is put in after the game has closed, by a small script Keystone starts as the game quits. On a Mac or
    /// Linux it is put in at once. Either way it is used from the next start. A mod CKAN installed is left to CKAN, which
    /// keeps a record of every file it put in (its page says to update it there).
    /// </summary>
    public static class Updater
    {
        const long Biggest = 120L * 1024 * 1024;           // no mod's download is near this: anything bigger is not one

        static readonly Dictionary<string, string> said = new Dictionary<string, string>();
        static readonly HashSet<string> done = new HashSet<string>();      // (brought up to date this session: the button is not shown again)
        static readonly List<string[]> waiting = new List<string[]>();     // (Windows: {staged folder, its place in GameData, where the old goes}) to be put in when the game has closed
        static bool busy;

        static string Root => Path.GetFullPath(KSPUtil.ApplicationRootPath);
        static string Updates => Path.Combine(Path.Combine(Root, "Keystone"), "Updates");
        static string Replaced => Path.Combine(Path.Combine(Root, "Keystone"), "Replaced");

        /// <summary>Whether it is busy with a mod just now.</summary>
        public static bool Busy => busy;

        /// <summary>What it last said about a mod ("" if nothing).</summary>
        public static string Said(Listed one) => one != null && said.TryGetValue(one.Id, out string words) ? words : "";

        /// <summary>Whether this mod could be brought up to its newest release from here.</summary>
        public static bool CanUpdate(Listed one, out string why)
        {
            why = "";
            if (one == null || !one.Behind) return false;
            if (done.Contains(one.Id)) { why = Said(one); return false; }
            if (one.ByCkan) { why = "Installed by CKAN: update it with CKAN, which keeps a record of every file it put in."; return false; }
            if (one.LatestZip.Length == 0) { why = "Its newest release has no download Keystone can use (a zip of the mod's folders on its GitHub page)."; return false; }
            if (!FromItsRepository(one, one.LatestZip)) { why = "Its newest release's download is not on its own GitHub repository."; return false; }
            if (OwnFolders(one).Count == 0) { why = "It has no folder of its own in GameData to put a new version in."; return false; }
            return true;
        }

        /// <summary>The mod's own folders in GameData (not GameData itself, and not a folder another mod also names).</summary>
        static List<string> OwnFolders(Listed one)
        {
            var own = new List<string>();
            foreach (string f in one.Folders)
                if (f.Length > 0 && f.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && f != "." && f != ".." && !string.Equals(f, "Squad", StringComparison.OrdinalIgnoreCase) && !string.Equals(f, "SquadExpansion", StringComparison.OrdinalIgnoreCase))
                    own.Add(f);
            return own;
        }

        /// <summary>A download from the mod's own GitHub repository's releases, over https (and, on the development build, a file on this computer, for trying it).</summary>
        static bool FromItsRepository(Listed one, string address)
        {
#if DEV
            if (address.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return true;
#endif
            return one.GitHub.Length > 0 && address.StartsWith("https://github.com/" + one.GitHub + "/releases/download/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The player pressed the button: fetch, read through, unpack, and put in (or, on Windows, ready to be put in when the game closes).</summary>
        public static void Begin(MonoBehaviour host, Listed one)
        {
            if (busy || host == null || !CanUpdate(one, out string _)) return;
            host.StartCoroutine(Run(one));
        }

        static IEnumerator Run(Listed one)
        {
            busy = true;
            string id = one.Id, version = one.Latest;
            said[id] = "fetching " + Path.GetFileName(new Uri(one.LatestZip).AbsolutePath) + (one.LatestZipBytes > 0 ? " (" + Size(one.LatestZipBytes) + ")" : "") + "...";
            byte[] zip = null;
            using (UnityWebRequest ask = UnityWebRequest.Get(one.LatestZip))
            {
                ask.timeout = 120;
                ask.redirectLimit = 6;
                if (!one.LatestZip.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) ask.SetRequestHeader("User-Agent", "Keystone-KSP-mod/" + Kit.Version);
                yield return ask.SendWebRequest();
                if (ask.isNetworkError || ask.isHttpError || ask.downloadHandler == null) { said[id] = "could not fetch it: " + (ask.error ?? "no answer"); busy = false; yield break; }
                zip = ask.downloadHandler.data;
            }
            if (zip == null || zip.Length == 0 || zip.Length > Biggest) { said[id] = "what came was not a mod's download (" + (zip == null ? 0 : zip.Length) + " bytes)"; busy = false; yield break; }
            try
            {
                said[id] = Put(one, version, zip);
                done.Add(id);
            }
            catch (Exception ex)
            {
                said[id] = "not put in: " + ex.Message;
                Kit.Log("Keystone", "the update of " + one.Name + " failed: " + ex);
            }
            busy = false;
        }

        /// <summary>Read the zip through, unpack it outside GameData, and put it in place (or leave it waiting). What to show the player.</summary>
        static string Put(Listed one, string version, byte[] zip)
        {
            List<string> own = OwnFolders(one);
            var files = Zip.Read(zip);
            // Every file must lie in one of the mod's own folders, or be a note beside them (README, LICENSE: left out).
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int kept = 0;
            foreach (Zip.Entry e in files)
            {
                string name = e.Name.Replace('\\', '/');
                if (name.Length == 0 || name.StartsWith("/") || name.Contains(":") || Array.IndexOf(name.Split('/'), "..") >= 0) throw new Exception("a file in it is named " + name);
                string top = name.Split('/')[0];
                if (name.IndexOf('/') < 0) continue;                                   // (a note beside the folders)
                // (a GameData folder anywhere in it: packed as a whole game's folder, or a mod's folder with GameData inside it, as
                // Keystone's mods were until 2026-10-10: put in as it is, the game would load the copies of libraries in it)
                if (Array.Exists(name.Split('/'), part => string.Equals(part, "GameData", StringComparison.OrdinalIgnoreCase))) throw new Exception("it holds a GameData folder, not the mod's folders as Keystone's mods are packed now");
                string match = own.Find(f => string.Equals(f, top, StringComparison.OrdinalIgnoreCase));
                if (match == null) throw new Exception("it holds a folder (" + top + ") that is not " + one.Name + "'s own");
                folders.Add(match);
                if (!e.Folder) kept++;
            }
            if (kept == 0) throw new Exception("it holds none of " + one.Name + "'s files");

            // Unpacked outside GameData.
            string stage = Path.Combine(Updates, Safe(one.Id) + "-" + Safe(version));
            if (Directory.Exists(stage)) Directory.Move(stage, stage + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(stage);
            foreach (Zip.Entry e in files)
            {
                string name = e.Name.Replace('\\', '/');
                if (name.IndexOf('/') < 0 || e.Folder) continue;
                string to = Path.GetFullPath(Path.Combine(stage, name.Replace('/', Path.DirectorySeparatorChar)));
                if (!to.StartsWith(Path.GetFullPath(stage) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new Exception("a file would go outside its folder: " + name);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.WriteAllBytes(to, Zip.Bytes(zip, e));
            }

            string gameData = Path.Combine(Root, "GameData");
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            bool windows = Application.platform == RuntimePlatform.WindowsPlayer;
            var swaps = new List<string[]>();
            foreach (string f in folders)
                swaps.Add(new[] { Path.Combine(stage, f), Path.Combine(gameData, f), Path.Combine(Replaced, f + "-" + Safe(one.Version) + "-" + stamp) });
            if (windows)
            {
                lock (waiting) waiting.AddRange(swaps);
                Kit.Log("Keystone", one.Name + " " + version + " unpacked in " + stage + ": to be put in when the game closes");
                return "Version " + version + " is ready: it is put in when you close the game, and used from the next start. (The old one is kept in Keystone/Replaced.)";
            }
            foreach (string[] s in swaps) Swap(s[0], s[1], s[2]);
            // (the staging folder, empty now that its folders have gone in: Keystone's own, and nothing in it)
            try { if (Directory.GetFileSystemEntries(stage).Length == 0) Directory.Delete(stage); } catch (Exception) { }
            Kit.Log("Keystone", one.Name + " brought up to " + version + "; the old folders kept in " + Replaced);
            return "Version " + version + " is in: it is used from the next start. (The old one is kept in Keystone/Replaced.)";
        }

        /// <summary>The new folder put in place of the old, the old kept aside, and what the old kept in its PluginData that the new lacks carried over.</summary>
        static void Swap(string fresh, string place, string aside)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(aside));
            if (Directory.Exists(place)) Directory.Move(place, aside);
            Directory.Move(fresh, place);
            string oldData = Path.Combine(aside, "PluginData"), newData = Path.Combine(place, "PluginData");
            if (!Directory.Exists(oldData)) return;
            foreach (string file in Directory.GetFiles(oldData, "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(newData, file.Substring(oldData.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (File.Exists(to)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(file, to);
            }
        }

        /// <summary>
        /// The game is closing (Host): on Windows, what is waiting is put in by a small script once the game's process has gone
        /// (its libraries are let go only then). It moves the old folders aside, the new ones in, carries over what the old
        /// PluginData kept that the new lacks, and deletes itself.
        /// </summary>
        public static void Closing()
        {
            string[][] swaps;
            lock (waiting) { if (waiting.Count == 0) return; swaps = waiting.ToArray(); waiting.Clear(); }
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                var s = new StringBuilder();
                s.Append("@echo off\r\n");
                // (the script is written as UTF-8 and cmd is told so: read in the console's old code page, as it would be, a folder
                // with a letter outside English in its name, the game's or a mod's, is not found. 2026-10-18, the PC)
                s.Append("chcp 65001 >nul\r\n");
                s.Append("rem Keystone: mods brought up to their newest releases, put in now that the game has closed.\r\n");
                s.Append(":wait\r\n");
                s.Append("tasklist /FI \"PID eq ").Append(pid).Append("\" 2>nul | find \"").Append(pid).Append("\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n");
                int k = 0;
                foreach (string[] w in swaps)
                {
                    string fresh = w[0], place = w[1], aside = w[2];
                    s.Append("if not exist \"").Append(Path.GetDirectoryName(aside)).Append("\" mkdir \"").Append(Path.GetDirectoryName(aside)).Append("\"\r\n");
                    // (The old folder aside first, tried for ten seconds: a virus scanner or a backup may hold one of its files a moment.
                    // If it cannot be moved, the new one is not put in: moved to where a folder still stands, it goes inside it, and the
                    // game loads both copies of the mod (seen on the PC, 2026-10-18, with a file of the old folder held open). It waits in
                    // Keystone/Updates, and the mod's page offers the update again at the next start.)
                    s.Append("for /l %%i in (1,1,10) do if exist \"").Append(place).Append("\" (move \"").Append(place).Append("\" \"").Append(aside).Append("\" >nul 2>&1 & if exist \"").Append(place).Append("\" timeout /t 1 /nobreak >nul)\r\n");
                    s.Append("if exist \"").Append(place).Append("\" goto kept").Append(k).Append("\r\n");
                    s.Append("move \"").Append(fresh).Append("\" \"").Append(place).Append("\" >nul\r\n");
                    s.Append("if exist \"").Append(Path.Combine(aside, "PluginData")).Append("\" robocopy \"").Append(Path.Combine(aside, "PluginData")).Append("\" \"").Append(Path.Combine(place, "PluginData")).Append("\" /E /XC /XN /XO >nul\r\n");
                    // (the staging folder, empty once its last folder has gone in, as the Mac's way leaves none)
                    s.Append("rd \"").Append(Path.GetDirectoryName(fresh)).Append("\" 2>nul\r\n");
                    s.Append(":kept").Append(k++).Append("\r\n");
                }
                s.Append("del \"%~f0\"\r\n");
                Directory.CreateDirectory(Updates);
                string script = Path.Combine(Updates, "put-in-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".cmd");
                File.WriteAllText(script, s.ToString());
                var start = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Updates };
                Process.Start(start);
                Kit.Log("Keystone", "the updates waiting are put in by " + script + " once the game has closed");
            }
            catch (Exception ex) { Kit.Log("Keystone", "the updates waiting could not be put in: " + ex.Message + " (they are in " + Updates + ")"); }
        }

        static string Safe(string text)
        {
            var s = new StringBuilder();
            foreach (char c in text ?? "") s.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
            return s.Length > 0 ? s.ToString() : "_";
        }

        static string Size(long bytes) => bytes >= 1024 * 1024 ? (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB" : (bytes / 1024.0).ToString("F0") + " KB";

#if DEV
        /// <summary>(Development build: the whole update tried with a zip on this computer standing for the newest release: "Folder fileOfZip version".)</summary>
        public static string Try(string words)
        {
            // (the folder first and the version last; the path between them may have spaces in it)
            string all = (words ?? "").Trim();
            int first = all.IndexOf(' '), last = all.LastIndexOf(' ');
            if (first < 0 || last <= first) return "give: the mod's folder, the zip's full path, the version it stands for";
            string[] w = { all.Substring(0, first), all.Substring(first + 1, last - first - 1).Trim(), all.Substring(last + 1) };
            Listed one = null;
            foreach (Listed l in Listing.All) if (l.Folders.Contains(w[0])) one = l;
            if (one == null) return "no mod in GameData/" + w[0];
            one.LatestZip = new Uri(w[1]).AbsoluteUri; one.LatestZipBytes = File.Exists(w[1]) ? new FileInfo(w[1]).Length : 0; one.Latest = w[2]; one.LatestAt = DateTime.UtcNow; one.LatestFrom = "a test";
            if (!CanUpdate(one, out string why)) return "cannot: " + why;
            Begin(Host.Instance, one);
            return "updating " + one.Name + " from " + w[1];
        }

        public static string Says(string folder)
        {
            foreach (Listed l in Listing.All) if (l.Folders.Contains(folder)) return l.Name + ": " + Said(l) + (busy ? " (busy)" : "");
            return "no such mod";
        }
#endif
    }

    /// <summary>Just enough of a reader for zip files to unpack a mod's download (stored or deflated files; no encryption, no zip64).</summary>
    static class Zip
    {
        public struct Entry { public string Name; public int Method; public long Packed, Size, Local; public bool Folder; }

        public static List<Entry> Read(byte[] z)
        {
            // (the end of the central directory: its signature, looked for from the end, past any comment)
            int end = -1;
            for (int i = z.Length - 22; i >= Math.Max(0, z.Length - 22 - 65535); i--)
                if (z[i] == 0x50 && z[i + 1] == 0x4b && z[i + 2] == 0x05 && z[i + 3] == 0x06) { end = i; break; }
            if (end < 0) throw new Exception("it is not a zip file");
            int count = U16(z, end + 10);
            long at = U32(z, end + 16);
            var list = new List<Entry>();
            for (int n = 0; n < count; n++)
            {
                if (at + 46 > z.Length || U32(z, (int)at) != 0x02014b50) throw new Exception("its list of files is broken");
                int p = (int)at;
                int flags = U16(z, p + 8);
                if ((flags & 1) != 0) throw new Exception("a file in it is locked with a password");
                var e = new Entry { Method = U16(z, p + 10), Packed = U32(z, p + 20), Size = U32(z, p + 24), Local = U32(z, p + 42) };
                int nameLen = U16(z, p + 28), extraLen = U16(z, p + 30), commentLen = U16(z, p + 32);
                e.Name = Encoding.UTF8.GetString(z, p + 46, nameLen);
                e.Folder = e.Name.EndsWith("/");
                if (e.Method != 0 && e.Method != 8) throw new Exception("a file in it is packed in a way this cannot read (" + e.Method + ")");
                if (e.Size > 200L * 1024 * 1024) throw new Exception("a file in it is too large");
                list.Add(e);
                at = p + 46 + nameLen + extraLen + commentLen;
            }
            return list;
        }

        public static byte[] Bytes(byte[] z, Entry e)
        {
            int p = (int)e.Local;
            if (p + 30 > z.Length || U32(z, p) != 0x04034b50) throw new Exception("a file in it is broken: " + e.Name);
            int start = p + 30 + U16(z, p + 26) + U16(z, p + 28);
            if (start + e.Packed > z.Length) throw new Exception("a file in it is cut short: " + e.Name);
            if (e.Method == 0) { var copy = new byte[e.Size]; Buffer.BlockCopy(z, start, copy, 0, (int)e.Size); return copy; }
            var output = new byte[e.Size];
            using (var input = new MemoryStream(z, start, (int)e.Packed))
            using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                int got = 0;
                while (got < output.Length)
                {
                    int read = inflate.Read(output, got, output.Length - got);
                    if (read <= 0) break;
                    got += read;
                }
                if (got != output.Length) throw new Exception("a file in it did not unpack whole: " + e.Name);
            }
            return output;
        }

        static int U16(byte[] z, int i) => z[i] | (z[i + 1] << 8);
        static long U32(byte[] z, int i) => (uint)(z[i] | (z[i + 1] << 8) | (z[i + 2] << 16) | (z[i + 3] << 24));
    }
}
