using System;
using System.Collections.Generic;
using System.Text;
using KSP.UI.Screens;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// The one window for mods. It opens from a button on the game's toolbar (a keystone) or with the modifier key
    /// and K (Option-K on a Mac, Alt-K elsewhere; the key can be changed on Keystone's page), in every scene of the
    /// game, on a list of every mod installed: Keystone's own mods first, each with its switch and a line on how it is
    /// doing, then every other mod in GameData with what can be known of it (see Listing). A click on a mod opens its
    /// page: for Keystone's mods their settings, drawn from what each registered, and anything more it draws there
    /// itself; for every mod, who made it, its addresses, what it needs and what needs it, and its folders.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class Host : MonoBehaviour
    {
        public static Host Instance { get; private set; }

        public const float LabelWidth = 150f, NumberWidth = 78f;
        const float Width = 440f, Box = 26f;
        /// <summary>How wide a page's settings may be, beside the bar it scrolls by.</summary>
        internal const float PageWide = Width - 44f;
        const string Lock = "KeystoneWindow", TypingLock = "KeystoneTyping";
        /// <summary>What the name of every box that can be typed in begins with (see Typed): while one of them has the keyboard, the game's own keys are held off.</summary>
        public const string TypedName = "keystone:";
        const string SearchName = TypedName + "search";
        static readonly int WindowId = "Keystone settings".GetHashCode();

        bool open, locked, typing;
        Listed shown;                                    // the mod whose page is open (nothing: the list)
        Listed wanted; bool going;                       // where a click asked to go: gone to at the start of the next drawing (a page must not change half way through one)
        int filter, filterWanted;                        // 0 every mod, 1 those with settings here, 2 those with a newer version out
        string search = "", searchTyped = "";
        Rect window = new Rect(-1f, 80f, Width, 0f);
        Vector2 listScroll, pageScroll;
        float listHigh, pageHigh, room = 300f, chrome = 150f, partShown, drawnHigh;
        bool listScrolls, pageScrolls;
        string tip = "";
        int measure;                                     // begin measuring (1) or end it (-1), in the next Update
        /// <summary>The page of what each mod costs (not a mod: a place in the window).</summary>
        static readonly Listed CostsPage = new Listed { Id = ":costs", Name = "What each mod costs" };
        ApplicationLauncherButton button;
        Texture2D icon;

        static GUIStyle rich, number, small, plain, row, title, faint, link;
        static Texture2D white;

        /// <summary>The window's own ways of writing: for a page that draws something itself. (Only inside a page's drawing.)</summary>
        public static GUIStyle Rich => rich ?? (rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        public static GUIStyle Number => number ?? (number = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false });
        public static GUIStyle Small => small ?? (small = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 11 });
        /// <summary>(Words kept on one line, however little room there is.)</summary>
        public static GUIStyle Plain => plain ?? (plain = new GUIStyle(GUI.skin.label) { wordWrap = false });
        static Texture2D Flat(float grey, float alpha)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, new Color(grey, grey * 1.04f, grey * 1.1f, alpha));
            t.Apply();
            return t;
        }

        /// <summary>A mod's line in the list: dark, so that white words and coloured marks can be read on it, lighter under the pointer.</summary>
        static GUIStyle RowStyle
        {
            get
            {
                if (row == null)
                {
                    row = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true, fixedHeight = 0f, stretchHeight = false };
                    row.padding = new RectOffset(8, 6, 4, 5);
                    row.border = new RectOffset(0, 0, 0, 0);
                    row.margin = new RectOffset(2, 2, 1, 1);
                    row.normal.background = Flat(0.16f, 0.88f); row.hover.background = Flat(0.25f, 0.92f); row.active.background = Flat(0.32f, 0.95f);
                    row.onNormal.background = row.normal.background; row.onHover.background = row.hover.background; row.onActive.background = row.active.background;
                    row.normal.textColor = row.hover.textColor = row.active.textColor = Color.white;
                }
                return row;
            }
        }

        static Texture2D backdrop;
        static GUIStyle Title => title ?? (title = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        static GUIStyle line;
        /// <summary>(Rich, on one line: for short words that must not be broken, as the interface's size changes how wide each letter is reckoned.)</summary>
        static GUIStyle Line => line ?? (line = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false });
        /// <summary>(Not rich: words from elsewhere, a release's notes, shown as they are.)</summary>
        static GUIStyle Faint => faint ?? (faint = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = true, fontSize = 11, normal = { textColor = new Color(0.72f, 0.72f, 0.72f) } });
        static GUIStyle Link => link ?? (link = new GUIStyle(GUI.skin.button) { fontSize = 11, wordWrap = false });

        static float UiScale => Mathf.Clamp(GameSettings.UI_SCALE, 0.5f, 2.5f);

        /// <summary>Whether the window is showing.</summary>
        public bool Open
        {
            get => open;
            set
            {
                bool was = open;
                open = value;
                if (!open) { Unlock(); Hotkey.Choosing = null; if (Meter.Best != null && Meter.Best.Running) measure = -1; }
                if (open && !was) Online.Look(this, false);
                if (button != null)
                {
                    if (open) button.SetTrue(false);
                    else button.SetFalse(false);
                }
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Camera.onPreCull += BeforeDrawing;
            Kit.OnlineSwitch.Changed += OnlineChanged;
        }

        void OnlineChanged()
        {
            if (Kit.OnlineSwitch.Value && open) Online.Look(this, false);
        }

        int filmed = -1;

        /// <summary>Once a frame, before the first camera draws: what the mods have asked of the film is put together and handed on (see Film).</summary>
        void BeforeDrawing(Camera cam)
        {
            if (filmed == Time.frameCount) return;
            filmed = Time.frameCount;
            Film.Apply();
        }

        void OnDestroy()
        {
            Camera.onPreCull -= BeforeDrawing;
            Kit.OnlineSwitch.Changed -= OnlineChanged;
            foreach (Mod mod in Kit.All()) mod.SaveIfDue(true);
            Unlock();
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (icon != null) Destroy(icon);
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            foreach (Mod mod in Kit.All()) mod.SaveIfDue(true);
        }

        void Update()
        {
            for (int n = 0; n < Kit.Mods.Count; n++) Kit.Mods[n].SaveIfDue();
            Kit.Own.SaveIfDue();
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER) return;
            if (Kit.WindowKey.Pressed) Open = !open;
            // (measuring is begun and ended here, outside the window's drawing: setting it up wraps that drawing's own methods too)
            if (measure != 0)
            {
                IMeter meter = Meter.Best;
                try
                {
                    if (meter != null && measure > 0 && open && !meter.Running) meter.Start();
                    else if (meter != null && measure < 0 && meter.Running) meter.Stop();
                }
                catch (Exception ex) { Kit.Log("Keystone", "measuring: " + ex.Message); }
                measure = 0;
            }
            if (button == null && Kit.Mods.Count > 0 && ApplicationLauncher.Ready && ApplicationLauncher.Instance != null)
            {
                if (icon == null) icon = Icon();
                button = ApplicationLauncher.Instance.AddModApplication(() => Open = true, () => Open = false, null, null, null, null, ApplicationLauncher.AppScenes.ALWAYS, icon);
                if (open) button.SetTrue(false);
            }
        }

        void OnGUI()
        {
            if (!open || Kit.Mods.Count == 0) return;
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER) return;
            if (HighLogic.Skin != null) GUI.skin = HighLogic.Skin;
            // A key being chosen for one of the keys on a page: the key pressed is for that, and for nothing else.
            Event e = Event.current;
            if (Hotkey.Choosing != null && e.type == EventType.KeyDown && Hotkey.Choosing.Take(e)) e.Use();
            // (drawn at the size the player has set for the rest of the game's interface)
            float scale = UiScale;
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float wide = Screen.width / scale, high = Screen.height / scale;
            if (window.x < 0f) window.x = Mathf.Max(0f, 0.5f * (wide - Width) - 140f);
            window.height = 0f;
            window = GUILayout.Window(WindowId, window, Draw, "Mods", GUILayout.Width(Width));
            if (e.type == EventType.Repaint) drawnHigh = window.height;
            // (kept on the screen: all of it, where it fits)
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, wide - window.width));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, high - Mathf.Min(window.height, high)));
            // While the pointer is over the window, a click or the wheel is for the window and not for what lies under it.
            var pointer = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
            bool over = window.Contains(pointer);
            if (over && !locked) { InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.KSC_ALL | ControlTypes.EDITOR_SOFT_LOCK, Lock); locked = true; }
            else if (!over && locked) Unlock(false);
            // While something is being typed into the window, or a key chosen, the keys are for the window: an M must not open the map.
            bool typed = (GUI.GetNameOfFocusedControl() ?? "").StartsWith(TypedName, StringComparison.Ordinal) || Hotkey.Choosing != null;
            if (typed && !typing) { InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, TypingLock); typing = true; }
            else if (!typed && typing) { InputLockManager.RemoveControlLock(TypingLock); typing = false; }
            GUI.matrix = before;
        }

        void Unlock(bool all = true)
        {
            if (locked) { InputLockManager.RemoveControlLock(Lock); locked = false; }
            if (all && typing)
            {
                InputLockManager.RemoveControlLock(TypingLock);
                typing = false;
                GUIUtility.keyboardControl = 0;
            }
        }

        /// <summary>Go to a mod's page (nothing: the list), at the start of the next drawing.</summary>
        void Go(Listed one)
        {
            wanted = one;
            going = true;
        }

        void Draw(int id)
        {
            Event e = Event.current;
            // (the game's window is see-through; a little more darkness behind the words, so that what lies under the window does not show through them)
            if (e.type == EventType.Repaint)
            {
                if (backdrop == null) backdrop = Flat(0.03f, 0.5f);
                if (drawnHigh > 30f) GUI.DrawTexture(new Rect(3f, 20f, Width - 6f, drawnHigh - 23f), backdrop);
            }
            if (e.type == EventType.Layout)
            {
                // (what a click asked for is done here, before anything is laid out: the page is then the same from the laying out to the drawing)
                if (going)
                {
                    shown = wanted; going = false;
                    pageScroll = Vector2.zero; pageHigh = 0f;
                    Hotkey.Choosing = null;
                    if (GUI.GetNameOfFocusedControl() != SearchName) GUIUtility.keyboardControl = 0;
                }
                if (shown != null && shown != CostsPage && !Listing.All.Contains(shown)) shown = Find(shown.Id);
                filter = filterWanted;
                search = searchTyped;
                float high = Screen.height / UiScale;
                room = Mathf.Max(110f, Mathf.Min(high * 0.62f, high - 12f - chrome));
            }
            try
            {
                if (shown == null) List(); else if (shown == CostsPage) Costs(); else Page(shown);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { GUILayout.Label(ex.GetType().Name + ": " + ex.Message, Small); }
            if (e.type == EventType.Repaint) tip = GUI.tooltip ?? "";
            GUILayout.Label(string.IsNullOrEmpty(tip) ? " " : tip, Small, GUILayout.MinHeight(30f));
            GUILayout.BeginHorizontal();
            Mod settings = shown == null ? null : shown.Mod ?? (shown.Base ? Kit.Own : null);
            if (settings != null) { if (GUILayout.Button("Defaults", GUILayout.Width(90f))) settings.Reset(); }
            else GUILayout.Space(90f);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=10><color=#909090>Keystone " + Kit.Version + (Kit.WindowKey.Code != KeyCode.None ? "  ·  " + Kit.WindowKey.Shown : "") + "</color></size>", Line);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f))) Open = false;
            GUILayout.EndHorizontal();
            // (how tall everything but the part that scrolls is, from this one drawing: the foot's bottom, less that part)
            if (e.type == EventType.Repaint) chrome = Mathf.Clamp(GUILayoutUtility.GetLastRect().yMax + 12f - partShown, 60f, 500f);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        static Listed Find(string id)
        {
            foreach (Listed one in Listing.All) if (one.Id == id) return one;
            return null;
        }

        // ---------------------------------------------------------------- the list of mods

        /// <summary>The mods the list shows just now, as filtered and searched.</summary>
        List<Listed> Showing()
        {
            var list = new List<Listed>();
            string looked = search.Trim();
            foreach (Listed one in Listing.All)
            {
                if (filter == 1 && one.ModName == null && !one.Base) continue;
                if (filter == 2 && !one.Behind) continue;
                if (looked.Length > 0 && !Matches(one, looked)) continue;
                list.Add(one);
            }
            return list;
        }

        static bool Matches(Listed one, string words)
        {
            foreach (string word in words.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool found = false;
                foreach (string text in new[] { one.Name, one.Author, one.About, one.Id, string.Join(" ", one.Folders.ToArray()) })
                    if (text != null && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) found = true;
                if (!found) return false;
            }
            return true;
        }

        void List()
        {
            IList<Listed> all = Listing.All;
            bool anyNewer = false;
            foreach (Listed one in all) if (one.Behind) anyNewer = true;
            GUILayout.BeginHorizontal();
            SearchBox();
            GUILayout.FlexibleSpace();
            string[] filters = anyNewer ? new[] { "All", "With settings", "Newer" } : new[] { "All", "With settings" };
            // (no newer versions any more: the filter for them goes, and the list is the whole list again)
            if (filter >= filters.Length) filterWanted = 0;
            int chosen = GUILayout.Toolbar(Mathf.Min(filter, filters.Length - 1), filters, GUILayout.Width(anyNewer ? 250f : 190f));
            if (chosen != Mathf.Min(filter, filters.Length - 1)) filterWanted = chosen;
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=11><color=#9a9a9a>" + Listing.Game + "</color></size>", Rich);

            List<Listed> showing = Showing();
            if (Event.current.type == EventType.Layout) listScrolls = listHigh > room + 1f;
            if (listScrolls) listScroll = GUILayout.BeginScrollView(listScroll, false, true, GUILayout.Height(room));
            GUILayout.BeginVertical();
            bool ours = false, theirs = false;
            foreach (Listed one in showing)
            {
                bool mine = one.ModName != null || one.Base;
                if (mine && !ours) { ours = true; Heading("With settings here", "Keystone's mods: the switch is each mod's own, as on its page. Click a mod for its settings."); }
                if (!mine && !theirs) { theirs = true; if (ours) GUILayout.Space(6f); Heading("Every other mod installed", "As each describes itself: in what CKAN recorded when it installed it, in its .version file, and in its libraries. Click one for more."); }
                RowFor(one);
            }
            if (showing.Count == 0) GUILayout.Label(all.Count == 0 ? "No mods found." : "Nothing matches.", Small);
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) listHigh = GUILayoutUtility.GetLastRect().height;
            if (listScrolls) GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) partShown = GUILayoutUtility.GetLastRect().height;
            OnlineLine();
        }

        static void Heading(string words, string hint)
        {
            GUILayout.Label(new GUIContent("<b>" + words + "</b>", hint), Line);
        }

        void SearchBox()
        {
            bool had = GUI.GetNameOfFocusedControl() == SearchName;
            Event e = Event.current;
            if (had && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { searchTyped = ""; GUIUtility.keyboardControl = 0; e.Use(); }
            GUI.SetNextControlName(SearchName);
            searchTyped = GUILayout.TextField(searchTyped, 40, GUILayout.Width(160f));
            if (searchTyped.Length == 0 && !had && e.type == EventType.Repaint)
            {
                Rect at = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(at.x + 6f, at.y, at.width - 6f, at.height), "<size=11><color=#8a8a8a>search</color></size>", Rich);
            }
            if (had && e.type == EventType.MouseDown && !GUILayoutUtility.GetLastRect().Contains(e.mousePosition)) GUIUtility.keyboardControl = 0;
        }

        void RowFor(Listed one)
        {
            Mod mod = one.Mod;
            Toggle on = mod?.Switch;
            GUILayout.BeginHorizontal();
            Rect box = GUILayoutUtility.GetRect(Box, Box, GUILayout.Width(Box), GUILayout.ExpandHeight(true));
            bool clicked = GUILayout.Button(new GUIContent(RowText(one, mod, on), RowHint(one, mod)), RowStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (on != null)
            {
                bool had = on.Value;
                bool now = GUI.Toggle(new Rect(box.x + 3f, box.y + 0.5f * box.height - 10f, Box - 4f, 20f), had, new GUIContent("", on.Hint ?? ""));
                if (now != had) on.Value = now;
            }
            if (clicked) Go(one);
        }

        static string Safe(string text) => (text ?? "").Replace("<", "< ");

        static string RowText(Listed one, Mod mod, Toggle on)
        {
            var s = new StringBuilder();
            bool off = on != null && !on.Value;
            s.Append("<b>").Append(Safe(one.Name)).Append("</b>");
            if (one.Version.Length > 0) s.Append("  <size=11><color=").Append(one.Behind ? "#e0b050" : "#a8b0b8").Append(">").Append(Safe(one.Version)).Append("</color></size>");
            Badges(one, s);
            string second = Second(one, mod, on);
            if (second.Length > 0) s.Append("\n<size=11><color=").Append(off ? "#e8a860" : "#c4cad0").Append(">").Append(second).Append("</color></size>");
            return s.ToString();
        }

        static void Badges(Listed one, StringBuilder s)
        {
            if (one.Behind) s.Append("  <size=11><color=#86d086>newer: ").Append(Safe(one.Latest)).Append("</color></size>");
            if (one.Note.StartsWith("its library did not load", StringComparison.Ordinal)) s.Append("  <size=11><color=#ff8a70>did not load</color></size>");
            foreach (Need need in one.Needs)
                if (need.Met == null && !need.Game) { s.Append("  <size=11><color=#ff8a70>needs ").Append(Safe(need.Name.Replace("|", " or "))).Append("</color></size>"); break; }
            string range = one.OutOfRange;
            if (range.Length > 0) s.Append("  <size=11><color=#e0b050>").Append(range).Append("</color></size>");
            if (one.Library && !one.Base) s.Append("  <size=11><color=#8c8c8c>library</color></size>");
            // (while measuring: what it costs the game's main thread)
            IMeter meter = Meter.Best;
            if (meter != null && meter.Running)
                foreach (Cost cost in meter.Costs)
                    if (cost.Who == one) { s.Append("  <size=11><color=#9fc8ff>").Append(Ms(cost.Ms)).Append(" ms</color></size>"); break; }
        }

        static string Ms(double ms) => ms.ToString(ms < 0.095 ? "F3" : ms < 9.95 ? "F2" : "F1", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The line under a mod's name: how it is doing (Keystone's mods), or what it is.</summary>
        static string Second(Listed one, Mod mod, Toggle on)
        {
            if (mod != null)
            {
                if (on != null && !on.Value) return "off";
                string said = null;
                if (mod.Status != null)
                {
                    try { said = mod.Status(); }
                    catch (Exception ex) { said = ex.GetType().Name; }
                }
                if (!string.IsNullOrEmpty(said)) return Short(Safe(said), 110);
                int changed = mod.Changes;
                return changed == 0 ? "settings as installed" : changed + (changed == 1 ? " setting changed" : " settings changed");
            }
            if (one.Base) return "the base the mods above stand on" + (Kit.Own.Changes > 0 ? "; " + Kit.Own.Changes + (Kit.Own.Changes == 1 ? " setting changed" : " settings changed") : "");
            if (one.About.Length > 0) return Short(Safe(one.About), 110);
            if (one.Note.Length > 0) return Safe(one.Note);
            return one.Folders.Count > 0 && one.Folders[0].Length > 0 ? "in GameData/" + Safe(string.Join(", ", one.Folders.ToArray())) : "";
        }

        static string Short(string text, int most) => text.Length <= most ? text : text.Substring(0, most - 3).TrimEnd() + "...";

        static string RowHint(Listed one, Mod mod)
        {
            string about = mod != null ? mod.About : one.About;
            return (string.IsNullOrEmpty(about) ? one.Name : about) + (one.Author.Length > 0 ? "  (" + one.Author + ")" : "");
        }

        /// <summary>The foot of the list: what the mods cost, and looking online for newer versions, which is the player's to allow.</summary>
        void OnlineLine()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("What each mod costs", "How much of each frame every mod takes, and what each loaded from its folder."), GUILayout.Width(160f))) Go(CostsPage);
            IMeter meter = Meter.Best;
            if (meter != null && meter.Running)
            {
                double all = 0;
                foreach (Cost cost in meter.Costs) all += cost.Ms;
                GUILayout.Label("<size=11><color=#9fc8ff>measuring: mods take " + Ms(all) + " ms of a " + meter.Frame.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ms frame</color></size>", Rich);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            Toggle allowed = Kit.OnlineSwitch;
            GUILayout.BeginHorizontal();
            bool now = GUILayout.Toggle(allowed.Value, new GUIContent(allowed.Label, allowed.Hint));
            if (now != allowed.Value) allowed.Value = now;
            GUILayout.FlexibleSpace();
            if (allowed.Value && GUILayout.Button(new GUIContent(Online.Looking ? "looking" : "Look now", "Ask now for every mod, not only those not looked at today."), GUILayout.Width(90f)) && !Online.Looking) Online.Look(this, true);
            GUILayout.EndHorizontal();
            if (allowed.Value && Online.State.Length > 0) GUILayout.Label("<size=11><color=#9a9a9a>" + Safe(Online.State) + "</color></size>", Rich);
        }

        // ---------------------------------------------------------------- what each mod costs

        static readonly GUILayoutOption NameWide = GUILayout.Width(150f), FigureWide = GUILayout.Width(52f);

        static void Figures(string name, string a, string b, string c, string d, bool heading = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(heading ? "<size=11><color=#9a9a9a>" + name + "</color></size>" : "<size=11>" + name + "</size>", Title, NameWide);
            foreach (string figure in new[] { a, b, c, d })
                GUILayout.Label("<size=11>" + (heading ? "<color=#9a9a9a>" + figure + "</color>" : figure) + "</size>", NumberRich, FigureWide);
            GUILayout.EndHorizontal();
        }

        static GUIStyle numberRich;
        static GUIStyle NumberRich => numberRich ?? (numberRich = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false, richText = true });

        void Costs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("All mods", "Back to the list of every mod."), GUILayout.Width(80f))) Go(null);
            GUILayout.Space(6f);
            GUILayout.Label("<b>What each mod costs</b>", Title, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            if (Event.current.type == EventType.Layout) pageScrolls = pageHigh > room + 1f;
            if (pageScrolls) pageScroll = GUILayout.BeginScrollView(pageScroll, false, true, GUILayout.Height(room));
            GUILayout.BeginVertical();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            IMeter meter = Meter.Best;
            if (meter == null) GUILayout.Label("<color=#e0b050>" + Safe(Meter.WhyNot) + "</color>", Small);
            else
            {
                bool running = meter.Running;
                bool wants = GUILayout.Toggle(measure > 0 || (running && measure == 0), new GUIContent("Measure the game's main thread",
                    "While this is on, every mod's methods that the game or Unity calls (each frame's Update and the rest, its coroutines, what it hangs on events, its patches of the game) are timed. Turning it on holds the game still for a moment while that is set up. It goes off by itself when this window is closed."));
                if (wants && !running && measure == 0) measure = 1;
                if (!wants && running && measure == 0) measure = -1;
                if (running && meter.Frame > 0.01)
                {
                    double all = 0;
                    foreach (Cost cost in meter.Costs) all += cost.Ms;
                    GUILayout.Label("A frame takes " + meter.Frame.ToString("F1", culture) + " ms (" + (1000.0 / meter.Frame).ToString("F0", culture) + " a second). The mods' own work: " + Ms(all) + " ms of it ("
                                    + (100.0 * all / meter.Frame).ToString("F0", culture) + "%). The measuring itself adds about " + Ms(meter.Itself) + " ms.", Small);
                    bool bytes = meter.Costs.Count > 0 && meter.Costs[0].Bytes >= 0;
                    Figures("on average, over 3 s", "ms", "% frame", "worst ms", bytes ? "KB made" : "calls", true);
                    foreach (Cost cost in meter.Costs)
                    {
                        if (cost.Ms < 0.0005 && cost.Calls < 0.01) continue;
                        string name = cost.Who != null ? Safe(cost.Who.Name) : "<color=#9a9a9a>not any mod's</color>";
                        Figures(name, Ms(cost.Ms), (100.0 * cost.Ms / meter.Frame).ToString(cost.Ms / meter.Frame < 0.1 ? "F1" : "F0", culture), Ms(cost.Worst),
                                bytes ? (cost.Bytes / 1024.0).ToString("F1", culture) : cost.Calls.ToString(cost.Calls < 10 ? "F1" : "F0", culture));
                    }
                    GUILayout.Label("<color=#9a9a9a>" + Safe(meter.Report) + ".</color>", Small);
                }
                else if (running || measure > 0) GUILayout.Label("Measuring: the first figures come in a second.", Small);
                GUILayout.Label("<color=#9a9a9a>Only the game's main thread is timed. What a mod does on threads of its own (the explosions' smoke is worked out on one) is not in these figures, and nor is the graphics card's time. What the game itself does for a mod's parts (physics, drawing them) is the game's.</color>", Small);
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Loaded from each mod's folder</b>", Rich);
            IList<Meter.Load> loads = Meter.Loaded;
            if (loads.Count == 0) GUILayout.Label("Nothing.", Small);
            else
            {
                Figures("pictures, models and sounds", "pictures", "models", "sounds", "memory", true);
                long sum = 0;
                foreach (Meter.Load load in loads)
                {
                    sum += load.Bytes;
                    Figures(Safe(load.Who.Name), load.Pictures.ToString(culture), load.Models.ToString(culture), load.Sounds.ToString(culture), Meter.Size(load.Bytes));
                }
                GUILayout.Label("<color=#9a9a9a>" + Meter.Size(sum) + " in all, estimated from each thing's size and form, through the game's own database. What a mod makes for itself while it runs is not counted. Managed memory in use by everything, game and mods together: "
                                + Meter.Size(GC.GetTotalMemory(false)) + ".</color>", Small);
            }
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) pageHigh = GUILayoutUtility.GetLastRect().height;
            if (pageScrolls) GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) partShown = GUILayoutUtility.GetLastRect().height;
        }

        // ---------------------------------------------------------------- a mod's page

        void Page(Listed one)
        {
            Mod mod = one.Mod ?? (one.Base ? Kit.Own : null);
            List<Listed> order = Showing();
            int at = order.IndexOf(one);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("All mods", "Back to the list of every mod."), GUILayout.Width(80f))) Go(null);
            GUILayout.Space(6f);
            // (a long name goes onto a second line rather than pushing the buttons out of the window)
            GUILayout.Label("<b>" + Safe(one.Name) + "</b>  <size=11><color=#a0a0a0>" + Safe(one.Version) + "</color></size>", Title, GUILayout.ExpandWidth(true));
            if (order.Count > 1)
            {
                if (GUILayout.Button(new GUIContent("<", "The mod before this one in the list."), GUILayout.Width(26f))) Go(order[at <= 0 ? order.Count - 1 : at - 1]);
                if (GUILayout.Button(new GUIContent(">", "The mod after this one in the list."), GUILayout.Width(26f))) Go(order[at < 0 || at >= order.Count - 1 ? 0 : at + 1]);
            }
            GUILayout.EndHorizontal();
            string about = mod != null && !one.Base ? mod.About : one.About;
            if (!string.IsNullOrEmpty(about)) GUILayout.Label(Safe(about), Small);
            GUILayout.Space(4f);

            // The page is as tall as what is on it; only one too tall for the screen is given a bar to scroll by.
            if (Event.current.type == EventType.Layout) pageScrolls = pageHigh > room + 1f;
            if (pageScrolls) pageScroll = GUILayout.BeginScrollView(pageScroll, false, true, GUILayout.Height(room));
            GUILayout.BeginVertical();
            if (mod != null)
            {
                for (int n = 0; n < mod.Settings.Count; n++)
                {
                    try { mod.Settings[n].Draw(); }
                    catch (ExitGUIException) { throw; }
                    catch (Exception ex) { GUILayout.Label(mod.Settings[n].Label + ": " + ex.Message); }
                }
                if (mod.Panel != null)
                {
                    try { mod.Panel(); }
                    catch (ExitGUIException) { throw; }
                    catch (Exception ex) { GUILayout.Label(ex.GetType().Name + ": " + ex.Message, Small); }
                }
                GUILayout.Space(8f);
            }
            AboutIt(one, mod != null);
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) pageHigh = GUILayoutUtility.GetLastRect().height;
            if (pageScrolls) GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) partShown = GUILayoutUtility.GetLastRect().height;
        }

        static string Ago(DateTime at)
        {
            TimeSpan since = DateTime.UtcNow - at;
            if (since.TotalMinutes < 2) return "just now";
            if (since.TotalHours < 2) return (int)since.TotalMinutes + " minutes ago";
            if (since.TotalDays < 2) return (int)since.TotalHours + " hours ago";
            return (int)since.TotalDays + " days ago";
        }

        /// <summary>What is known of a mod: who made it, its addresses, what it needs and what needs it, what game it is made for, where it is.</summary>
        void AboutIt(Listed one, bool hasSettings)
        {
            if (hasSettings) GUILayout.Label("<b>About it</b>", Rich);
            var said = new StringBuilder();
            if (one.Author.Length > 0) said.Append("By ").Append(Safe(one.Author)).Append(". ");
            if (one.Licence.Length > 0) said.Append("Licence: ").Append(Safe(one.Licence)).Append(". ");
            if (said.Length > 0) GUILayout.Label(said.ToString(), Small);
            if (one.Note.Length > 0) GUILayout.Label("<color=#e0b050>" + Safe(one.Note) + "</color>", Small);

            if (one.Links.Count > 0)
            {
                var names = new GUIContent[one.Links.Count];
                for (int n = 0; n < names.Length; n++) names[n] = new GUIContent(one.Links[n][0], "Opens " + one.Links[n][1] + " in your web browser.");
                int clicked = GUILayout.SelectionGrid(-1, names, Mathf.Min(names.Length, 4), Link);
                if (clicked >= 0 && clicked < one.Links.Count) Application.OpenURL(one.Links[clicked][1]);
            }

            if (one.Needs.Count > 0)
            {
                var needs = new StringBuilder("Needs: ");
                for (int n = 0; n < one.Needs.Count; n++)
                {
                    Need need = one.Needs[n];
                    if (n > 0) needs.Append(", ");
                    if (need.Met != null) needs.Append(Safe(need.Met.Name)).Append(need.Met.Version.Length > 0 ? " " + Safe(need.Met.Version) : "");
                    else if (need.Game) needs.Append(Safe(need.Name.Replace("-DLC", "")).Replace("MakingHistory", "Making History").Replace("BreakingGround", "Breaking Ground")).Append(" (part of the game)");
                    else needs.Append("<color=#ff8a70>").Append(Safe(need.Name.Replace("|", " or "))).Append(", which is not installed</color>");
                    if (need.Version.Length > 0 && need.Met != null) needs.Append(" (asks for ").Append(Safe(need.Version)).Append(")");
                }
                GUILayout.Label(needs.ToString(), Small);
            }
            if (one.NeededBy.Count > 0)
            {
                var by = new List<string>();
                foreach (Listed other in one.NeededBy) by.Add(Safe(other.Name));
                GUILayout.Label("Needed by: " + string.Join(", ", by.ToArray()), Small);
            }
            if (one.KspMin.Length > 0 || one.KspMax.Length > 0)
            {
                // (a version of the game given as two numbers is any of its kind: "1.12" is 1.12.x)
                string most = one.KspMax.Split('.').Length == 2 ? one.KspMax + ".x" : one.KspMax, least = one.KspMin;
                string range = least == one.KspMax || least.Length == 0 ? most : one.KspMax.Length == 0 ? least + " or later" : least + " to " + most;
                bool outside = one.OutOfRange.Length > 0;
                GUILayout.Label((outside ? "<color=#e0b050>" : "") + "Made for KSP " + Safe(range) + " (this game is " + Listing.GameVersion + ")" + (outside ? "</color>" : ""), Small);
            }
            string how = one.ByCkan ? (one.AsDependency ? "Installed by CKAN, because another mod needed it." : "Installed by CKAN.") : Listing.UsesCkan ? "Not installed by CKAN." : "";
            string where = one.Folders.Count > 0 && (one.Folders.Count > 1 || one.Folders[0].Length > 0) ? "In GameData/" + Safe(string.Join(", GameData/", one.Folders.ToArray())) + ". " : "";
            string libraries = one.Libraries.Count > 0 ? "Libraries: " + Safe(string.Join(", ", one.Libraries.ToArray())) + "." : "";
            if (how.Length + where.Length + libraries.Length > 0) GUILayout.Label("<color=#a8a8a8>" + where + libraries + (how.Length > 0 ? " " + how : "") + "</color>", Small);

            // What was learnt online, if the player allows looking.
            if (one.LatestAt != default(DateTime))
            {
                string newest = one.Behind ? "<color=#86d086>A newer version is out: " + Safe(one.Latest) + "</color>" : "This is the newest version" + (one.Latest.Length > 0 && one.Latest != one.Version ? " (" + Safe(one.Latest) + " online)" : "");
                GUILayout.BeginHorizontal();
                GUILayout.Label(newest + "  <color=#9a9a9a>(" + Safe(one.LatestFrom) + ", " + Ago(one.LatestAt) + ")</color>", Small);
                if (one.LatestPage.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && GUILayout.Button(new GUIContent("Its page", "Opens " + one.LatestPage + " in your web browser."), Link, GUILayout.Width(70f)))
                    Application.OpenURL(one.LatestPage);
                GUILayout.EndHorizontal();
                if (one.LatestNotes.Length > 0) GUILayout.Label(one.LatestNotes, Faint);
            }
            else if (one.Remote.Length > 0 || one.GitHub.Length > 0)
                GUILayout.Label("<color=#9a9a9a>" + (Online.Allowed ? "Not looked for online yet." : "Whether a newer version is out can be looked for online: the switch is at the foot of the list.") + "</color>", Small);
        }

#if DEV
        /// <summary>For the development build: open or close the window, and say what there is in it.</summary>
        public static string Show()
        {
            if (Instance == null) return "not running";
            Instance.Open = !Instance.open;
            string text = (Instance.open ? "open" : "closed") + ", button " + (Instance.button != null ? "on the toolbar" : "not made yet") + ", mods:";
            foreach (Mod mod in Kit.Mods) text += " " + mod.Name + " (" + mod.Settings.Count + " settings)";
            return text;
        }

        /// <summary>For the development build: turn to the next page (from the list, the first mod's).</summary>
        public static string Next()
        {
            if (Instance == null || Kit.Mods.Count == 0) return "not running";
            List<Listed> order = Instance.Showing();
            if (order.Count == 0) return "nothing listed";
            int at = Instance.shown == null ? -1 : order.IndexOf(Instance.shown);
            Instance.Go(order[(at + 1) % order.Count]);
            Instance.Open = true;
            return order[(at + 1) % order.Count].Name;
        }

        /// <summary>For the development build: open a mod's page by its name (nothing, or "list": the list).</summary>
        public static string Page(string name)
        {
            if (Instance == null) return "not running";
            Instance.Open = true;
            if (string.IsNullOrEmpty(name) || name == "list") { Instance.Go(null); return "the list"; }
            if (name == "costs") { Instance.Go(CostsPage); return "what each mod costs"; }
            foreach (Listed one in Listing.All)
                if (string.Equals(one.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(one.Id, name, StringComparison.OrdinalIgnoreCase)) { Instance.Go(one); return one.Name; }
            return "no mod named " + name;
        }

        /// <summary>For the development build: scroll what is shown (the list, or a page) as its bar would: to its foot ("end"), so many
        /// units down (a number), or back to its top (anything else). For films of the window.</summary>
        public static string Scroll(string where)
        {
            if (Instance == null) return "not running";
            float down = where == "end" ? 100000f : float.TryParse(where, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float by) ? by : 0f;
            if (Instance.shown == null) Instance.listScroll = new Vector2(0f, down);
            else Instance.pageScroll = new Vector2(0f, down);
            return (Instance.shown == null ? "the list " : "the page ") + (where == "end" ? "at its foot" : "at " + down);
        }

        /// <summary>For the development build: flick a mod's switch, as the switch beside it in the list does. Says what it is now.</summary>
        public static string Flip(string name)
        {
            Mod mod = Kit.Find(name);
            Toggle on = mod?.Switch;
            if (on == null) return "no switch for " + name;
            on.Value = !on.Value;
            return name + " is " + (on.Value ? "on" : "off");
        }

        /// <summary>For the development build: the list's filter (all, settings, newer) and search ("filter|search").</summary>
        public static string Filter(string what)
        {
            if (Instance == null) return "not running";
            string[] parts = (what ?? "").Split('|');
            Instance.filterWanted = parts[0] == "settings" ? 1 : parts[0] == "newer" ? 2 : 0;
            Instance.searchTyped = parts.Length > 1 ? parts[1] : "";
            return "filter " + Instance.filterWanted + ", search '" + Instance.searchTyped + "'";
        }

        /// <summary>For the development build: the size of the game's interface (as its settings set it, 0.8 to 2), for this run only; says what it was.</summary>
        public static string Scale(string value)
        {
            float was = GameSettings.UI_SCALE;
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float now))
            {
                GameSettings.UI_SCALE = now;
                try { KSP.UI.UIMasterController.Instance.SetScale(now); KSP.UI.UIMasterController.Instance.SetAppScale(GameSettings.UI_SCALE_APPS * now); } catch (Exception) { }
            }
            return "was " + was.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", now " + GameSettings.UI_SCALE.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>For the development build: every mod listed, as text.</summary>
        public static string Describe()
        {
            var s = new StringBuilder(Listing.Game + "\n");
            foreach (Listed one in Listing.All)
            {
                s.Append(one.Name).Append(" [").Append(one.Id).Append("] ").Append(one.Version);
                if (one.ModName != null) s.Append(" (Keystone's: ").Append(Second(one, one.Mod, one.Mod?.Switch)).Append(")");
                if (one.Base) s.Append(" (Keystone)");
                if (one.Library) s.Append(" library");
                if (one.ByCkan) s.Append(one.AsDependency ? " ckan-dependency" : " ckan");
                if (one.Note.Length > 0) s.Append(" note: ").Append(one.Note);
                if (one.Author.Length > 0) s.Append(" by ").Append(one.Author);
                s.Append(" folders: ").Append(string.Join(",", one.Folders.ToArray())).Append(" libs: ").Append(string.Join(",", one.Libraries.ToArray()));
                if (one.Needs.Count > 0)
                {
                    s.Append(" needs:");
                    foreach (Need n in one.Needs) s.Append(" ").Append(n.Name).Append(n.Met != null ? "=" + n.Met.Name : n.Game ? "=game" : "=MISSING");
                }
                if (one.NeededBy.Count > 0) { s.Append(" neededby:"); foreach (Listed b in one.NeededBy) s.Append(" ").Append(b.Name); }
                if (one.KspMin.Length + one.KspMax.Length > 0) s.Append(" ksp ").Append(one.KspMin).Append("..").Append(one.KspMax);
                if (one.OutOfRange.Length > 0) s.Append(" OUT: ").Append(one.OutOfRange);
                if (one.Remote.Length > 0) s.Append(" remote: ").Append(one.Remote);
                if (one.GitHub.Length > 0) s.Append(" github: ").Append(one.GitHub);
                if (one.Links.Count > 0) { s.Append(" links:"); foreach (string[] l in one.Links) s.Append(" ").Append(l[0]); }
                if (one.LatestAt != default(DateTime)) s.Append(" latest: ").Append(one.Latest).Append(" from ").Append(one.LatestFrom).Append(one.Behind ? " BEHIND" : "");
                s.Append("\n");
            }
            return s.ToString();
        }

        /// <summary>For the development build: the window's measures (in the game's interface units).</summary>
        public static string Sizes()
        {
            if (Instance == null) return "not running";
            Host h = Instance;
            return "scale " + UiScale + ", screen " + Screen.width / UiScale + " x " + Screen.height / UiScale + ", window " + h.window + ", drawn high " + h.drawnHigh + ", room " + h.room + ", chrome " + h.chrome
                   + ", part shown " + h.partShown + ", list high " + h.listHigh + " (scrolls " + h.listScrolls + "), page high " + h.pageHigh + " (scrolls " + h.pageScrolls + ")";
        }

        /// <summary>For the development build: make the list again (after a reload).</summary>
        public static string Relist()
        {
            Listing.Again();
            return Listing.All.Count + " mods";
        }

        /// <summary>For the development build: switch measuring on or off ("on", "off"), as the switch on the costs page does (the window must be open for it to begin).</summary>
        public static string Measure(string on)
        {
            if (Instance == null) return "not running";
            if (Meter.Best == null) return "no meter: " + Meter.WhyNot;
            Instance.measure = on == "on" ? 1 : -1;
            return "asked to " + (on == "on" ? "begin" : "end") + " (window " + (Instance.open ? "open" : "closed") + ")";
        }

        /// <summary>For the development build: what the meter says, as text.</summary>
        public static string CostsText()
        {
            IMeter meter = Meter.Best;
            if (meter == null) return "no meter: " + Meter.WhyNot;
            var c = System.Globalization.CultureInfo.InvariantCulture;
            var s = new StringBuilder((meter.Running ? "running" : "not running") + "; frame " + meter.Frame.ToString("F2", c) + " ms; itself " + meter.Itself.ToString("F3", c) + " ms; " + meter.Report + "\n");
            foreach (Cost cost in meter.Costs)
                s.Append(cost.Who != null ? cost.Who.Name : "(no mod)").Append(": ").Append(cost.Ms.ToString("F3", c)).Append(" ms, worst ").Append(cost.Worst.ToString("F2", c))
                 .Append(", calls ").Append(cost.Calls.ToString("F1", c)).Append(cost.Elsewhere > 0 ? ", elsewhere " + cost.Elsewhere.ToString("F1", c) : "").Append(cost.Bytes >= 0 ? ", bytes " + cost.Bytes.ToString("F0", c) : "").Append("\n");
            foreach (Meter.Load load in Meter.Loaded)
                s.Append("loaded ").Append(load.Who.Name).Append(": ").Append(load.Pictures).Append(" pictures ").Append(Meter.Size(load.PictureBytes)).Append(", ").Append(load.Models).Append(" models ").Append(Meter.Size(load.ModelBytes))
                 .Append(", ").Append(load.Sounds).Append(" sounds ").Append(Meter.Size(load.SoundBytes)).Append("\n");
            return s.ToString();
        }
#endif

        // ---- things a page can be drawn with

        /// <summary>A patch of a colour, in the flow of the window.</summary>
        public static void Swatch(Color colour, float width, float height)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Rect where = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            where.y += 3f;
            Color was = GUI.color;
            GUI.color = new Color(colour.r, colour.g, colour.b, 1f);
            GUI.DrawTexture(where, white);
            GUI.color = was;
        }

        /// <summary>A patch of a colour that can be clicked, in the flow of the window. True in the frame it is clicked.</summary>
        public static bool Pick(Color colour, float width, float height)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Rect where = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            where.y += 2f;
            Color was = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(where, white);
            GUI.color = new Color(colour.r, colour.g, colour.b, 1f);
            GUI.DrawTexture(new Rect(where.x + 1f, where.y + 1f, where.width - 2f, where.height - 2f), white);
            GUI.color = was;
            return GUI.Button(where, GUIContent.none, GUIStyle.none);
        }

        static GUIStyle box;
        static Texture2D shades, hues;
        static float shadesOf = -1f, hueKept;
        const int Fine = 64;
        const float ShadesWide = 236f, ShadesHigh = 112f, HuesHigh = 14f;

        /// <summary>
        /// A box to type in, in the flow of the window. Give it a name of its own that begins with TypedName, and
        /// what should be in it; it gives back what is in it now. `entered` is true in the frame the player has
        /// done with it (Return, or a click somewhere else), and `mine` while it has the keyboard.
        /// </summary>
        public static string Typed(string name, string text, float width, out bool entered, out bool mine)
        {
            if (box == null) box = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            Event e = Event.current;
            bool had = GUI.GetNameOfFocusedControl() == name;
            bool done = had && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape || e.character == '\n');
            if (done) e.Use();
            GUI.SetNextControlName(name);
            string now = GUILayout.TextField(text ?? "", 16, box, GUILayout.Width(width));
            if (had && e.type == EventType.MouseDown && !GUILayoutUtility.GetLastRect().Contains(e.mousePosition)) done = true;
            if (done) GUIUtility.keyboardControl = 0;
            mine = !done && GUI.GetNameOfFocusedControl() == name;
            entered = done;
            return now;
        }

        /// <summary>
        /// A colour chosen by eye: a field of every shade of one hue, from grey at the left to the full colour at
        /// the right and from black at the foot to light at the top, with all the hues in a bar under it. Click
        /// or drag in either. (With a name, as for Typed, there is also a box to type the colour into, as the six
        /// letters and figures a web page would give it.) Gives back the colour as changed.
        /// </summary>
        public static Color Mixer(Color colour, string name = null)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            Color.RGBToHSV(colour, out float hue, out float strength, out float light);
            // (a grey, or black, has no hue of its own: the field stays on the hue it was on)
            if (strength < 0.004f || light < 0.004f) hue = hueKept;
            else hueKept = hue;
            if (hues == null)
            {
                hues = new Texture2D(Fine * 4, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < Fine * 4; x++) hues.SetPixel(x, 0, Color.HSVToRGB(x / (Fine * 4f - 1f), 1f, 1f));
                hues.Apply();
            }
            if (shades == null) shades = new Texture2D(Fine, Fine, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            if (Mathf.Abs(shadesOf - hue) > 0.0005f)
            {
                var pixels = new Color[Fine * Fine];
                for (int y = 0; y < Fine; y++)
                    for (int x = 0; x < Fine; x++) pixels[y * Fine + x] = Color.HSVToRGB(hue, x / (Fine - 1f), y / (Fine - 1f));
                shades.SetPixels(pixels);
                shades.Apply();
                shadesOf = hue;
            }

            float h = hue, s = strength, v = light;
            GUILayout.BeginHorizontal();
            GUILayout.Space(18f);
            GUILayout.BeginVertical(GUILayout.Width(ShadesWide));
            Rect field = GUILayoutUtility.GetRect(ShadesWide, ShadesHigh, GUILayout.Width(ShadesWide), GUILayout.Height(ShadesHigh));
            GUILayout.Space(4f);
            Rect bar = GUILayoutUtility.GetRect(ShadesWide, HuesHigh, GUILayout.Width(ShadesWide), GUILayout.Height(HuesHigh));
            GUILayout.EndVertical();
            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            Swatch(colour, 64f, 40f);
            GUILayout.Space(6f);
            string letters = ColorUtility.ToHtmlStringRGB(colour);
            Color asked = colour;
            bool typedIt = false;
            if (name != null)
            {
                string text = Typed(name, typedHex != null && typedFor == name ? typedHex : letters, 64f, out bool entered, out bool mine);
                if (mine) { typedHex = text; typedFor = name; }
                if (typedFor == name && typedHex != null && (entered || !mine))
                {
                    typedIt = ColorUtility.TryParseHtmlString("#" + typedHex.Trim().TrimStart('#'), out asked);
                    typedHex = null; typedFor = null;
                }
            }
            else GUILayout.Label("#" + letters, Small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(field, shades);
                GUI.DrawTexture(bar, hues);
                Ring(new Vector2(field.x + strength * field.width, field.yMax - light * field.height));
                Ring(new Vector2(bar.x + hue * bar.width, bar.center.y));
            }
            Vector2 at;
            if (Held(field, 0x4b53, out at)) { s = Mathf.Clamp01((at.x - field.x) / field.width); v = Mathf.Clamp01((field.yMax - at.y) / field.height); }
            if (Held(bar, 0x4b54, out at)) { h = Mathf.Clamp01((at.x - bar.x) / bar.width); hueKept = h; }
            if (typedIt) { asked.a = colour.a; return asked; }
            if (h == hue && s == strength && v == light) return colour;
            Color mixed = Color.HSVToRGB(h, s, v);
            mixed.a = colour.a;
            return mixed;
        }

        static string typedHex, typedFor;

        /// <summary>Whether the pointer is held down on a place in the window that it was pressed on, and where it is now. (For things that are dragged on.)</summary>
        static bool Held(Rect where, int hint, out Vector2 at)
        {
            int id = GUIUtility.GetControlID(hint, FocusType.Passive, where);
            Event e = Event.current;
            at = e.mousePosition;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !where.Contains(e.mousePosition)) return false;
                    GUIUtility.hotControl = id;
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                    return true;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) return false;
                    e.Use();
                    return true;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    return false;
            }
            return false;
        }

        /// <summary>A small mark round a place: dark outside, light inside, so that it shows on any colour.</summary>
        static void Ring(Vector2 at)
        {
            Color was = GUI.color;
            for (int pass = 0; pass < 2; pass++)
            {
                float half = pass == 0 ? 5f : 4f;
                GUI.color = pass == 0 ? new Color(0f, 0f, 0f, 0.85f) : Color.white;
                GUI.DrawTexture(new Rect(at.x - half, at.y - half, 2f * half, 1f), white);
                GUI.DrawTexture(new Rect(at.x - half, at.y + half - 1f, 2f * half, 1f), white);
                GUI.DrawTexture(new Rect(at.x - half, at.y - half, 1f, 2f * half), white);
                GUI.DrawTexture(new Rect(at.x + half - 1f, at.y - half, 1f, 2f * half), white);
            }
            GUI.color = was;
        }

        /// <summary>One labelled slider, in the flow of the window.</summary>
        public static float Row(string label, float value, float least, float most, string shown = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(18f);
            GUILayout.Label(label, GUILayout.Width(LabelWidth - 18f));
            float now = GUILayout.HorizontalSlider(value, least, most, GUILayout.ExpandWidth(true));
            GUILayout.Label(shown ?? now.ToString("F2"), Number, GUILayout.Width(NumberWidth));
            GUILayout.EndHorizontal();
            return now;
        }

        /// <summary>The toolbar button's picture: a keystone, the wedge at the top of an arch that holds the rest in place.</summary>
        static Texture2D Icon()
        {
            const int size = 38;
            var picture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // a wedge, wider at the top: from 8 pixels each side of the middle at the foot to 15 at the head
                    float up = (y - 5f) / 28f, half = Mathf.Lerp(8f, 15f, up), off = Mathf.Abs(x - 18.5f);
                    bool inside = up >= 0f && up <= 1f && off <= half;
                    bool edge = inside && (off > half - 2f || up < 0.07f || up > 0.93f);
                    pixels[y * size + x] = !inside ? new Color32(0, 0, 0, 0) : edge ? new Color32(40, 44, 48, 255) : new Color32((byte)(150 + 60 * up), (byte)(160 + 60 * up), (byte)(170 + 60 * up), 255);
                }
            picture.SetPixels32(pixels);
            picture.Apply();
            return picture;
        }
    }
}
