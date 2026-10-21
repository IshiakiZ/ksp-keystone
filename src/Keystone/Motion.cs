using System.Collections.Generic;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// How far the craft have been carried on, for the frame being drawn, between the game's physics steps (Smooth Running, from
    /// its 0.3.0: "each frame at its own moment"). The game draws every frame at the pose its last physics step left; where that mod
    /// is installed and on, just before the flight cameras draw, the models of every craft in physics are carried on to where each
    /// has got to by then (by its own velocity and turning), and the camera with the craft it follows; all are put back after.
    /// Anything another mod draws riding with a craft from the craft's own position (a flame at a nozzle, smoke laid at it) is to be
    /// carried on with it, or it lags its craft by up to a step: six metres at three hundred metres a second, forty in orbit, where
    /// the game moves the world past the craft and not the craft through it. So is anything kept beside the craft flown in space
    /// by an offset from it. Nothing is carried outside drawing: Now is false then, and every Carry gives back what it is given.
    /// </summary>
    public static class Motion
    {
        struct Ride { public Vector3 Ahead, About; public Quaternion Turn; }
        static readonly Dictionary<Vessel, Ride> rides = new Dictionary<Vessel, Ride>();
        static int frame = -1;

        /// <summary>Whether craft are carried on in the frame being drawn now.</summary>
        public static bool Now => frame == Time.frameCount;

        /// <summary>How long after the last physics step the frame being drawn stands (seconds; 0 when nothing is carried).</summary>
        public static float Since { get; private set; }

        /// <summary>How far the craft flown has been carried on, in the scene (metres).</summary>
        public static Vector3 Ahead { get; private set; }
        /// <summary>How it has been turned, about About.</summary>
        public static Quaternion Turn { get; private set; } = Quaternion.identity;
        /// <summary>Its centre of mass at its last physics step, which it was turned about.</summary>
        public static Vector3 About { get; private set; }

        /// <summary>Where a point riding with the craft flown (given as it stands at the craft's last physics step) is drawn this frame.</summary>
        public static Vector3 Carry(Vector3 p) => Now ? About + Turn * (p - About) + Ahead : p;

        /// <summary>A direction riding with the craft flown, turned as the craft is this frame.</summary>
        public static Vector3 CarryWay(Vector3 d) => Now ? Turn * d : d;

        /// <summary>Where a point riding with the craft v (as it stands at v's last physics step) is drawn this frame.</summary>
        public static Vector3 CarryWith(Vessel v, Vector3 p)
        {
            if (!Now || v == null || !rides.TryGetValue(v, out Ride r)) return p;
            return r.About + r.Turn * (p - r.About) + r.Ahead;
        }

        /// <summary>A way of standing riding with the craft v, turned as v is this frame.</summary>
        public static Quaternion TurnWith(Vessel v, Quaternion q)
        {
            if (!Now || v == null || !rides.TryGetValue(v, out Ride r)) return q;
            return r.Turn * q;
        }

        /// <summary>How far the craft v has been carried on this frame (zero if it is not).</summary>
        public static Vector3 AheadOf(Vessel v)
        {
            if (!Now || v == null || !rides.TryGetValue(v, out Ride r)) return Vector3.zero;
            return r.Ahead;
        }

        static int keptFrame = -1;

        /// <summary>
        /// How far the game's moving frame has gone on this frame. Once a craft flown goes faster than five metres a second, the
        /// game holds it still in the scene and moves the world past it instead, a step at a time; whatever moves with the craft's
        /// frame and not with the world (a thing shifted with the craft when the game recentres the scene, but not with the world)
        /// and is moved every frame by its owner, not in physics steps, is carried on by this to stay where it is against the craft.
        /// </summary>
        public static Vector3 FrameAhead { get; private set; }

        static readonly List<Transform> frameRiders = new List<Transform>();

        /// <summary>
        /// For a mod with something drawn that moves with the game's moving frame, every frame (see FrameAhead): its root, carried on
        /// by FrameAhead before the cameras draw and put back after, by the mod that carries the craft. Given once; let go of when
        /// destroyed.
        /// </summary>
        public static void RideWithFrame(Transform t) { if (t != null && !frameRiders.Contains(t)) frameRiders.Add(t); }

        /// <summary>For the mod that carries the craft on: what rides with the game's moving frame (destroyed ones are dropped here).</summary>
        public static List<Transform> FrameRiders { get { frameRiders.RemoveAll(t => t == null); return frameRiders; } }

        /// <summary>For the mod that carries the craft on: how far the moving frame has gone this frame.</summary>
        public static void SetFrame(Vector3 ahead, float since, int at)
        {
            if (frame != at) { rides.Clear(); frame = at; Ahead = Vector3.zero; Turn = Quaternion.identity; }
            FrameAhead = ahead; Since = since;
        }

        /// <summary>
        /// For a mod that holds the flight camera still in the world this frame (on a tripod on the ground, say): the camera is then
        /// not carried on with the craft it was following. Called each frame it holds it, before the cameras draw.
        /// </summary>
        public static void KeepCamera() => keptFrame = Time.frameCount;

        /// <summary>Whether a mod holds the flight camera still in the world this frame.</summary>
        public static bool CameraKept => keptFrame == Time.frameCount;

        /// <summary>For the mod that carries the craft on: what it has done to one craft this frame (the craft flown also as Ahead, Turn and About).</summary>
        public static void Set(Vessel v, Vector3 ahead, Quaternion turn, Vector3 about, float since, int at)
        {
            if (frame != at) { rides.Clear(); frame = at; Ahead = Vector3.zero; Turn = Quaternion.identity; }
            Since = since;
            rides[v] = new Ride { Ahead = ahead, Turn = turn, About = about };
            if (v == FlightGlobals.ActiveVessel) { Ahead = ahead; Turn = turn; About = about; }
        }

        /// <summary>For the mod that carries the craft on: put back.</summary>
        public static void Clear()
        {
            if (frame < 0) return;
            rides.Clear();
            Ahead = Vector3.zero; Turn = Quaternion.identity; FrameAhead = Vector3.zero; Since = 0f; frame = -1;
        }
    }
}
