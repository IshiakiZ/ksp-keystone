using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Keystone
{
    /// <summary>
    /// (Since 0.10.0.) Recorded sounds a mod carries in one packed file of its own, PluginData/sounds.pack. The recordings
    /// come from libraries whose licence lets a mod play them but not hand them out as sound files (Sonniss's #GameAudioGDC
    /// bundles: "may not distribute ... the sound effects as sound effects"), so they are not loose .ogg files a player could
    /// lift as a pack; and the game's own loader, which reads every sound file under GameData but none in a PluginData folder,
    /// does not see them. A mod built without its recordings (a repository's build: they are never in one) has no pack, and
    /// plays the sounds it makes itself, as before.
    ///
    /// The pack (written by tools/sounds/pack.py): "KSND" and a version byte (1); the number of sounds (4 bytes, little-endian);
    /// for each its name (2 bytes of length, then UTF-8), where its bytes start in the file (8) and how many there are (4);
    /// then the sounds, each Ogg Vorbis turned by a running key made from its name (xorshift32 from the name's FNV-1a hash: not
    /// secrecy, only that what is there is not a sound file). A sound is read when a mod first asks for it: turned back,
    /// written to the game's temporary folder, loaded by Unity as any Ogg file is, and the file removed at once.
    ///
    /// One sound is read at a time, the next as soon as it is in. Unity unpacks a sound it hands over on the game's own thread,
    /// there and then, about half a millisecond for each second of it: the thirty-seven of the sea and the weather, asked
    /// for together at a flight's start, were one frame of 340 ms (2026-10-24, the Mac); one at a time, the dearest frame had
    /// 8.5 ms of it. And a long loop of what is all round (rain, wind, the sea), asked for with Stream, is not unpacked at
    /// all: it is kept packed in memory, as read, and unpacked by the sound library on its own thread as it plays (as
    /// Mission Music plays its tracks), ready in a frame at about 2 ms.
    /// </summary>
    public static class Sounds
    {
        sealed class Pack
        {
            public string Path;
            public readonly Dictionary<string, KeyValuePair<long, int>> Where = new Dictionary<string, KeyValuePair<long, int>>(StringComparer.OrdinalIgnoreCase);
        }

        static readonly Dictionary<string, Pack> packs = new Dictionary<string, Pack>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, List<Action<AudioClip>>> waiting = new Dictionary<string, List<Action<AudioClip>>>(StringComparer.OrdinalIgnoreCase);

        // (those asked for, read in turn; the one being read, so that it is asked again if what was reading it went away; and
        // what reads them: an object of its own, kept through every scene's change, which does not stop it half way)
        static readonly Queue<string[]> queue = new Queue<string[]>();
        static string[] reading;
        sealed class Reader : MonoBehaviour { }
        static Reader reader;
        static bool readingAll;
        static bool Reading => readingAll && reader != null;

        // (the requests streamed sounds are read through as they play: kept with them, for the rest of the game. One let go
        // while the sound library might still read it crashed the game: Mission Music's Decks.Part, 2026-10-24)
        static readonly List<UnityWebRequest> streams = new List<UnityWebRequest>();

        /// <summary>The names of the sounds a mod's pack holds (none if it has no pack).</summary>
        public static ICollection<string> Names(string mod) => Open(mod)?.Where.Keys ?? (ICollection<string>)new string[0];

        /// <summary>Whether a mod's pack holds a sound of that name.</summary>
        public static bool Has(string mod, string name) => Open(mod)?.Where.ContainsKey(name) ?? false;

        /// <summary>
        /// A sound from a mod's pack, handed to then when it is loaded: at once if it is already; null if the mod has no pack,
        /// the pack no such sound, or it could not be read. Loaded once, and kept for the rest of the game.
        /// </summary>
        public static void Get(string mod, string name, Action<AudioClip> then) => Ask(mod, name, then, false);

        /// <summary>
        /// As Get, but the sound kept packed and unpacked by the sound library as it plays: for a long loop that one source
        /// plays at a time (the rain all round, the sea), ready in a frame where unpacking it whole holds the game up a
        /// millisecond or so for every two seconds of it. Such a sound can be played by one source at a time only: another
        /// starting it stops the first (seen 2026-10-24). A sound is streamed or not as it was first asked for.
        /// </summary>
        public static void Stream(string mod, string name, Action<AudioClip> then) => Ask(mod, name, then, true);

        static void Ask(string mod, string name, Action<AudioClip> then, bool streamed)
        {
            string key = mod + "/" + name;
            if (loaded.TryGetValue(key, out AudioClip have)) { then(have); return; }
            Pack pack = Open(mod);
            if (pack == null || !pack.Where.ContainsKey(name)) { then(null); return; }
            if (waiting.TryGetValue(key, out List<Action<AudioClip>> others)) { others.Add(then); if (!Reading) Read(); return; }
            waiting[key] = new List<Action<AudioClip>> { then };
            queue.Enqueue(new[] { mod, name, key, streamed ? "streamed" : "" });
            if (!Reading) Read();
        }

        /// <summary>The reading begun (again, if what was reading went away with one half read: that one first).</summary>
        static void Read()
        {
            if (reading != null)
            {
                var again = new Queue<string[]>();
                again.Enqueue(reading);
                foreach (string[] job in queue) again.Enqueue(job);
                queue.Clear();
                foreach (string[] job in again) queue.Enqueue(job);
                reading = null;
            }
            if (reader == null)
            {
                var holder = new GameObject("Keystone sounds");
                UnityEngine.Object.DontDestroyOnLoad(holder);
                reader = holder.AddComponent<Reader>();
            }
            readingAll = true;
            reader.StartCoroutine(ReadAll());
        }

        static IEnumerator ReadAll()
        {
            int lastIn = -1;
            while (queue.Count > 0)
            {
                // (one taken in a frame at most: Unity's request for a file can be done at once, and thirteen footsteps came in on
                // one frame)
                if (Time.frameCount == lastIn) yield return null;
                reading = queue.Dequeue();
                Pack pack = Open(reading[0]);
                if (pack != null && pack.Where.ContainsKey(reading[1])) yield return reader.StartCoroutine(Load(reading[0], pack, reading[1], reading[2], reading[3] == "streamed"));
                else Handed(reading[2], null);
                lastIn = Time.frameCount;
                reading = null;
            }
            readingAll = false;
        }

        /// <summary>A sound already loaded (null if it is not, yet).</summary>
        public static AudioClip Ready(string mod, string name)
        {
            if (loaded.TryGetValue(mod + "/" + name, out AudioClip clip)) return clip;
            // (what was reading them gone with some still to read: begun again, by those that wait for one)
            if (!Reading && (queue.Count > 0 || reading != null)) Read();
            return null;
        }

#if DEV
        /// <summary>
        /// (Development build: what can be heard just now, there being no ears here.) Every sound source playing, the loudest
        /// first: its object, its clip, its volume and pitch, and how far it is from the game's listener; text containing what
        /// is given ("Fire", "rain") keeps only those. Then what each mod's pack holds and how many of its sounds are loaded.
        /// </summary>
        public static string Playing(string only)
        {
            AudioListener ears = UnityEngine.Object.FindObjectOfType<AudioListener>();
            var rows = new List<KeyValuePair<float, string>>();
            foreach (AudioSource s in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (s == null || !s.isPlaying) continue;
                string clip = s.clip != null ? s.clip.name : "(none)";
                string what = s.gameObject.name + ": " + clip;
                if (!string.IsNullOrEmpty(only) && what.IndexOf(only.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                float far = ears != null ? (s.transform.position - ears.transform.position).magnitude : -1f;
                rows.Add(new KeyValuePair<float, string>(s.volume, what + ", volume " + s.volume.ToString("F3") + ", pitch " + s.pitch.ToString("F2") + (s.loop ? ", looped" : "") + (s.mute ? ", muted" : "") + ", " + far.ToString("F0") + " m off" + (s.spatialBlend > 0.5f ? "" : " (flat)")));
            }
            rows.Sort((a, b) => b.Key.CompareTo(a.Key));
            var o = new StringBuilder(rows.Count + " playing" + (string.IsNullOrEmpty(only) ? "" : " with \"" + only.Trim() + "\"") + ":\n");
            foreach (KeyValuePair<float, string> r in rows) o.Append("  ").Append(r.Value).Append('\n');
            foreach (KeyValuePair<string, Pack> p in packs)
            {
                int have = 0;
                if (p.Value != null) foreach (string n in p.Value.Where.Keys) if (loaded.ContainsKey(p.Key + "/" + n)) have++;
                o.Append(p.Key).Append("'s pack: ").Append(p.Value == null ? "none" : p.Value.Where.Count + " sounds, " + have + " loaded").Append('\n');
            }
            return o.ToString();
        }
#endif

#if DEV
        static readonly List<string> loadNotes = new List<string>();

        /// <summary>
        /// (Development build: whether one loaded sound plays twice at once, as two crashes' crunches or two claps of thunder
        /// may: "mod/name", started on two sources a second apart; then each one's place in it, read twice, half a second
        /// apart, on Twice("").)
        /// </summary>
        public static string Twice(string what)
        {
            if (string.IsNullOrEmpty((what ?? "").Trim()))
            {
                var o = new StringBuilder();
                foreach (AudioSource s in twice) o.Append(s == null ? "gone; " : (s.isPlaying ? "playing" : "stopped") + " at " + s.time.ToString("F2") + " s (" + s.timeSamples + "); ");
                return o.ToString();
            }
            foreach (AudioSource s in twice) if (s != null) UnityEngine.Object.Destroy(s.gameObject);
            twice.Clear();
            AudioClip clip = loaded.TryGetValue(what.Trim(), out AudioClip c) ? c : null;
            if (clip == null) return "not loaded: " + what;
            for (int n = 0; n < 2; n++)
            {
                var go = new GameObject("Keystone twice " + n);
                AudioSource s = go.AddComponent<AudioSource>();
                s.clip = clip; s.spatialBlend = 0f; s.volume = 0.05f;
                if (n == 0) s.Play(); else s.PlayDelayed(1f);
                twice.Add(s);
            }
            return "playing " + clip.name + " (" + clip.loadType + ", " + clip.length.ToString("F1") + " s) twice, a second apart";
        }
        static readonly List<AudioSource> twice = new List<AudioSource>();

        /// <summary>(Development build: what each sound's loading cost, in the order they came in; "clear" forgets them.)</summary>
        public static string Loads(string how)
        {
            string said = loadNotes.Count + " loaded:\n  " + string.Join("\n  ", loadNotes.ToArray());
            if ((how ?? "").Trim() == "clear") loadNotes.Clear();
            return said;
        }
#endif

        static Pack Open(string mod)
        {
            if (packs.TryGetValue(mod, out Pack known)) return known;
            Pack pack = null;
            string path = KSPUtil.ApplicationRootPath + "GameData/" + mod + "/PluginData/sounds.pack";
            try
            {
                if (File.Exists(path))
                {
                    using (var reader = new BinaryReader(File.OpenRead(path)))
                    {
                        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "KSND" || reader.ReadByte() != 1) throw new Exception("not a pack of version 1");
                        int count = reader.ReadInt32();
                        pack = new Pack { Path = path };
                        for (int n = 0; n < count; n++)
                        {
                            string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
                            long start = reader.ReadInt64();
                            int length = reader.ReadInt32();
                            pack.Where[name] = new KeyValuePair<long, int>(start, length);
                        }
                    }
                }
            }
            catch (Exception problem)
            {
                Debug.LogWarning("[Keystone] " + mod + "'s sounds could not be read (" + problem.Message + "): its own made sounds are played instead");
                pack = null;
            }
            packs[mod] = pack;
            return pack;
        }

        /// <summary>The running key, applied to a sound's bytes in place (applied twice, it gives them back).</summary>
        static void Turn(byte[] bytes, string name)
        {
            uint x = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(name)) { x ^= b; x *= 16777619u; }
            if (x == 0) x = 0x9E3779B9u;
            for (int i = 0; i < bytes.Length; i++)
            {
                if ((i & 3) == 0) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; }
                bytes[i] ^= (byte)(x >> ((i & 3) * 8));
            }
        }

        static IEnumerator Load(string mod, Pack pack, string name, string key, bool streamed)
        {
            AudioClip clip = null;
            string file = null;
#if DEV
            var own = System.Diagnostics.Stopwatch.StartNew();
            double content = 0.0;
            float began = Time.realtimeSinceStartup, longest = 0f;
            int frameFrom = Time.frameCount;
#endif
            try
            {
                KeyValuePair<long, int> where = pack.Where[name];
                byte[] bytes = new byte[where.Value];
                using (var stream = File.OpenRead(pack.Path))
                {
                    stream.Seek(where.Key, SeekOrigin.Begin);
                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int n = stream.Read(bytes, got, bytes.Length - got);
                        if (n <= 0) throw new EndOfStreamException();
                        got += n;
                    }
                }
                Turn(bytes, name);
                string folder = Path.Combine(Application.temporaryCachePath, "keystone-sounds");
                Directory.CreateDirectory(folder);
                file = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".ogg");
                File.WriteAllBytes(file, bytes);
            }
            catch (Exception problem)
            {
                Debug.LogWarning("[Keystone] " + key + " could not be read from its pack: " + problem.Message);
                file = null;
            }
            if (file != null)
            {
                string address;
                try { address = new Uri(file).AbsoluteUri; }
                catch (Exception) { address = KSPUtil.ApplicationFileProtocol + file; }
                UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(address, AudioType.OGGVORBIS);
                try
                {
                    // (a stream kept packed and unpacked as it plays; the rest unpacked now, whole, ready to play at once and as
                    // often together as is wanted)
                    if (request.downloadHandler is DownloadHandlerAudioClip handler) { handler.compressed = false; handler.streamAudio = streamed; }
#if DEV
                    own.Stop();
#endif
                    yield return request.SendWebRequest();
#if DEV
                    own.Start(); longest = Mathf.Max(longest, Time.unscaledDeltaTime);
                    double contentFrom = own.Elapsed.TotalMilliseconds;
#endif
                    if (!request.isNetworkError && !request.isHttpError)
                    {
                        try { clip = DownloadHandlerAudioClip.GetContent(request); }
                        catch (Exception) { clip = null; }
                    }
#if DEV
                    content = own.Elapsed.TotalMilliseconds - contentFrom;
#endif
                    if (clip != null && streamed) { streams.Add(request); request = null; }
                    if (clip != null)
                    {
                        float until = Time.realtimeSinceStartup + 5f;
                        while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < until)
                        {
#if DEV
                            own.Stop();
#endif
                            yield return null;
#if DEV
                            own.Start(); longest = Mathf.Max(longest, Time.unscaledDeltaTime);
#endif
                        }
                        if (clip.loadState == AudioDataLoadState.Failed || clip.samples <= 0) { UnityEngine.Object.Destroy(clip); clip = null; }
                    }
                }
                finally { if (request != null) request.Dispose(); }
                // (a stream too: what it plays from is in memory, its request's)
                try { File.Delete(file); }
                catch (Exception) { }
            }
#if DEV
            own.Stop();
            loadNotes.Add(key + ": " + (pack.Where.TryGetValue(name, out KeyValuePair<long, int> w) ? (w.Value / 1024) + " kB" + (streamed ? " (streamed)" : "") : "?") + (clip != null ? ", " + clip.length.ToString("F1") + " s, " + clip.channels + " ch" : ", FAILED")
                + "; the game's thread " + own.Elapsed.TotalMilliseconds.ToString("F1") + " ms (of which taking the clip " + content.ToString("F1") + "); "
                + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F0") + " ms over " + (Time.frameCount - frameFrom) + " frames, the longest " + (longest * 1000f).ToString("F0") + " ms; in at frame " + Time.frameCount);
#endif
            Handed(key, clip);
        }

        /// <summary>A sound read (or null: it could not be), kept, and handed to all that asked for it.</summary>
        static void Handed(string key, AudioClip clip)
        {
            if (clip != null)
            {
                clip.name = key;
                // (kept through the game's clearing of what it thinks unused at each scene change)
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                loaded[key] = clip;
            }
            if (!waiting.TryGetValue(key, out List<Action<AudioClip>> those)) return;
            waiting.Remove(key);
            foreach (Action<AudioClip> then in those)
            {
                try { then(clip); }
                catch (Exception problem) { Debug.LogException(problem); }
            }
        }
    }
}
