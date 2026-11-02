using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace AscendJigoku.Gameplay
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ActiveInputPrompts : MonoBehaviour
    {
        public bool UsesController { get; private set; }
        private bool stickWasMoved;
        private PlayerActions bindings;
        private Gamepad activeGamepad;
        private string controlsText;
        private bool controlsWereController;
        private readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private void Awake() => bindings = new PlayerActions();
        private void OnDestroy() => bindings?.Dispose();
        public string Interact => UsesController ? "Y" : "E";
        public string Confirm => UsesController ? "A" : "Enter / Space";
        public string Back => UsesController ? "B" : "Esc";
        public string Controls
        {
            get
            {
                if (controlsText == null || controlsWereController != UsesController)
                {
                    controlsWereController = UsesController;
                    controlsText = $"{(UsesController ? "Stick/D-pad" : "WASD/Arrows")}: move/aim   {Button("Jump")}: jump   {Button("Attack")}: attack   {Button("Dash")}: dash   {Button("Parry")}: parry   {Button("CastOrHeal")}: tap shoot / hold heal   {Interact}: talk   {(UsesController ? "Menu" : "Esc")}: pause";
                }
                return controlsText;
            }
        }
        public string Button(string action)
        {
            if (bindings == null) bindings = new PlayerActions();
            string key = (UsesController ? "pad:" : "key:") + action;
            if (labels.TryGetValue(key, out string label)) return label;
            label = ReadButton(action); labels[key] = label; return label;
        }
        private string ReadButton(string action)
        {
            foreach (var binding in bindings.asset.FindAction("PlayerInput/" + action, true).bindings)
            {
                string path = binding.effectivePath;
                if (string.IsNullOrEmpty(path) || !path.StartsWith(UsesController ? "<Gamepad>" : "<Keyboard>")) continue;
                switch (path)
                {
                    case "<Gamepad>/buttonSouth": return "A";
                    case "<Gamepad>/buttonEast": return "B";
                    case "<Gamepad>/buttonWest": return "X";
                    case "<Gamepad>/buttonNorth": return "Y";
                    case "<Gamepad>/leftShoulder": return "LB";
                    case "<Gamepad>/rightShoulder": return "RB";
                    case "<Keyboard>/semicolon": return ";";
                }
                return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            return action;
        }
        private void Update()
        {
            bool moved = false;
            foreach (var g in Gamepad.all)
            {
                bool thisMoved = g.leftStick.ReadValue().sqrMagnitude > 0.16f || g.dpad.ReadValue().sqrMagnitude > 0.1f;
                moved |= thisMoved;
                if (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame
                    || g.buttonWest.wasPressedThisFrame || g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame
                    || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame || (thisMoved && !stickWasMoved))
                { UsesController = true; activeGamepad = g; }
            }
            if (moved && !stickWasMoved) UsesController = true;
            stickWasMoved = moved;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) UsesController = false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) UsesController = false;
            if (activeGamepad == null || !activeGamepad.added || !activeGamepad.enabled) UsesController = false;
        }
    }
}
