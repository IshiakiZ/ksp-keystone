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

        internal override void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Name, GUILayout.Width(Host.LabelWidth));
            Value = GUILayout.SelectionGrid(value, Options, Mathf.Min(Options.Length, 3));
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
        internal override void Draw() => GUILayout.Label("<b>" + Label + "</b>", Host.Rich);
    }
}
