using System;
using System.Globalization;
using UnityEngine;

namespace Keystone
{
    /// <summary>One thing that can be set about a mod: what it is called in the settings file and in the window, and how it is drawn there.</summary>
    public abstract class Setting
    {
        public readonly string Key, Label, Hint;
        internal Mod Owner;

        /// <summary>The player changed it (or it was put back to what it starts as).</summary>
        public event Action Changed;

        protected Setting(string key, string label, string hint) { Key = key; Label = label; Hint = hint; }

        protected void Touch()
        {
            Owner?.Touched();
            Changed?.Invoke();
        }

        internal abstract void Read(string text);
        internal abstract string Write();
        internal abstract void Reset();
        /// <summary>Whether it is as it is when first installed.</summary>
        internal abstract bool AsInstalled { get; }
        /// <summary>Its row in the window.</summary>
        internal abstract void Draw();

        protected GUIContent Name => new GUIContent(Label, Hint ?? "");
        protected static readonly CultureInfo Plain = CultureInfo.InvariantCulture;
    }

    public sealed class Toggle : Setting
    {
        readonly bool first;
        bool value;

        internal Toggle(string key, string label, string hint, bool value) : base(key, label, hint) { first = this.value = value; }

        public bool Value
        {
            get => value;
            set { if (value == this.value) return; this.value = value; Touch(); }
        }

        public static implicit operator bool(Toggle toggle) => toggle.value;

        internal override void Read(string text) => value = bool.Parse(text);
        internal override string Write() => value ? "True" : "False";
        internal override void Reset() { if (value != first) { value = first; Touch(); } }
        internal override bool AsInstalled => value == first;
        internal override void Draw() => Value = GUILayout.Toggle(value, Name);
    }

    public sealed class Slider : Setting
    {
        public readonly float Least, Most;
        readonly float first;
        readonly string unit, format;
        float value;

        internal Slider(string key, string label, string hint, float value, float least, float most, string unit, int decimals) : base(key, label, hint)
        {
            Least = least; Most = most; this.unit = unit ?? "";
            format = "F" + Mathf.Clamp(decimals, 0, 4);
            first = this.value = Mathf.Clamp(value, least, most);
            step = Mathf.Pow(10f, -Mathf.Clamp(decimals, 0, 4));
        }

        readonly float step;

        public float Value
        {
            get => value;
            set
            {
                // (kept to the steps it is shown in: what the window says is then exactly what is in use)
                value = Mathf.Clamp(Mathf.Round(value / step) * step, Least, Most);
                if (value == this.value) return;
                this.value = value;
                Touch();
            }
        }

        public static implicit operator float(Slider slider) => slider.value;

        internal override void Read(string text) => value = Mathf.Clamp(float.Parse(text, Plain), Least, Most);
        internal override string Write() => value.ToString(Plain);
        internal override void Reset() { if (value != first) { value = first; Touch(); } }
        // (as shown: a value read from a file may carry more decimals than the window shows)
        internal override bool AsInstalled => Mathf.Abs(value - first) < step * 0.5f;

        string typing;                                            // what is being typed into its box, while something is

        internal override void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Name, GUILayout.Width(Host.LabelWidth));
            float slid = GUILayout.HorizontalSlider(value, Least, Most, GUILayout.ExpandWidth(true));
            if (slid != value) { typing = null; Value = slid; }
            // The figure can be typed as well as slid to. (What is being typed is left as typed until the player has
            // done with it: "1." on the way to "1.5" must not be tidied back to "1".)
            string now = Host.Typed(Host.TypedName + Owner.Name + ":" + Key, typing ?? value.ToString(format, Plain), 50f, out bool entered, out bool mine);
            if (mine) typing = now;
            if (typing != null && (entered || !mine))
            {
                if (float.TryParse(typing.Trim().Replace(',', '.'), NumberStyles.Float, Plain, out float asked) && !float.IsNaN(asked)) Value = asked;
                typing = null;
            }
            GUILayout.Label(unit, Host.Plain, GUILayout.MinWidth(Host.NumberWidth - 22f));
            GUILayout.EndHorizontal();
        }
    }

    public sealed class Choice : Setting
    {
        public readonly string[] Options;
        readonly int first;
        int value;

        internal Choice(string key, string label, string hint, int value, string[] options) : base(key, label, hint)
        {
            Options = options;
            first = this.value = Mathf.Clamp(value, 0, options.Length - 1);
        }

        public int Value
        {
            get => value;
            set { value = Mathf.Clamp(value, 0, Options.Length - 1); if (value == this.value) return; this.value = value; Touch(); }
        }

        public static implicit operator int(Choice choice) => choice.value;

        // (kept by name, so that a later version can add to the list or put it in another order)
        internal override void Read(string text)
        {
            int at = Array.IndexOf(Options, text);
            if (at >= 0) value = at;
        }

        internal override string Write() => Options[value];
        internal override void Reset() { if (value != first) { value = first; Touch(); } }
        internal override bool AsInstalled => value == first;

        int columns;

        internal override void Draw()
        {
            // (as many to a row as fit beside the label: long names one or two to a row, so that the page is never made wider than the window)
            if (columns == 0)
            {
                float widest = 0f;
                foreach (string option in Options) widest = Mathf.Max(widest, GUI.skin.button.CalcSize(new GUIContent(option)).x);
                columns = Mathf.Clamp(Mathf.FloorToInt(Host.PageWide - Host.LabelWidth) / Mathf.Max(1, Mathf.CeilToInt(widest + 6f)), 1, Mathf.Min(Options.Length, 3));
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(Name, GUILayout.Width(Host.LabelWidth));
            Value = GUILayout.SelectionGrid(value, Options, columns, GUILayout.MaxWidth(Host.PageWide - Host.LabelWidth));
            GUILayout.EndHorizontal();
        }
    }

    public sealed class Tint : Setting
    {
        readonly Color first;
        Color value;
        bool open;

        internal Tint(string key, string label, string hint, Color value) : base(key, label, hint) { first = this.value = value; }

        public Color Value
        {
            get => value;
            set { if (value == this.value) return; this.value = value; Touch(); }
        }

        public static implicit operator Color(Tint tint) => tint.value;

        internal override void Read(string text)
        {
            string[] parts = text.Split(',');
            value = new Color(float.Parse(parts[0], Plain), float.Parse(parts[1], Plain), float.Parse(parts[2], Plain), parts.Length > 3 ? float.Parse(parts[3], Plain) : 1f);
        }

        internal override string Write() => value.r.ToString("F3", Plain) + "," + value.g.ToString("F3", Plain) + "," + value.b.ToString("F3", Plain) + "," + value.a.ToString("F3", Plain);
        internal override void Reset() { if (value != first) { value = first; Touch(); } }
        // (as written to the file: three decimals)
        internal override bool AsInstalled => Mathf.Abs(value.r - first.r) < 0.0006f && Mathf.Abs(value.g - first.g) < 0.0006f && Mathf.Abs(value.b - first.b) < 0.0006f && Mathf.Abs(value.a - first.a) < 0.0006f;

        internal override void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Name, GUILayout.Width(Host.LabelWidth));
            // (the patch of colour itself opens the field to choose from, and closes it again)
            if (Host.Pick(value, 46f, 18f)) open = !open;
            if (GUILayout.Button(open ? "done" : "change", GUILayout.Width(64f))) open = !open;
            GUILayout.EndHorizontal();
            if (open) Value = Host.Mixer(value, Host.TypedName + Owner.Name + ":" + Key + ":colour");
        }
    }

    /// <summary>Not a setting: a line of words that heads the settings after it.</summary>
    public sealed class Heading : Setting
    {
        internal Heading(string label) : base(null, label, null) { }
        internal override void Read(string text) { }
        internal override string Write() => "";
        internal override void Reset() { }
        internal override bool AsInstalled => true;
        internal override void Draw() => GUILayout.Label("<b>" + Label + "</b>", Host.Rich);
    }

    /// <summary>
    /// A key the mod listens for, which the player can change on the mod's page: one key, alone or with the game's
    /// modifier key (Option on a Mac, Alt elsewhere, or whatever the game's own settings make it). Ask it every
    /// frame, in Update: Pressed is true in the frame the key goes down, Held for as long as it is down.
    ///
    /// Neither is ever true while the player is typing (in a box of this window or one of the game's own) or is
    /// choosing a key in this window. A key bound with the modifier answers only with the modifier held, and one bound
    /// without it only without it, so that K and Option-K can be two things.
    /// </summary>
    public sealed class Hotkey : Setting
    {
        readonly KeyCode firstCode;
        readonly bool firstModifier;
        KeyCode code;
        bool modifier;

        internal Hotkey(string key, string label, string hint, KeyCode code, bool modifier) : base(key, label, hint)
        {
            firstCode = this.code = code;
            firstModifier = this.modifier = modifier && code != KeyCode.None;
        }

        /// <summary>The key (KeyCode.None: no key at all; the player has taken it off).</summary>
        public KeyCode Code => code;
        /// <summary>Whether the game's modifier key is to be held with it.</summary>
        public bool Modifier => modifier;

        /// <summary>Change it, as the player would.</summary>
        public void Set(KeyCode code, bool modifier)
        {
            if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6) return;            // (the mouse's buttons are for the window and the game)
            modifier = modifier && code != KeyCode.None;
            if (code == this.code && modifier == this.modifier) return;
            this.code = code; this.modifier = modifier;
            Touch();
        }

        /// <summary>True in the frame the key goes down.</summary>
        public bool Pressed => code != KeyCode.None && Free && Input.GetKeyDown(code) && GameSettings.MODIFIER_KEY.GetKey() == modifier;
        /// <summary>True for as long as the key is held down.</summary>
        public bool Held => code != KeyCode.None && Free && Input.GetKey(code) && GameSettings.MODIFIER_KEY.GetKey() == modifier;

        /// <summary>The key as the player would say it, for a hint or a line on the screen: "Option K", "Page Up", "none".</summary>
        public string Shown => code == KeyCode.None ? "none" : (modifier ? ModifierName + " " : "") + Say(code);

        /// <summary>The game's modifier key, as it is called on this computer.</summary>
        public static string ModifierName
        {
            get
            {
                KeyCode held = GameSettings.MODIFIER_KEY != null && GameSettings.MODIFIER_KEY.primary != null ? GameSettings.MODIFIER_KEY.primary.code : KeyCode.LeftAlt;
                switch (held)
                {
                    case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: return Application.platform == RuntimePlatform.OSXPlayer ? "Option" : "Alt";
                    case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                    case KeyCode.LeftCommand: case KeyCode.RightCommand: return Application.platform == RuntimePlatform.OSXPlayer ? "Cmd" : "Windows";
                    case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                    default: return Say(held);
                }
            }
        }

        /// <summary>A key's name as it is printed on the key, more or less.</summary>
        public static string Say(KeyCode code)
        {
            if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9) return ((int)(code - KeyCode.Alpha0)).ToString(Plain);
            if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9) return "Keypad " + (int)(code - KeyCode.Keypad0);
            switch (code)
            {
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Return: return "Enter";
            }
            // (PageUp: "Page Up")
            string name = code.ToString();
            var spaced = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) spaced.Append(' ');
                spaced.Append(name[i]);
            }
            return spaced.ToString();
        }

        /// <summary>Not while the player is typing, or choosing a key.</summary>
        static bool Free => Choosing == null && !Typing;

        /// <summary>
        /// Whether the player is typing just now: into a box drawn the way this window is drawn (by any mod), or into one of
        /// the game's own (a craft's name, a search). (The game's KEYBOARDINPUT lock is no guide: it takes in the camera's
        /// lock, which this window and the game set whenever the pointer is over a window.)
        /// </summary>
        public static bool Typing
        {
            get
            {
                if (GUIUtility.keyboardControl != 0) return true;
                try
                {
                    UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
                    GameObject chosen = events != null ? events.currentSelectedGameObject : null;
                    if (chosen == null) return false;
                    TMPro.TMP_InputField box = chosen.GetComponent<TMPro.TMP_InputField>();
                    if (box != null && box.isFocused) return true;
                    UnityEngine.UI.InputField old = chosen.GetComponent<UnityEngine.UI.InputField>();
                    return old != null && old.isFocused;
                }
                catch (Exception) { return false; }
            }
        }

        /// <summary>The key the player is choosing a new key for just now, in the window (nothing: none).</summary>
        internal static Hotkey Choosing;

        internal override void Read(string text)
        {
            // ("K", "modifier+K", "None")
            string[] parts = text.Split('+');
            var read = (KeyCode)Enum.Parse(typeof(KeyCode), parts[parts.Length - 1].Trim(), true);
            bool held = false;
            for (int i = 0; i < parts.Length - 1; i++) if (parts[i].Trim().Equals("modifier", StringComparison.OrdinalIgnoreCase)) held = true;
            code = read; modifier = held && read != KeyCode.None;
        }

        internal override string Write() => (modifier ? "modifier+" : "") + code;
        internal override void Reset() { if (code != firstCode || modifier != firstModifier) { code = firstCode; modifier = firstModifier; Touch(); } }
        internal override bool AsInstalled => code == firstCode && modifier == firstModifier;

        /// <summary>The key the player has just pressed, while this one is being chosen: taken, unless it is one that cannot be (true: it was used).</summary>
        internal bool Take(Event e)
        {
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return false;
            switch (e.keyCode)
            {
                // (the modifier keys themselves are waited past: the key that comes with them is the one)
                case KeyCode.LeftAlt: case KeyCode.RightAlt: case KeyCode.AltGr: case KeyCode.LeftShift: case KeyCode.RightShift:
                case KeyCode.LeftControl: case KeyCode.RightControl: case KeyCode.LeftCommand: case KeyCode.RightCommand: case KeyCode.CapsLock: case KeyCode.Numlock:
                    return false;
                case KeyCode.Escape:
                    break;                                       // (as it was)
                case KeyCode.Backspace: case KeyCode.Delete:
                    Set(KeyCode.None, false);
                    break;
                default:
                    Set(e.keyCode, GameSettings.MODIFIER_KEY.GetKey());
                    break;
            }
            Choosing = null;
            return true;
        }

        internal override void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Name, GUILayout.Width(Host.LabelWidth));
            bool choosing = Choosing == this;
            if (GUILayout.Button(new GUIContent(choosing ? "press a key" : Shown, choosing ? "Press the key, with " + ModifierName + " held if it is to be with it. Esc: leave it as it was; Backspace: no key." : Hint ?? ""), GUILayout.Width(120f)))
                Choosing = choosing ? null : this;
            string clash = Clash();
            if (clash != null) GUILayout.Label("<color=#e0a040>also " + clash + "</color>", Host.Small);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>Another key of any mod's that is the same as this one, in words (nothing if there is none).</summary>
        internal string Clash()
        {
            if (code == KeyCode.None) return null;
            foreach (Mod mod in Kit.All())
                foreach (Setting setting in mod.Settings)
                    if (setting != this && setting is Hotkey other && other.code == code && other.modifier == modifier)
                        return (mod == Owner ? "" : mod.Name + ": ") + other.Label;
            return null;
        }
    }
}
