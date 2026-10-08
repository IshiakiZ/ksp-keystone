using System;
using KSP.UI.Screens;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// The one settings window all the mods share. It opens from a button on the game's toolbar (a keystone)
    /// or with the modifier key and K (Option-K on a Mac, Alt-K elsewhere), in every scene of the game.
    /// Each mod has a page: its settings, drawn from what it registered, and anything more it wants to
    /// draw there itself.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class Host : MonoBehaviour
    {
        public static Host Instance { get; private set; }

        public const float LabelWidth = 150f, NumberWidth = 78f;
        const float Width = 440f;
        const string Lock = "KeystoneWindow", TypingLock = "KeystoneTyping";
        /// <summary>What the name of every box that can be typed in begins with (see Typed): while one of them has the keyboard, the game's own keys are held off.</summary>
        public const string TypedName = "keystone:";
        static readonly int WindowId = "Keystone settings".GetHashCode();

        bool open, locked, typing;
        int page;
        Rect window = new Rect(-1f, 80f, Width, 0f);
        Vector2 scroll;
        float pageHeight;
        string tip = "";
        ApplicationLauncherButton button;
        Texture2D icon;

        static GUIStyle rich, number, small, plain;
        static Texture2D white;

        /// <summary>The window's own ways of writing: for a page that draws something itself. (Only inside a page's drawing.)</summary>
        public static GUIStyle Rich => rich ?? (rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
        public static GUIStyle Number => number ?? (number = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false });
        public static GUIStyle Small => small ?? (small = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 11 });
        /// <summary>(Words kept on one line, however little room there is.)</summary>
        public static GUIStyle Plain => plain ?? (plain = new GUIStyle(GUI.skin.label) { wordWrap = false });

        /// <summary>Whether the window is showing.</summary>
        public bool Open
        {
            get => open;
            set
            {
                open = value;
                if (!open) Unlock();
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
        }

        void OnDestroy()
        {
            foreach (Mod mod in Kit.Mods) mod.SaveIfDue(true);
            Unlock();
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (icon != null) Destroy(icon);
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            foreach (Mod mod in Kit.Mods) mod.SaveIfDue(true);
        }

        void Update()
        {
            for (int n = 0; n < Kit.Mods.Count; n++) Kit.Mods[n].SaveIfDue();
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER) return;
            if (Input.GetKeyDown(KeyCode.K) && GameSettings.MODIFIER_KEY.GetKey() && GUIUtility.keyboardControl == 0) Open = !open;
            if (button == null && Kit.Mods.Count > 0 && ApplicationLauncher.Ready && ApplicationLauncher.Instance != null)
            {
                if (icon == null) icon = Icon();
                button = ApplicationLauncher.Instance.AddModApplication(() => open = true, () => { open = false; Unlock(); }, null, null, null, null, ApplicationLauncher.AppScenes.ALWAYS, icon);
                if (open) button.SetTrue(false);
            }
        }

        void OnGUI()
        {
            if (!open || Kit.Mods.Count == 0) return;
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER) return;
            if (HighLogic.Skin != null) GUI.skin = HighLogic.Skin;
            // (drawn at the size the player has set for the rest of the game's interface)
            float scale = Mathf.Clamp(GameSettings.UI_SCALE, 0.5f, 2.5f);
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float wide = Screen.width / scale, high = Screen.height / scale;
            if (window.x < 0f) window.x = Mathf.Max(0f, 0.5f * (wide - Width) - 140f);
            window.height = 0f;
            window = GUILayout.Window(WindowId, window, Draw, "Mods", GUILayout.Width(Width));
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, wide - window.width));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, high - 40f));
            // While the pointer is over the window, a click or the wheel is for the window and not for what lies under it.
            var pointer = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
            bool over = window.Contains(pointer);
            if (over && !locked) { InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.KSC_ALL | ControlTypes.EDITOR_SOFT_LOCK, Lock); locked = true; }
            else if (!over && locked) Unlock(false);
            // While something is being typed into the window, the keys are for the window: an M must not open the map.
            bool typed = (GUI.GetNameOfFocusedControl() ?? "").StartsWith(TypedName, StringComparison.Ordinal);
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

        void Draw(int id)
        {
            if (page >= Kit.Mods.Count) page = 0;
            if (Kit.Mods.Count > 1)
            {
                var names = new string[Kit.Mods.Count];
                for (int n = 0; n < names.Length; n++) names[n] = Kit.Mods[n].Name;
                page = GUILayout.SelectionGrid(page, names, Mathf.Min(names.Length, 3));
                GUILayout.Space(4f);
            }
            Mod mod = Kit.Mods[page];
            GUILayout.Label("<b>" + mod.Name + "</b>  <size=11><color=#a0a0a0>" + mod.Version + "</color></size>", Rich);
            if (!string.IsNullOrEmpty(mod.About)) GUILayout.Label(mod.About, Small);
            GUILayout.Space(4f);
            // The page is as tall as what is on it; only one too tall for the screen is given a bar to scroll by.
            float room = Mathf.Max(200f, Screen.height / Mathf.Clamp(GameSettings.UI_SCALE, 0.5f, 2.5f) * 0.62f);
            bool scrolls = pageHeight > room;
            if (scrolls) scroll = GUILayout.BeginScrollView(scroll, false, true, GUILayout.Height(room));
            GUILayout.BeginVertical();
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
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) pageHeight = GUILayoutUtility.GetLastRect().height;
            if (scrolls) GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) tip = GUI.tooltip ?? "";
            GUILayout.Label(string.IsNullOrEmpty(tip) ? " " : tip, Small, GUILayout.MinHeight(30f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Defaults", GUILayout.Width(90f))) mod.Reset();
            GUILayout.FlexibleSpace();
            GUILayout.Label("<size=10><color=#909090>Keystone " + Kit.Version + "</color></size>", Rich);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(90f))) Open = false;
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
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
#endif

#if DEV
        /// <summary>For the development build: turn to the next mod's page.</summary>
        public static string Next()
        {
            if (Instance == null || Kit.Mods.Count == 0) return "not running";
            Instance.page = (Instance.page + 1) % Kit.Mods.Count;
            Instance.open = true;
            return Kit.Mods[Instance.page].Name;
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
