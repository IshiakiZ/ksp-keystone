using System;
using UnityEngine;

namespace Keystone
{
    /// <summary>
    /// (Since 0.10.0.) A window of a mod's own beside Keystone's, for more than a page holds: charts, maps, long tables. It is
    /// drawn as Keystone's window is, in the game's own look at the size the player has set for the game's interface and a little
    /// darker behind, with a close mark at its top right; carried by its top edge, kept on the screen, and while the pointer is
    /// over it a click or the wheel is for it, not for the camera, the space centre or the editor under it. It goes with the scene
    /// it was opened in. The mod draws what is in it, given the room inside (from 0, 0), with the game's GUI and GUILayout and
    /// Keystone's styles (Host.Rich, Host.Small ...).
    /// </summary>
    public sealed class Sheet
    {
        public readonly string Title;
        /// <summary>How large it is, in the game's interface units (as Keystone's window is).</summary>
        public float Width, Height;
        /// <summary>How dark it is behind what is drawn in it, 0 to 1 (the game's window is see-through; Keystone's own is 0.5).</summary>
        public float Shade = 0.5f;
        /// <summary>Said when it is shut, by its close mark, by the mod, or with its scene.</summary>
        public event Action Closed;

        readonly Action<Rect> draw;
        readonly int id;
        Rect place = new Rect(-1f, -1f, 0f, 0f);
        Runner runner;

        public Sheet(string title, float width, float height, Action<Rect> draw)
        {
            Title = title ?? "";
            Width = width; Height = height;
            this.draw = draw;
            id = 0x4b530000 ^ (Title.GetHashCode() & 0xffff);
        }

        /// <summary>Whether it is open; set it to open or shut it.</summary>
        public bool Open
        {
            get => runner != null;
            set
            {
                if (value == (runner != null)) return;
                if (value)
                {
                    var holder = new GameObject("Keystone sheet: " + Title);
                    runner = holder.AddComponent<Runner>();
                    runner.Sheet = this;
                }
                else UnityEngine.Object.Destroy(runner.gameObject);
            }
        }

        void Gone(Runner r)
        {
            if (runner != r) return;
            runner = null;
            try { Closed?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        static float Scale => Mathf.Clamp(GameSettings.UI_SCALE, 0.5f, 2.5f);

        /// <summary>Where the pointer is, in the game's interface units from the top left of the screen.</summary>
        static Vector2 PointerAt() => new Vector2(Input.mousePosition.x / Scale, (Screen.height - Input.mousePosition.y) / Scale);

        static Texture2D backdrop;

        /// <summary>What draws it and holds the pointer for it: a component of its own, made as the sheet opens and gone as it shuts.</summary>
        sealed class Runner : MonoBehaviour
        {
            internal Sheet Sheet;
            string lockName;
            bool carried, shut;
            Vector2 carriedBy;
            float errorAt = -10f;

            void Awake() { lockName = "KeystoneSheet" + GetInstanceID(); }

            void OnDestroy()
            {
                InputLockManager.RemoveControlLock(lockName);
                if (Sheet != null) Sheet.Gone(this);
            }

            bool Loading => HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER;

            /// <summary>
            /// Once a frame, from where it was last drawn: carried by the pointer while its top is held, and the camera's hold on
            /// a click and the wheel taken while the pointer is over it (asked of the game itself, not remembered: the game lets
            /// every hold go at times, from its pause menu for one). As Keystone's window does it (Host.Pointer), for the same
            /// reasons: done once a frame, not in each event of the drawing.
            /// </summary>
            void Update()
            {
                if (shut) { Destroy(gameObject); return; }
                Sheet s = Sheet;
                if (s == null || Loading) return;
                Vector2 pointer = PointerAt();
                if (carried && !Input.GetMouseButton(0)) carried = false;
                float wide = Screen.width / Scale, high = Screen.height / Scale;
                if (carried)
                {
                    s.place.x = Mathf.Clamp(pointer.x - carriedBy.x, 0f, Mathf.Max(0f, wide - s.Width));
                    s.place.y = Mathf.Clamp(pointer.y - carriedBy.y, 0f, Mathf.Max(0f, high - s.Height));
                }
                bool over = carried || (s.place.x >= 0f && new Rect(s.place.x, s.place.y, s.Width, s.Height).Contains(pointer));
                bool held = InputLockManager.lockStack.ContainsKey(lockName);
                if (over && !held) InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.KSC_ALL | ControlTypes.EDITOR_SOFT_LOCK, lockName);
                else if (!over && held) InputLockManager.RemoveControlLock(lockName);
            }

            void OnGUI()
            {
                Sheet s = Sheet;
                if (s == null || shut || Loading) return;
                if (HighLogic.Skin != null) GUI.skin = HighLogic.Skin;
                float scale = Scale;
                Matrix4x4 before = GUI.matrix;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float wide = Screen.width / scale, high = Screen.height / scale;
                // (first opened in the middle of the screen; then kept on it, all of it, where it fits)
                if (s.place.x < 0f) { s.place.x = Mathf.Max(0f, 0.5f * (wide - s.Width)); s.place.y = Mathf.Max(0f, 0.5f * (high - s.Height)); }
                s.place.x = Mathf.Clamp(s.place.x, 0f, Mathf.Max(0f, wide - s.Width));
                s.place.y = Mathf.Clamp(s.place.y, 0f, Mathf.Max(0f, high - s.Height));
                s.place.width = s.Width; s.place.height = s.Height;
                // (a window of a fixed size, laid out by hand: Keystone's own had to learn that a window laid out by GUILayout
                // that is moved by the game blinks; this one is carried by Update, never by GUI.DragWindow)
                GUI.Window(s.id, s.place, Inside, s.Title);
                GUI.matrix = before;
            }

            void Inside(int windowId)
            {
                Sheet s = Sheet;
                Event e = Event.current;
                if (e.type == EventType.Repaint)
                {
                    if (backdrop == null)
                    {
                        backdrop = new Texture2D(1, 1, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
                        backdrop.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.03f, 1f));
                        backdrop.Apply();
                    }
                    Color was = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s.Shade));
                    GUI.DrawTexture(new Rect(3f, 20f, s.Width - 6f, s.Height - 23f), backdrop);
                    GUI.color = was;
                }
                if (GUI.Button(new Rect(s.Width - 26f, 2f, 22f, 17f), new GUIContent("×", "Close"))) { shut = true; return; }
                if (e.type == EventType.MouseDown && e.button == 0 && new Rect(0f, 0f, s.Width - 30f, 22f).Contains(e.mousePosition))
                {
                    carriedBy = PointerAt() - new Vector2(s.place.x, s.place.y);
                    carried = true;
                    e.Use();
                }
                GUILayout.BeginArea(new Rect(8f, 24f, s.Width - 16f, s.Height - 30f));
                try { s.draw?.Invoke(new Rect(0f, 0f, s.Width - 16f, s.Height - 30f)); }
                catch (Exception ex)
                {
                    // (an error in a mod's drawing said once a few seconds, not in every event: the game draws many a frame)
                    if (Time.realtimeSinceStartup - errorAt > 5f) { errorAt = Time.realtimeSinceStartup; Debug.LogException(ex); }
                }
                finally { GUILayout.EndArea(); }
            }
        }
    }
}
