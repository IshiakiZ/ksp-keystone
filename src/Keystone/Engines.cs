using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>What a rocket engine burns, as far as what comes out of it goes: its flame, and its smoke.</summary>
    public enum Fuel { None, Kerosene, Hydrogen, Methane, Storable, Solid, Mono, Reactor }

    /// <summary>
    /// What the mods that draw an engine's exhaust all need to know of it, kept in one place so that they agree:
    /// what it burns, where the mouth of each nozzle really is and how wide, and how hard it is burning.
    /// </summary>
    public static class Engines
    {
        /// <summary>
        /// The game has one fuel for every liquid rocket engine, and its engines are plainly not all alike: so
        /// which of the real fuels each of the game's own engines is taken as burning is set down here, by the
        /// engine it was modelled on. (Anyone's config can say otherwise, or say it for other engines: see Told.)
        /// </summary>
        static readonly Dictionary<string, Fuel> Stock = new Dictionary<string, Fuel>
        {
            { "liquidEngine", Fuel.Kerosene }, { "liquidEngine.v2", Fuel.Kerosene }, { "liquidEngine2", Fuel.Kerosene }, { "liquidEngine2.v2", Fuel.Kerosene }, { "liquidEngineMainsail.v2", Fuel.Kerosene },
            { "Size2LFB", Fuel.Kerosene }, { "Size2LFB.v2", Fuel.Kerosene }, { "LiquidEngineKE-1", Fuel.Kerosene }, { "LiquidEngineRK-7", Fuel.Kerosene }, { "LiquidEngineRV-1", Fuel.Kerosene },
            { "SSME", Fuel.Hydrogen }, { "Size3EngineCluster", Fuel.Hydrogen }, { "Size3AdvancedEngine", Fuel.Hydrogen }, { "engineLargeSkipper.v2", Fuel.Hydrogen }, { "LiquidEngineRE-I2", Fuel.Hydrogen },
            { "toroidalAerospike", Fuel.Hydrogen }, { "RAPIER", Fuel.Hydrogen },
            { "liquidEngine3.v2", Fuel.Storable }, { "liquidEngine2-2.v2", Fuel.Storable }, { "liquidEngineMini.v2", Fuel.Storable }, { "microEngine.v2", Fuel.Storable }, { "radialEngineMini.v2", Fuel.Storable },
            { "smallRadialEngine", Fuel.Storable }, { "smallRadialEngine.v2", Fuel.Storable }, { "radialLiquidEngine1-2", Fuel.Storable }, { "LiquidEngineRE-J10", Fuel.Storable }, { "LiquidEngineLV-T91", Fuel.Storable },
            { "LiquidEngineLV-TX87", Fuel.Storable },
        };

        /// <summary>What configs say: ENGINE_FLAMES { ENGINE { part = the part's name; flame = kerosene, hydrogen, methane, storable, solid, mono, reactor, or none } }.</summary>
        static Dictionary<string, Fuel> told;
        static Dictionary<string, Fuel> Told()
        {
            if (told != null) return told;
            told = new Dictionary<string, Fuel>();
            try
            {
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ENGINE_FLAMES"))
                    foreach (ConfigNode one in node.GetNodes("ENGINE"))
                    {
                        string part = (one.GetValue("part") ?? "").Trim().Replace('_', '.'), flame = (one.GetValue("flame") ?? "").Trim();
                        if (part.Length == 0) continue;
                        if (!Enum.TryParse(flame, true, out Fuel fuel)) { Kit.Log("Engines", "A config names a flame \"" + flame + "\" for " + part + ": there is none of that name."); continue; }
                        told[part] = fuel;
                    }
            }
            catch (Exception ex) { Kit.Log("Engines", "The configs' ENGINE_FLAMES could not be read: " + ex.Message); }
            return told;
        }

        /// <summary>
        /// What an engine is taken as burning: what a config says of its part; or by what it takes in, and for
        /// the game's one rocket fuel by which engine it is; or nothing, for the kinds whose exhaust is left to
        /// the game (jets, ion engines, anything unknown).
        /// </summary>
        public static Fuel Burns(ModuleEngines engine, string part)
        {
            if (part != null && Told().TryGetValue(part, out Fuel said)) return said;
            bool fuel = false, oxidizer = false, solid = false, mono = false, hydrogen = false, methane = false, kerosene = false, keeps = false, other = false;
            foreach (Propellant p in engine.propellants)
            {
                switch (p.name)
                {
                    case "LiquidFuel": fuel = true; break;
                    case "Oxidizer": case "LqdOxygen": case "LOX": oxidizer = true; break;
                    case "SolidFuel": case "HTPB": case "PBAN": solid = true; break;
                    case "MonoPropellant": case "Hydrazine": case "HTP": mono = true; break;
                    case "LqdHydrogen": case "LH2": hydrogen = true; break;
                    case "LqdMethane": case "Methane": case "Methalox": methane = true; break;
                    case "Kerosene": case "RP-1": kerosene = true; break;
                    case "Aerozine50": case "MMH": case "UDMH": case "NTO": case "MON3": keeps = true; break;
                    case "ElectricCharge": break;
                    default: other = true; break;                    // (air for a jet, xenon, anything unknown)
                }
            }
            if (other) return Fuel.None;
            if (solid) return Fuel.Solid;
            if (hydrogen) return oxidizer ? Fuel.Hydrogen : Fuel.Reactor;
            if (methane) return Fuel.Methane;
            if (kerosene) return Fuel.Kerosene;
            if (keeps) return Fuel.Storable;
            if (fuel && oxidizer)
            {
                if (part != null && Stock.TryGetValue(part, out Fuel known)) return known;
                // (an engine nobody has named: one made for a vacuum is taken for a small upper-stage engine)
                float vacuum = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(0f) : 300f, sea = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(1f) : 250f;
                return sea < vacuum * 0.6f ? Fuel.Storable : Fuel.Kerosene;
            }
            if (mono && !fuel) return Fuel.Mono;
            if (fuel) return Fuel.Reactor;
            return Fuel.None;
        }

        /// <summary>A nozzle's radius in metres for each root of a kilonewton of thrust, where the model cannot be measured.</summary>
        public const float Rule = 0.0245f;

        /// <summary>
        /// A nozzle's radius, from how hard it pushes (share: its part of the engine's thrust): the push is the
        /// pressure at its mouth times the mouth's area, more or less, so the radius goes as the root of the
        /// thrust; and an engine made for a vacuum (much better there than at sea level) has a wider mouth for
        /// the same push.
        /// </summary>
        public static float Guess(ModuleEngines engine, float share)
        {
            float vacuum = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(0f) : 300f, sea = engine.atmosphereCurve != null ? Mathf.Max(engine.atmosphereCurve.Evaluate(1f), 1f) : 250f;
            float bell = Mathf.Clamp(Mathf.Sqrt(vacuum / sea) / 1.06f, 1f, 2.2f);
            return Mathf.Clamp(Rule * Mathf.Sqrt(Mathf.Max(engine.maxThrust * share, 0.05f)) * bell, 0.03f, 4f);
        }

        static readonly List<Vector3> corners = new List<Vector3>();

        /// <summary>
        /// The mouth of a nozzle, measured from the engine's own model: how far along the jet's line the model
        /// reaches near that line (the rim of the bell), and how wide it is there. The game says where an engine
        /// pushes from, which can be half a metre up inside the bell or below it. Where the model cannot be
        /// read, or gives something a nozzle could not be, this says no, and the guess from the thrust stands.
        /// (whole: for a part that is only a pattern, not in the world, where what is switched off is found by hand.)
        /// </summary>
        public static bool Mouth(Part part, Transform nozzle, IList<Transform> all, float guess, out float radius, out float rim, bool whole = false)
        {
            radius = guess; rim = 0f;
            Vector3 from = nozzle.position, along = nozzle.forward;
            // (what is this nozzle's and not its neighbour's: within reach of its line)
            float reach = guess * 3.4f;
            if (all != null)
                foreach (Transform other in all)
                {
                    // (a nozzle that is switched off belongs to another look of the engine, and is not there)
                    if (other == null || other == nozzle || (!whole && !other.gameObject.activeInHierarchy)) continue;
                    Vector3 to = other.position - from;
                    float apart = (to - along * Vector3.Dot(to, along)).magnitude;
                    if (apart > 0.05f) reach = Mathf.Min(reach, apart * 0.5f);
                }
            var cast = new List<Transform>();
            foreach (ModuleJettison shroud in part.FindModulesImplementing<ModuleJettison>())
            {
                if (shroud == null) continue;
                if (shroud.jettisonTransform != null) cast.Add(shroud.jettisonTransform);
                foreach (string name in (shroud.jettisonName ?? "").Split(','))
                    if (name.Trim().Length > 0) cast.AddRange(part.FindModelTransforms(name.Trim()));
            }
            float far = float.NegativeInfinity;
            var seen = new List<Vector2>();
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(whole))
            {
                Mesh mesh = filter.sharedMesh;
                Renderer drawn = filter.GetComponent<Renderer>();
                if (mesh == null || !mesh.isReadable || drawn == null || !drawn.enabled) continue;
                // (the engine itself is solid: flames, glows and such that others have hung on it are not)
                Material m = drawn.sharedMaterial;
                if (m == null || m.renderQueue >= 2500 || (m.shader != null && m.shader.name.StartsWith("Waterfall", StringComparison.Ordinal))) continue;
                bool shed = false;
                foreach (Transform t in cast) if (filter.transform.IsChildOf(t)) shed = true;
                if (shed) continue;
                if (whole)
                {
                    bool off = false;
                    for (Transform t = filter.transform; t != null && t != part.transform; t = t.parent) if (!t.gameObject.activeSelf) off = true;
                    if (off) continue;
                }
                Matrix4x4 place = filter.transform.localToWorldMatrix;
                mesh.GetVertices(corners);
                for (int i = 0; i < corners.Count; i++)
                {
                    Vector3 at = place.MultiplyPoint3x4(corners[i]) - from;
                    float z = Vector3.Dot(at, along), r = (at - along * z).magnitude;
                    if (r > reach) continue;
                    seen.Add(new Vector2(z, r));
                    if (z > far) far = z;
                }
            }
            corners.Clear();
            if (seen.Count < 8) return false;
            float slab = Mathf.Max(0.02f, guess * 0.1f), widest = 0f;
            foreach (Vector2 one in seen) if (one.x > far - slab && one.y > widest) widest = one.y;
            if (widest < guess * 0.2f || widest > guess * 2.2f || far < -guess * 4f || far > guess * 10f) return false;
            radius = widest; rim = far;
            return true;
        }

        /// <summary>
        /// How hard an engine is burning, 0 to 1: how much it burns of what it could, not how hard it pushes (a
        /// vacuum engine in thick air pushes little, and burns as much as ever).
        /// </summary>
        public static float Running(ModuleEngines e)
        {
            if (!e.EngineIgnited || e.flameout || e.finalThrust <= 0f || e.maxThrust <= 0f) return 0f;
            float best = e.atmosphereCurve != null ? e.atmosphereCurve.Evaluate(0f) : 0f;
            return Mathf.Clamp01(e.finalThrust / e.maxThrust * (best > 0f && e.realIsp > 0f ? best / e.realIsp : 1f));
        }

        /// <summary>How long the flame of a nozzle of this radius is at full burn in thick air, metres: where its smoke begins.</summary>
        public static float Flame(Fuel fuel, float radius) => radius * 44f * (fuel == Fuel.Solid ? 1.15f : fuel == Fuel.Mono ? 0.7f : 1f);
    }
}
