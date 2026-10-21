using System;

namespace Keystone
{
    /// <summary>A mod that draws the sea with waves of its own, and can say where its water stands (Natural Ocean).</summary>
    public interface IWater
    {
        /// <summary>The mod's name, for the window.</summary>
        string Name { get; }
        /// <summary>How high the water stands at a place over the sea's level (metres): its waves there, as it draws them.</summary>
        float HeightAt(Vector3d world);
    }

    /// <summary>
    /// Where the water stands, for every mod: a craft's splash, a wreck sinking, drops off a wave, all where the sea is drawn and
    /// not where the game's flat sea would be. A sea mod sets itself as Provider; the others ask here, and with none installed
    /// the water is the sea's own level. (Until 2026-10-18 each mod looked Natural Ocean up by name on its own.)
    /// </summary>
    public static class Water
    {
        /// <summary>The mod that draws the sea, if one is installed and has said so.</summary>
        public static IWater Provider;

        /// <summary>How high the water stands at a place over the sea's level, metres (0 with no sea mod: the game's own flat sea).</summary>
        public static float HeightAt(Vector3d world)
        {
            IWater p = Provider;
            if (p == null) return 0f;
            try { return p.HeightAt(world); }
            catch (Exception) { return 0f; }
        }

        /// <summary>How far under the water a place is, metres (less than nought: over it); far over it where the world has no sea.</summary>
        public static float Depth(CelestialBody body, Vector3d world)
        {
            if (body == null || !body.ocean) return -1e9f;
            return HeightAt(world) - (float)((world - body.position).magnitude - body.Radius);
        }
    }
}
