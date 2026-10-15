// F1: overhead stats and hitboxes. Number keys run registered debug actions.
// OnEnable: DebugMenu.Watch(this, "Player", "Health", () => health);
// OnDisable: DebugMenu.RemoveAll(this);  Actions use DebugMenu.Action(...).
using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AscendJigoku.Debugging
{
    public sealed class DebugMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static float DisplayScale => Mathf.Clamp(Screen.height / 720f, 0.65f, 2f);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Watch<T>(UnityEngine.Object owner, string category, string label, Func<T> getter)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            Register(new Entry { Owner = owner, Category = category, Label = label,
                Read = () => Convert.ToString(getter()) });
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Action(UnityEngine.Object owner, string category, string label, int key, System.Action callback)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (key < 1 || key > 9) throw new ArgumentOutOfRangeException(nameof(key), "Use a key from 1 to 9.");
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            Register(new Entry { Owner = owner, Category = category, Label = label, Key = key, Run = callback });
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void RemoveAll(UnityEngine.Object owner)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            entries.RemoveAll(e => ReferenceEquals(e.Owner, owner));
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private sealed class Entry
        {
            public UnityEngine.Object Owner;
            public string Category, Label, Value;
            public Func<string> Read;
            public System.Action Run;
            public int Key;
        }
        private static readonly List<Entry> entries = new List<Entry>();
        private static DebugMenu instance;
        private float nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            instance = null;
            IsOpen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance == null) new GameObject("Debug Menu").AddComponent<DebugMenu>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static void Register(Entry entry)
        {
            if (entry.Owner == null) throw new ArgumentNullException("owner");
            if (string.IsNullOrWhiteSpace(entry.Category) || string.IsNullOrWhiteSpace(entry.Label))
                throw new ArgumentException("Category and label are required.");
            entries.RemoveAll(e => e.Owner == entry.Owner && e.Category == entry.Category && e.Label == entry.Label);
            entries.Add(entry);
            entries.Sort((a, b) => string.CompareOrdinal(a.Category, b.Category));
        }

        private void Update()
        {
            if (Pressed(0))
            {
                IsOpen = !IsOpen;
                if (IsOpen) RefreshValues();
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                entries.RemoveAll(e => e.Owner == null);
                if (IsOpen) RefreshValues();
                nextRefresh = Time.unscaledTime + 0.1f;
            }
            if (!IsOpen || Time.timeScale <= 0) return;
            for (int key = 1; key <= 9; key++)
            {
                if (!Pressed(key)) continue;
                Entry action = null;
                int matches = 0;
                foreach (Entry entry in entries)
                    if (entry.Owner != null && entry.Key == key) { action = entry; matches++; }
                if (matches > 1) { Debug.LogWarning("Debug key " + key + " is assigned more than once."); continue; }
                if (action == null) continue;
                try { action.Run(); }
                catch (Exception exception) { Debug.LogException(exception); }
                RefreshValues();
            }
        }

        private static bool Pressed(int number)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return number == 0 ? keyboard.f1Key.wasPressedThisFrame
                : keyboard[(Key)((int)Key.Digit1 + number - 1)].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(number == 0 ? KeyCode.F1 : (KeyCode)((int)KeyCode.Alpha0 + number));
#else
            return false;
#endif
        }

        private void RefreshValues()
        {
            foreach (Entry entry in entries.ToArray())
            {
                if (entry.Owner == null || entry.Read == null) continue;
                try { entry.Value = entry.Read(); }
                catch (Exception exception) { entry.Value = "Error: " + exception.Message; }
            }
        }

        internal static string Readout(Component entity)
        {
            var text = new System.Text.StringBuilder();
            foreach (Entry entry in entries)
            {
                var component = entry.Owner as Component;
                GameObject owner = component != null ? component.gameObject : entry.Owner as GameObject;
                if (owner != entity.gameObject || entry.Read == null) continue;
                text.Append("\n").Append(entry.Label).Append(": ").Append(entry.Value);
            }
            return text.ToString();
        }

        internal static void DrawBackdrop(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.06f, 0.06f, 0.06f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void OnDisable() { if (instance == this) IsOpen = false; }
        private void OnDestroy() { if (instance == this) { instance = null; IsOpen = false; } }
#endif
    }
}
