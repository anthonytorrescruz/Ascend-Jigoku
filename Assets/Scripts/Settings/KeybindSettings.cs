using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AscendJigoku.Settings
{
    // Keyboard rebinding shared by every PlayerActions instance.
    // Scripts that create their own PlayerActions call Register(controls.asset) once so saved keys apply to them.
    // Arrow keys stay as fixed movement alternates; Escape is reserved for pause/back; gamepad layout is fixed.
    public static class KeybindSettings
    {
        private const string ActionsKey = "keybinds.v1.actions";
        private const string InteractKey = "keybinds.v1.interact";
        private const string DefaultInteractPath = "<Keyboard>/e";
        private const string GamepadInteractPath = "<Gamepad>/buttonNorth";

        public sealed class Entry
        {
            public string Id, Label;
            public InputAction Action;
            public int BindingIndex;
            public bool IsGameplayButton;
            public string DisplayKey => Action.GetBindingDisplayString(BindingIndex);
            public string EffectivePath => Action.bindings[BindingIndex].effectivePath;
        }

        private static PlayerActions master;
        private static InputAction interactTemplate;
        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly List<InputActionAsset> assets = new List<InputActionAsset>();
        private static readonly List<InputAction> interactActions = new List<InputAction>();

        // Bumped whenever bindings change so cached prompt labels can refresh.
        public static int Version { get; private set; }
        public static event Action Changed;

        public static IReadOnlyList<Entry> Entries { get { Init(); return entries; } }
        public static string InteractLabel { get { Init(); return interactTemplate.GetBindingDisplayString(0); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            master = null; interactTemplate = null;
            entries.Clear(); assets.Clear(); interactActions.Clear();
            Version = 0; Changed = null;
        }

        private static void Init()
        {
            if (master != null && master.asset != null) return;
            master = new PlayerActions();
            master.asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            interactTemplate = new InputAction("Interact (settings)", InputActionType.Button, DefaultInteractPath);

            string json = PlayerPrefs.GetString(ActionsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { master.asset.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Debug.LogWarning("Saved keybinds were unreadable and have been reset. " + e.Message); PlayerPrefs.DeleteKey(ActionsKey); }
            }
            string interact = PlayerPrefs.GetString(InteractKey, "");
            if (!string.IsNullOrEmpty(interact)) interactTemplate.ApplyBindingOverride(0, interact);

            entries.Clear();
            AddPart("move_up", "Move up", "Move", "up");
            AddPart("move_down", "Move down / aim down", "Move", "down");
            AddPart("move_left", "Move left", "Move", "left");
            AddPart("move_right", "Move right", "Move", "right");
            AddButton("jump", "Jump", "Jump");
            AddButton("attack", "Attack", "Attack");
            AddButton("dash", "Dash", "Dash");
            AddButton("parry", "Parry", "Parry");
            AddButton("cast", "Shoot (tap) / Heal (hold)", "CastOrHeal");
            entries.Add(new Entry { Id = "interact", Label = "Talk / Interact", Action = interactTemplate, BindingIndex = 0 });
        }

        private static void AddPart(string id, string label, string actionName, string part)
        {
            var action = master.asset.FindAction("PlayerInput/" + actionName, true);
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
                if (bindings[i].isPartOfComposite && string.Equals(bindings[i].name, part, StringComparison.OrdinalIgnoreCase)
                    && bindings[i].path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
                { entries.Add(new Entry { Id = id, Label = label, Action = action, BindingIndex = i }); return; }
            Debug.LogWarning($"Keybinds: no keyboard '{part}' binding on {actionName}.");
        }

        private static void AddButton(string id, string label, string actionName)
        {
            var action = master.asset.FindAction("PlayerInput/" + actionName, true);
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite
                    && bindings[i].path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
                { entries.Add(new Entry { Id = id, Label = label, Action = action, BindingIndex = i, IsGameplayButton = true }); return; }
            Debug.LogWarning($"Keybinds: no keyboard binding on {actionName}.");
        }

        // Call right after `new PlayerActions()`. Safe to call more than once.
        public static void Register(InputActionAsset asset)
        {
            if (asset == null) return;
            Init();
            assets.RemoveAll(a => a == null);
            if (!assets.Contains(asset)) assets.Add(asset);
            ApplyTo(asset);
        }

        // Creates the shop/NPC interact action with the player's chosen key plus Xbox Y.
        public static InputAction CreateInteractAction(string name)
        {
            Init();
            var action = new InputAction(name, InputActionType.Button, interactTemplate.bindings[0].effectivePath);
            action.AddBinding(GamepadInteractPath);
            interactActions.RemoveAll(a => a == null);
            interactActions.Add(action);
            return action;
        }

        public static void ReleaseInteractAction(InputAction action) => interactActions.Remove(action);

        private static void ApplyTo(InputActionAsset asset)
        {
            string json = master.asset.SaveBindingOverridesAsJson();
            if (string.IsNullOrEmpty(json)) asset.RemoveAllBindingOverrides();
            else asset.LoadBindingOverridesFromJson(json);
        }

        private static void SaveAndBroadcast()
        {
            PlayerPrefs.SetString(ActionsKey, master.asset.SaveBindingOverridesAsJson());
            var interactOverride = interactTemplate.bindings[0].overridePath;
            if (string.IsNullOrEmpty(interactOverride)) PlayerPrefs.DeleteKey(InteractKey);
            else PlayerPrefs.SetString(InteractKey, interactOverride);
            PlayerPrefs.Save();

            assets.RemoveAll(a => a == null);
            foreach (var asset in assets) ApplyTo(asset);
            string interactPath = interactTemplate.bindings[0].effectivePath;
            foreach (var action in interactActions) action.ApplyBindingOverride(0, interactPath);
            Version++;
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            Init();
            master.asset.RemoveAllBindingOverrides();
            interactTemplate.RemoveAllBindingOverrides();
            SaveAndBroadcast();
        }

        // Listens for one keyboard key. Escape cancels. A key already used by another entry is swapped onto it.
        // onFinished receives a short status message for the UI.
        public static InputActionRebindingExtensions.RebindingOperation StartRebind(Entry entry, Action<string> onFinished)
        {
            Init();
            string previous = entry.EffectivePath;
            InputActionRebindingExtensions.RebindingOperation operation = null;
            operation = entry.Action.PerformInteractiveRebinding(entry.BindingIndex)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/f1")
                .WithControlsExcluding("<Keyboard>/upArrow")
                .WithControlsExcluding("<Keyboard>/downArrow")
                .WithControlsExcluding("<Keyboard>/leftArrow")
                .WithControlsExcluding("<Keyboard>/rightArrow")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnCancel(op => { op.Dispose(); onFinished?.Invoke(""); })
                .OnComplete(op =>
                {
                    string message = "";
                    foreach (var other in entries)
                    {
                        if (other == entry || !SameKey(other.EffectivePath, entry.EffectivePath)) continue;
                        other.Action.ApplyBindingOverride(other.BindingIndex, previous);
                        message = $"{other.Label} moved to {other.DisplayKey}.";
                    }
                    op.Dispose();
                    SaveAndBroadcast();
                    onFinished?.Invoke(message);
                });
            operation.Start();
            return operation;
        }

        // True while any rebindable gameplay button (not movement) is physically held.
        public static bool AnyGameplayKeyHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Init();
            foreach (var entry in entries)
                if (entry.IsGameplayButton && InputControlPath.TryFindControl(keyboard, entry.EffectivePath) is ButtonControl key && key.isPressed)
                    return true;
            return false;
        }

        private static bool SameKey(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            // Character paths such as "<Keyboard>/#(J)" and key paths such as "<Keyboard>/j" can name the same key.
            var first = InputControlPath.TryFindControl(keyboard, a);
            return first != null && first == InputControlPath.TryFindControl(keyboard, b);
        }
    }
}
