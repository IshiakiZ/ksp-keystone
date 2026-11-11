using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// (Since 0.11.0.) Every mod's settings as a code: a line of letters to copy, paste into a message, and paste again in another
    /// game, where using it sets that game's mods as the code has them. The code names each mod it was made with, its version and
    /// where it is to be had, so that whoever uses it is told of a mod it names that they have not got, and where to get it.
    ///
    /// In it: each of Keystone's mods with its version and its GitHub repository, and those of its settings that are not as
    /// installed (not its keys: those are each player's own, as Keystone's own settings are). Used, a mod it names is set as it
    /// says and its other settings put back as installed, so that what is shared is the whole of how it was set; a mod it does
    /// not name is left as it is. The settings as they were are kept as a set first (Kit.Keep), to be put back.
    ///
    /// The code is "KS1", a letter for its kind (z: the text below packed with Deflate; t: the text as it is), and then the text
    /// in base 64, with - and _ for + and /. The text: "keystone VERSION"; for each mod "mod|NAME|VERSION|OWNER/REPOSITORY" and
    /// its settings as "KEY=VALUE"; and "end", so that a code cut short in the copying is known to be.
    /// </summary>
    internal static class Share
    {
        const string Prefix = "KS1";
        static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        sealed class InCode
        {
            public string Name = "", Version = "", GitHub = "";
            public readonly List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
        }

        static string made = "", madeSaid = "", pastedSaid = "", usedSaid = "";
        static List<InCode> read;
        /// <summary>The mods a code read names that are not installed here: what they are called, and where to get them ("" if it does not say).</summary>
        static readonly List<KeyValuePair<string, string>> missing = new List<KeyValuePair<string, string>>();
        // (what a click asked for, done at the next laying out of the page, so that it is the same from its laying out to its drawing:
        // 1 copy, 2 paste, 3 use, 4 forget)
        static int asked;

        static GUIStyle wrapped;
        static GUIStyle Wrapped => wrapped ?? (wrapped = new GUIStyle(GUI.skin.textArea) { wordWrap = true, fontSize = 10 });

        /// <summary>On Keystone's page, under the sets.</summary>
        internal static void Draw()
        {
            if (Event.current.type == EventType.Layout && asked != 0) { Do(asked); asked = 0; }
            GUILayout.Space(6f);
            GUILayout.Label(new GUIContent("<b>Share your settings</b>", "Every mod's settings as a code: copy it, paste it into a message, and whoever you give it to pastes it here to have their mods set as yours. The code names each mod it was made with, so that they are told of any they have not got, and where to get them."), Host.Rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy a code", "A code of every mod's settings as they are now, copied, to paste wherever you like (your keys are left out: they are yours)."), GUILayout.Width(110f))) asked = 1;
            if (GUILayout.Button(new GUIContent("Paste a code", "Reads a code someone gave you from what you last copied, and says what using it would do: nothing is changed yet."), GUILayout.Width(110f))) asked = 2;
            GUILayout.EndHorizontal();
            if (madeSaid.Length > 0)
            {
                GUILayout.Label(madeSaid, Host.Small);
                // (shown too, to be copied by hand if the copying did not reach where it is to go)
                GUILayout.TextArea(made, Wrapped, GUILayout.MaxHeight(64f));
            }
            if (pastedSaid.Length > 0) GUILayout.Label(pastedSaid, Host.Small);
            if (read != null)
            {
                foreach (KeyValuePair<string, string> one in missing)
                {
                    if (one.Value.Length == 0) continue;
                    if (GUILayout.Button(new GUIContent("Get " + one.Key, "Opens " + one.Value + " in your web browser."), GUILayout.ExpandWidth(false))) Application.OpenURL(one.Value);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Use it", "Your mods set as the code has them, and their other settings put back as installed: your own settings are kept as a set first, under Sets of settings."), GUILayout.Width(110f))) asked = 3;
                if (GUILayout.Button(new GUIContent("Forget it", "Nothing changed."), GUILayout.Width(110f))) asked = 4;
                GUILayout.EndHorizontal();
            }
            if (usedSaid.Length > 0) GUILayout.Label(usedSaid, Host.Small);
        }

        static void Do(int what)
        {
            switch (what)
            {
                case 1:
                    made = Code(out int mods, out int changed);
                    GUIUtility.systemCopyBuffer = made;
                    madeSaid = "Copied: a code of " + made.Length + " letters, for " + mods + (mods == 1 ? " mod" : " mods") + " and " + changed + (changed == 1 ? " setting" : " settings") + " not as installed. Paste it wherever you like.";
                    break;
                case 2:
                    read = Read(GUIUtility.systemCopyBuffer ?? "", out string why, out string keystone);
                    usedSaid = "";
                    pastedSaid = read == null ? why : Preview(read, keystone);
                    break;
                case 3:
                    if (read != null) usedSaid = Use(read);
                    read = null; pastedSaid = "";
                    break;
                case 4:
                    read = null; pastedSaid = "";
                    break;
            }
        }

        /// <summary>The GitHub repository a registered mod is known by ("owner/name": from its .version file or CKAN's record), or "".</summary>
        static string GitHubOf(Mod m)
        {
            try { foreach (Listed one in Listing.All) if (one.ModName == m.Name) return one.GitHub ?? ""; }
            catch (Exception) { }
            return "";
        }

        static string Clean(string text) => (text ?? "").Replace("|", "/").Replace("\n", " ").Replace("\r", "");

        /// <summary>Whether what a code gives as a mod's repository is one ("owner/name", nothing else): only such an address is opened.</summary>
        static bool Repository(string text)
        {
            string[] parts = (text ?? "").Split('/');
            if (parts.Length != 2) return false;
            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 100 || part == "." || part == "..") return false;
                foreach (char c in part) if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')) return false;
            }
            return true;
        }

        /// <summary>The code for every mod's settings as they are now: how many mods are in it, and how many settings not as installed.</summary>
        internal static string Code(out int mods, out int changed)
        {
            var text = new StringBuilder();
            text.Append("keystone ").Append(Kit.Version).Append('\n');
            mods = 0; changed = 0;
            foreach (Mod m in new List<Mod>(Kit.Mods))
            {
                text.Append("mod|").Append(Clean(m.Name)).Append('|').Append(Clean(m.Version)).Append('|').Append(Clean(GitHubOf(m))).Append('\n');
                mods++;
                foreach (Setting setting in m.Settings)
                {
                    if (setting == null || setting is Hotkey || string.IsNullOrEmpty(setting.Key) || setting.AsInstalled) continue;
                    string value = setting.Write();
                    if (value == null) continue;
                    text.Append(setting.Key).Append('=').Append(Clean(value)).Append('\n');
                    changed++;
                }
            }
            text.Append("end\n");
            return Pack(text.ToString());
        }

        static string Pack(string text)
        {
            byte[] plain = Encoding.UTF8.GetBytes(text), packed = null;
            try
            {
                using (var into = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(into, CompressionMode.Compress, true)) deflate.Write(plain, 0, plain.Length);
                    packed = into.ToArray();
                }
                // (read back at once: a game whose Deflate cannot be trusted gives the text as it is, longer but sure)
                if (Inflate(packed) != text) packed = null;
            }
            catch (Exception) { packed = null; }
            return Prefix + (packed != null ? "z" : "t") + Letters(packed ?? plain);
        }

        static string Inflate(byte[] bytes)
        {
            using (var from = new MemoryStream(bytes))
            using (var inflate = new DeflateStream(from, CompressionMode.Decompress))
            using (var into = new MemoryStream())
            {
                var buffer = new byte[4096];
                int n;
                while ((n = inflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    into.Write(buffer, 0, n);
                    if (into.Length > 1 << 20) throw new InvalidDataException("too long");     // (no code of settings is a megabyte)
                }
                return Encoding.UTF8.GetString(into.ToArray());
            }
        }

        static string Letters(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] FromLetters(string letters)
        {
            string b = letters.Replace('-', '+').Replace('_', '/');
            while (b.Length % 4 != 0) b += "=";
            return Convert.FromBase64String(b);
        }

        /// <summary>A code read: the mods in it and their settings; nothing, and why, if it cannot be read.</summary>
        static List<InCode> Read(string code, out string why, out string keystone)
        {
            why = ""; keystone = "";
            int start = (code ?? "").IndexOf(Prefix, StringComparison.Ordinal);
            if (start < 0) { why = "What you last copied is not a code of settings (a code begins KS1). Copy the code, then press Paste a code."; return null; }
            // (A message may break a code over lines, and put words after it: the code is taken with its pieces joined, and failing
            // that with the last piece left off, and the last two, and so on, until one reads to its end. Quotes and the like round
            // it are left out.)
            string[] pieces = code.Substring(start).Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string text = null;
            bool readSome = false;
            for (int k = Math.Min(pieces.Length, 400); k >= 1 && text == null; k--)
            {
                var only = new StringBuilder();
                for (int p = 0; p < k; p++)
                    foreach (char c in pieces[p]) if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_') only.Append(c);
                string s = only.ToString();
                if (s.Length < Prefix.Length + 2) continue;
                string got = null;
                try
                {
                    byte[] bytes = FromLetters(s.Substring(Prefix.Length + 1));
                    char kind = s[Prefix.Length];
                    got = kind == 'z' ? Inflate(bytes) : kind == 't' ? Encoding.UTF8.GetString(bytes) : null;
                }
                catch (Exception) { }
                if (got == null) continue;
                readSome = true;
                if (got.IndexOf("\nend\n", StringComparison.Ordinal) >= 0) text = got;
            }
            if (text == null)
            {
                why = readSome ? "That code is cut short: the end of it was lost in the copying. Copy the whole of it again."
                               : "That code cannot be read: some of it was lost in the copying, or it was made by a later Keystone. Copy the whole of it again.";
                return null;
            }
            var list = new List<InCode>();
            InCode current = null;
            bool ended = false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line == "end") { ended = true; break; }
                if (line.StartsWith("keystone ", StringComparison.Ordinal)) { keystone = line.Substring(9).Trim(); continue; }
                if (line.StartsWith("mod|", StringComparison.Ordinal))
                {
                    string[] p = line.Split('|');
                    current = new InCode { Name = p.Length > 1 ? p[1] : "", Version = p.Length > 2 ? p[2] : "", GitHub = p.Length > 3 ? p[3] : "" };
                    list.Add(current);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (current != null && eq > 0) current.Values.Add(new KeyValuePair<string, string>(line.Substring(0, eq), line.Substring(eq + 1)));
            }
            if (!ended) { why = "That code is cut short: the end of it was lost in the copying. Copy the whole of it again."; return null; }
            if (list.Count == 0) { why = "That code has no mods in it."; return null; }
            return list;
        }

        /// <summary>What using a code would do, in words, before anything is changed; and the mods it names that are not here, kept for their buttons.</summary>
        static string Preview(List<InCode> code, string keystone)
        {
            missing.Clear();
            int here = 0, changes = 0;
            var needed = new List<string>();
            var also = new List<string>();
            var otherVersions = new List<string>();
            foreach (InCode c in code)
            {
                changes += c.Values.Count;
                string what = c.Name + (c.Version.Length > 0 ? " " + c.Version : "");
                Mod m = Kit.Find(c.Name);
                if (m != null)
                {
                    here++;
                    if (c.Version.Length > 0 && m.Version != c.Version) otherVersions.Add(c.Name + " (the code's " + c.Version + ", yours " + m.Version + ")");
                    continue;
                }
                if (c.Values.Count > 0) needed.Add(what); else also.Add(what);
                missing.Add(new KeyValuePair<string, string>(what, Repository(c.GitHub) ? "https://github.com/" + c.GitHub : ""));
            }
            var said = new StringBuilder();
            said.Append("A code of ").Append(code.Count).Append(code.Count == 1 ? " mod's settings" : " mods' settings").Append(", ").Append(changes).Append(changes == 1 ? " of them" : " of them").Append(" not as installed");
            if (keystone.Length > 0) said.Append(" (made with Keystone ").Append(keystone).Append(")");
            said.Append(". Used, it sets ").Append(here).Append(here == 1 ? " of your mods" : " of your mods").Append(" as it has them; your own settings are kept as a set first.");
            if (needed.Count > 0) said.Append("\n<color=#ff8a70>Needed for these settings, and not installed here: ").Append(string.Join(", ", needed.ToArray())).Append(". Their settings are left out: install them and use the code again to have those too.</color>");
            if (also.Count > 0) said.Append("\n<color=#e0b050>Also in the game it was made in, as installed, and not installed here: ").Append(string.Join(", ", also.ToArray())).Append(".</color>");
            if (otherVersions.Count > 0) said.Append("\nMade with other versions: ").Append(string.Join(", ", otherVersions.ToArray())).Append(". A setting your version does not have is left out.");
            return said.ToString();
        }

        /// <summary>Every mod the code names that is here, set as it has them, its other settings as installed. What was done, in words.</summary>
        static string Use(List<InCode> code)
        {
            // (to the second: a second code used within the minute must not write its own "before" over the first's)
            string name = "Mine " + DateTime.Now.ToString("MM-dd HH-mm-ss", Plain);
            string kept = Kit.Keep(name);
            bool keptOk = kept.StartsWith("Kept", StringComparison.Ordinal);
            int changed = 0, mods = 0, left = 0;
            foreach (InCode c in code)
            {
                Mod m = Kit.Find(c.Name);
                if (m == null) continue;
                int before = changed;
                var values = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kv in c.Values) values[kv.Key] = kv.Value;
                foreach (Setting setting in m.Settings)
                {
                    if (setting == null || setting is Hotkey || string.IsNullOrEmpty(setting.Key)) continue;
                    if (values.TryGetValue(setting.Key, out string value))
                    {
                        if (setting.Apply(value)) changed++;
                        values.Remove(setting.Key);
                    }
                    else if (!setting.AsInstalled) { setting.Reset(); changed++; }
                }
                left += values.Count;
                if (changed > before) mods++;
            }
            var said = new StringBuilder();
            said.Append(changed == 0 ? "Your mods were set as the code has them already." : changed + (changed == 1 ? " setting" : " settings") + " changed in " + mods + (mods == 1 ? " mod" : " mods") + ", as the code has them.");
            if (left > 0) said.Append(" ").Append(left).Append(left == 1 ? " setting in it is not in your versions, and was left out." : " settings in it are not in your versions, and were left out.");
            said.Append(keptOk ? " Your own were kept first as the set \"" + name + "\" (Sets of settings, above: Put back)." : " (Your own could not be kept first: " + kept + ")");
            if (missing.Count > 0)
            {
                var names = new List<string>();
                foreach (KeyValuePair<string, string> one in missing) names.Add(one.Key);
                said.Append(" Not installed here, so left out: ").Append(string.Join(", ", names.ToArray())).Append(".");
            }
            return said.ToString();
        }

#if DEV
        /// <summary>(Development build) A code of every mod's settings now, as the button makes it, read back and said: for trying it from outside.</summary>
        public static string CodeNow(string _)
        {
            string code = Code(out int mods, out int changed);
            List<InCode> back = Read(code, out string why, out string keystone);
            return code + "\n" + code.Length + " letters (" + (code.Length > 3 && code[3] == 'z' ? "packed" : "not packed") + "), " + mods + " mods, " + changed + " settings not as installed; read back: " + (back == null ? why : back.Count + " mods, Keystone " + keystone);
        }

        /// <summary>(Development build) What a code would do, as Paste a code says it.</summary>
        public static string Look(string code)
        {
            List<InCode> got = Read(code, out string why, out string keystone);
            if (got == null) return why;
            string said = Preview(got, keystone);
            foreach (KeyValuePair<string, string> one in missing) said += "\n(Get " + one.Key + ": " + (one.Value.Length > 0 ? one.Value : "no button") + ")";
            return said;
        }

        /// <summary>(Development build) A code used, as Use it does.</summary>
        public static string UseCode(string code)
        {
            List<InCode> got = Read(code, out string why, out string keystone);
            if (got == null) return why;
            Preview(got, keystone);
            return Use(got);
        }
#endif
    }
}
