using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>What kind of cloud a layer is.</summary>
    public enum CloudKind
    {
        None = 0,
        Cumulus,          // heaped fair-weather cloud, flat-bottomed, in separate heads
        Cumulonimbus,     // the thunderstorm cloud, from low down to an anvil at the top
        Stratocumulus,    // a low lumpy sheet, often with gaps
        Stratus,          // a low grey sheet with no shape
        Nimbostratus,     // a thick dark sheet that rains or snows steadily
        Altocumulus,      // middle-level, in rolls or patches
        Altostratus,      // a middle-level grey veil the sun shows through as through ground glass
        Cirrus,           // high, thin, made of ice, in wisps
        Fog,              // cloud lying on the ground
        Dust,             // dust raised by the wind (Duna's storms)
    }

    /// <summary>One layer of cloud over a place.</summary>
    public struct CloudLayer
    {
        public CloudKind Kind;
        /// <summary>How much of the sky it covers, 0 to 1, taken over a few kilometres round.</summary>
        public float Cover;
        /// <summary>Its base and its top, in metres above sea level.</summary>
        public float Base, Top;
        /// <summary>How much water it holds, 0 to 1: nothing a thin white cloud, 1 a dark one heavy with rain.</summary>
        public float Water;
    }

    /// <summary>The weather at one place and moment.</summary>
    public struct WeatherHere
    {
        /// <summary>The low clouds (up to about 2 km above the ground: cumulus, stratus, the base of a storm), the middle ones and the high ones.</summary>
        public CloudLayer Low, Middle, High;
        /// <summary>What falls at the ground, 0 to 1: 0.1 drizzle, 0.3 steady rain, 0.6 heavy, 1 a cloudburst (about 50 mm an hour).</summary>
        public float Rain;
        /// <summary>The share of it that falls as snow, 0 to 1.</summary>
        public float Snow;
        /// <summary>How stormy, 0 to 1: thunderstorms within about twenty kilometres, and how strong.</summary>
        public float Storm;
        /// <summary>Fog at the ground, 0 to 1.</summary>
        public float Fog;
        /// <summary>Dust in the air, 0 to 1 (Duna's storms).</summary>
        public float Dust;
        /// <summary>How far one can see along the ground, in metres.</summary>
        public float Seeing;
        /// <summary>The wind ten metres above the ground: east, north, up, in metres a second, without the gusts.</summary>
        public Vector3d Wind;
        /// <summary>How gusty: the typical size of a gust there (one standard deviation), metres a second.</summary>
        public float Gusts;
        /// <summary>How the low clouds move over the place: east, north, up, metres a second.</summary>
        public Vector3d Drift;
        /// <summary>The temperature at the ground, kelvin, as the game has it (0 where no weather mod answers).</summary>
        public float Temperature;

        /// <summary>How much of the sky is covered by all the layers together, 0 to 1.</summary>
        public float Cover => 1f - (1f - Low.Cover) * (1f - Middle.Cover) * (1f - High.Cover);
    }

    /// <summary>A storm, or a shower: a cell of heaped cloud that grows, rains and dies within an hour or so.</summary>
    public struct WeatherCell
    {
        /// <summary>The same number for the same cell all its life.</summary>
        public int Id;
        /// <summary>Its middle, in degrees.</summary>
        public double Latitude, Longitude;
        /// <summary>How far its rain reaches from the middle, metres.</summary>
        public float Radius;
        /// <summary>Its base and its top (the anvil's), metres above sea level.</summary>
        public float Base, Top;
        /// <summary>How far through its life it is, 0 to 1 (strongest from about 0.3 to 0.7).</summary>
        public float Life;
        /// <summary>How strong it is now, 0 to 1.</summary>
        public float Strength;
        /// <summary>How hard it rains in its middle now, 0 to 1.</summary>
        public float Rain;
        /// <summary>A thunderstorm (it makes lightning), or a shower (it does not).</summary>
        public bool Thunder;
        /// <summary>How it moves: east, north, up, metres a second.</summary>
        public Vector3d Drift;
    }

    /// <summary>A flash of lightning.</summary>
    public struct WeatherFlash
    {
        public int Id;
        public CelestialBody Body;
        /// <summary>Where its channel is, in degrees (for a flash to the ground, where it strikes).</summary>
        public double Latitude, Longitude;
        /// <summary>The bottom and top of the channel, metres above sea level.</summary>
        public float Bottom, Top;
        /// <summary>To the ground; otherwise within or between the clouds.</summary>
        public bool Ground;
        /// <summary>When it begins (the game's universal time, seconds).</summary>
        public double Time;
        /// <summary>How bright, 1 an ordinary flash.</summary>
        public float Brightness;
        /// <summary>How many strokes down the same channel (each one a flicker), 1 to 6.</summary>
        public int Strokes;
        /// <summary>For its shape: the same seed makes the same crooked channel.</summary>
        public int Seed;
    }

    /// <summary>How the clouds change the light at a place, for whatever lights the scene.</summary>
    public struct WeatherShade
    {
        /// <summary>How much of the sun's direct light gets through the clouds between the place and the sun, 0 to 1.</summary>
        public float Sun;
        /// <summary>The sky's light against a clear sky's: 1 clear; more under a bright, thin overcast (the sunlight spread out over the whole sky); less under a dark one.</summary>
        public float Sky;
        /// <summary>How far the sky's light has lost its own colour to the grey-white of cloud, 0 to 1.</summary>
        public float Grey;
        /// <summary>
        /// Whether the weather mod dims the sun itself just now (where it draws clouds of its own, they dim the sun's strength by
        /// the cloud really in the way as they are drawn): then a mod that lights the scene must not dim the sun again, but should
        /// still count Sun for the light the clouds spread over the sky and for how much an eye or a camera opens for it.
        /// </summary>
        public bool SunDimmed;

        public static readonly WeatherShade Clear = new WeatherShade { Sun = 1f, Sky = 1f, Grey = 0f };
    }

    /// <summary>
    /// What a weather mod answers. One at a time: the first to set itself as Weather.Provider. At, Cloud and Cells may be
    /// called from any thread (so a mod can fill a picture of the weather on a thread of its own: Cloud costs about a
    /// tenth of a millisecond); Wind and Shade take positions in world space, which move with the game's floating origin,
    /// so ask them on the game's thread (elsewhere they answer as for a still, clear day).
    /// </summary>
    public interface IWeather
    {
        /// <summary>The mod's name, for what readouts say.</summary>
        string Name { get; }
        /// <summary>The weather at a place (degrees) and time.</summary>
        WeatherHere At(CelestialBody body, double latitude, double longitude, double ut);
        /// <summary>The wind at a point in the game's world space, gusts and all: a vector in world space, metres a second.</summary>
        Vector3d Wind(CelestialBody body, Vector3d world, double ut);
        /// <summary>How much cloud there is at a point (degrees, metres above sea level), 0 to 1: the pattern the clouds are drawn from, heads of cumulus, gaps in a sheet, down to about a kilometre.</summary>
        float Cloud(CelestialBody body, double latitude, double longitude, double altitude, double ut);
        /// <summary>The storms and showers within a distance (metres) of a place, added to the list; gives back how many.</summary>
        int Cells(CelestialBody body, double latitude, double longitude, double radius, double ut, List<WeatherCell> into);
        /// <summary>How the clouds change the light at a point in world space, for a sun in the given direction (from the point, world space).</summary>
        WeatherShade Shade(CelestialBody body, Vector3d world, Vector3d toSun, double ut);
    }

    /// <summary>
    /// The weather, as every mod sees it (since Keystone 0.7). A weather mod sets itself as the Provider; anything that
    /// draws clouds, lights the scene, flies a craft or blows smoke asks here, and so all of them agree. Without a
    /// weather mod the answers are those of a still, clear day, and nothing needs to check.
    ///
    /// Coordinates: latitude and longitude in degrees as the game gives them, heights in metres above sea level, times
    /// in the game's universal time (seconds), positions and winds in world space where it says so.
    /// </summary>
    public static class Weather
    {
        static IWeather provider;

        /// <summary>The mod that answers, or null.</summary>
        public static IWeather Provider
        {
            get => provider;
            set { provider = value; Kit.Log("Keystone", value != null ? "the weather comes from " + value.Name : "no weather"); }
        }

        /// <summary>Whether a weather mod is answering.</summary>
        public static bool There => provider != null;

        /// <summary>A still, clear day (its temperature unknown: 0).</summary>
        public static WeatherHere Clear(CelestialBody body)
        {
            return new WeatherHere { Seeing = 40000f };
        }

        static bool complained;
        static void Complain(Exception ex)
        {
            if (complained) return;
            complained = true;
            Kit.Log("Keystone", "the weather failed (said once): " + ex);
        }

        /// <summary>The weather at a place and time (see IWeather.At).</summary>
        public static WeatherHere At(CelestialBody body, double latitude, double longitude, double ut)
        {
            IWeather p = provider;
            if (p != null && body != null) { try { return p.At(body, latitude, longitude, ut); } catch (Exception ex) { Complain(ex); } }
            return Clear(body);
        }

        /// <summary>The weather where a point in world space is, now (on the game's thread only).</summary>
        public static WeatherHere At(CelestialBody body, Vector3d world)
        {
            if (body == null) return Clear(null);
            return At(body, body.GetLatitude(world), body.GetLongitude(world), Planetarium.GetUniversalTime());
        }

        /// <summary>The wind at a point in world space, gusts and all, world space, metres a second (see IWeather.Wind).</summary>
        public static Vector3d Wind(CelestialBody body, Vector3d world, double ut)
        {
            IWeather p = provider;
            if (p != null && body != null) { try { return p.Wind(body, world, ut); } catch (Exception ex) { Complain(ex); } }
            return Vector3d.zero;
        }

        /// <summary>How much cloud there is at a point, 0 to 1 (see IWeather.Cloud).</summary>
        public static float Cloud(CelestialBody body, double latitude, double longitude, double altitude, double ut)
        {
            IWeather p = provider;
            if (p != null && body != null) { try { return p.Cloud(body, latitude, longitude, altitude, ut); } catch (Exception ex) { Complain(ex); } }
            return 0f;
        }

        /// <summary>The storms and showers near a place (see IWeather.Cells).</summary>
        public static int Cells(CelestialBody body, double latitude, double longitude, double radius, double ut, List<WeatherCell> into)
        {
            IWeather p = provider;
            if (p != null && body != null && into != null) { try { return p.Cells(body, latitude, longitude, radius, ut, into); } catch (Exception ex) { Complain(ex); } }
            return 0;
        }

        /// <summary>How the clouds change the light at a point (see IWeather.Shade).</summary>
        public static WeatherShade Shade(CelestialBody body, Vector3d world, Vector3d toSun, double ut)
        {
            IWeather p = provider;
            if (p != null && body != null) { try { return p.Shade(body, world, toSun, ut); } catch (Exception ex) { Complain(ex); } }
            return WeatherShade.Clear;
        }

        /// <summary>
        /// Raised on the game's thread when a flash of lightning begins (by the weather mod, through Flash), so that what
        /// draws clouds can light them from inside and what lights the scene can flash it. Handlers must be instance methods.
        /// </summary>
        public static event Action<WeatherFlash> Flashed;

        /// <summary>The flashes of the last half minute, oldest first.</summary>
        public static IList<WeatherFlash> Recent => recent;
        static readonly List<WeatherFlash> recent = new List<WeatherFlash>();

        /// <summary>For the weather mod: a flash has begun.</summary>
        public static void Flash(WeatherFlash flash)
        {
            recent.Add(flash);
            while (recent.Count > 0 && (recent.Count > 64 || flash.Time - recent[0].Time > 30.0)) recent.RemoveAt(0);
            Action<WeatherFlash> all = Flashed;
            if (all == null) return;
            foreach (Delegate one in all.GetInvocationList())
            {
                try { ((Action<WeatherFlash>)one)(flash); }
                catch (Exception ex) { Complain(ex); }
            }
        }
    }
}
