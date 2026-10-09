using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// What the film in a camera, and the way it is developed, do to the picture once the game has drawn
    /// it: how bright it comes out, what happens to what is brighter than white, how strong its contrast and
    /// its colours are, and the glow round whatever is very bright. A mod works these out from its own
    /// settings and asks for them every frame (see Film).
    /// </summary>
    public struct Grade
    {
        /// <summary>How much brighter or darker the whole picture comes out, in stops: +1 is twice as bright, -1 half.</summary>
        public float exposure;
        /// <summary>
        /// That exposure is set by hand, as a camera's is by its aperture, shutter and film: it is then the exposure, and
        /// what any other mod asks for (an eye that follows the view, say) is not added to it.
        /// </summary>
        public bool byHand;
        /// <summary>
        /// How what is brighter than white is brought back into the picture: 0 not at all (it is cut off flat, as the game
        /// does it), 1 gently, 2 as film does it (a shoulder that the bright end rolls over, a toe for the dark end, and
        /// more contrast between).
        /// </summary>
        public int curve;
        /// <summary>Contrast and strength of colour, each -1 to 1, 0 leaving it as it is.</summary>
        public float contrast, colour;
        /// <summary>From cold (-1) to warm (1).</summary>
        public float warmth;
        /// <summary>A tint for the dark end of the picture and one for the bright end (white: none).</summary>
        public Color dark, bright;
        /// <summary>The glow round what is bright: how strong (0 none), how bright a thing must be to have one (1 is white), and how far it spreads (1 to 10).</summary>
        public float glow, glowFrom, glowWide;
        public Color glowTint;
        /// <summary>The lens's marks, as for a Look: darker corners, and colours parting towards the corners (0 to 1).</summary>
        public float corners, fringe;
        /// <summary>The darkening of creases, and of where one thing meets another (0 none to 1).</summary>
        public float nooks;
        /// <summary>How grainy the picture is, 0 to 1 (as for a Look: the grainier of the two is had).</summary>
        public float grain;
        /// <summary>
        /// The picture pushed towards two tones, as a poster or a drawing in ink is: 0 not at all, 1 nothing but the two,
        /// parted at posterAt (0 to 1: what is brighter than that goes to the bright tone). The tones are inkDark and
        /// inkBright (black and white unless said). With negative, the two are changed over. (Ask for colour at -1 with it,
        /// or the picture's own colours show between the two.)
        /// </summary>
        public float poster, posterAt;
        public Color inkDark, inkBright;
        public bool negative;

        /// <summary>The picture as the game draws it.</summary>
        public static Grade None => new Grade { dark = Color.white, bright = Color.white, glowTint = Color.white, glowFrom = 1f, glowWide = 7f, posterAt = 0.5f, inkDark = Color.black, inkBright = Color.white };

        /// <summary>Two together: one mod's exposure on top of another's look.</summary>
        public static Grade Both(Grade a, Grade b) => new Grade
        {
            exposure = a.byHand && !b.byHand ? a.exposure : b.byHand && !a.byHand ? b.exposure : a.exposure + b.exposure,
            byHand = a.byHand || b.byHand,
            curve = Mathf.Max(a.curve, b.curve),
            contrast = Mathf.Clamp(a.contrast + b.contrast, -1f, 1f),
            colour = Mathf.Clamp(a.colour + b.colour, -1f, 1f),
            warmth = Mathf.Clamp(a.warmth + b.warmth, -1f, 1f),
            dark = a.dark * b.dark,
            bright = a.bright * b.bright,
            glow = Mathf.Max(a.glow, b.glow),
            glowFrom = a.glow >= b.glow ? a.glowFrom : b.glowFrom,
            glowWide = a.glow >= b.glow ? a.glowWide : b.glowWide,
            glowTint = a.glow >= b.glow ? a.glowTint : b.glowTint,
            corners = Mathf.Max(a.corners, b.corners),
            fringe = Mathf.Max(a.fringe, b.fringe),
            nooks = Mathf.Max(a.nooks, b.nooks),
            grain = Mathf.Max(a.grain, b.grain),
            poster = Mathf.Max(a.poster, b.poster),
            posterAt = a.poster >= b.poster ? a.posterAt : b.posterAt,
            inkDark = a.poster >= b.poster ? a.inkDark : b.inkDark,
            inkBright = a.poster >= b.poster ? a.inkBright : b.inkBright,
            negative = a.poster >= b.poster ? a.negative : b.negative
        };
    }

    /// <summary>Something that can give the game's picture a Grade.</summary>
    public interface IFilm
    {
        /// <summary>What does it, for telling the player.</summary>
        string Name { get; }
        /// <summary>Whether it can, as things are now.</summary>
        bool Works { get; }
        /// <summary>If it cannot, why, in words for the player.</summary>
        string Why { get; }
        /// <summary>Whether the game is keeping more than white in its picture (without that, nothing brighter than white is left to bring back, and exposure can only be roughly had).</summary>
        bool Wide { get; }
        void Use(Grade grade);
        void Off();
    }

    /// <summary>
    /// The film every mod on this base can put in the game's camera. More than one mod may ask at once (one
    /// for a look, another for an exposure): what they ask for is put together.
    ///
    /// The base has no way of its own to do this yet. Another mod's is used where one is installed and has
    /// offered it: TUFX, through this base's companion for it, which is Unity's own post-processing.
    /// </summary>
    public static class Film
    {
        /// <summary>A way of doing it, if some mod that is installed has offered one (see KeystoneTUFX).</summary>
        public static IFilm Other;

        /// <summary>The one to use, or nothing.</summary>
        public static IFilm Best => Other != null && Other.Works ? Other : null;

        /// <summary>Why there is none, in words for the player (nothing if there is one).</summary>
        public static string WhyNot => Best != null ? null : Other == null ? "Nothing installed can do it: the TUFX mod can." : Other.Why;

        sealed class Wish { public Grade Grade; public int Frame; }
        static readonly Dictionary<string, Wish> wishes = new Dictionary<string, Wish>();
        static bool used;

        /// <summary>Ask for a grade, in a mod's name: every frame for as long as it is wanted.</summary>
        public static void Ask(string who, Grade grade)
        {
            if (!wishes.TryGetValue(who, out Wish wish)) wishes[who] = wish = new Wish();
            wish.Grade = grade; wish.Frame = Time.frameCount;
        }

        /// <summary>(Called by the base once a frame, before the first camera draws.)</summary>
        internal static void Apply()
        {
            IFilm film = Best;
            Grade all = Grade.None;
            bool any = false;
            foreach (Wish wish in wishes.Values)
            {
                if (Time.frameCount - wish.Frame > 2) continue;
                all = any ? Grade.Both(all, wish.Grade) : wish.Grade;
                any = true;
            }
            if (film == null) { used = false; return; }
            if (any) { film.Use(all); used = true; }
            else if (used) { film.Off(); used = false; }
        }
    }
}
