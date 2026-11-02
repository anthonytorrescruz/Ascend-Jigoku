using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AscendJigoku.Gameplay
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerControlLock : MonoBehaviour
    {
        private Behaviour[] controls;
        private bool[] wasEnabled;
        private Coroutine release;
        public bool IsLocked { get; private set; }
        private void Awake()
        {
            controls = new Behaviour[] { GetComponent<PlayerController>(), GetComponent<PlayerCombat>(), GetComponent<PlayerParry>(), GetComponent<PlayerSpell>() };
            wasEnabled = new bool[controls.Length];
        }
        public void Lock()
        {
            if (release != null) { StopCoroutine(release); release = null; }
            if (IsLocked) return;
            IsLocked = true;
            GetComponent<PlayerController>().SuspendMovement();
            GetComponent<CombatAttack>()?.CancelAll();
            for (int i = 0; i < controls.Length; i++)
                if (controls[i] != null) { wasEnabled[i] = controls[i].enabled; controls[i].enabled = false; }
        }
        public void Unlock()
        { if (IsLocked && release == null) release = StartCoroutine(ReleaseWhenIdle()); }
        private IEnumerator ReleaseWhenIdle()
        {
            // Confirm/close must be released before gameplay actions are enabled again.
            yield return null;
            while (GameplayButtonsHeld()) yield return null;
            for (int i = 0; i < controls.Length; i++) if (controls[i] != null) controls[i].enabled = wasEnabled[i];
            IsLocked = false; release = null;
        }
        private static bool GameplayButtonsHeld()
        {
            var k = Keyboard.current;
            if (k != null && (k.spaceKey.isPressed || k.jKey.isPressed || k.lKey.isPressed || k.semicolonKey.isPressed)) return true;
            foreach (var g in Gamepad.all)
                if (g.buttonSouth.isPressed || g.buttonWest.isPressed || g.buttonNorth.isPressed
                    || g.buttonEast.isPressed || g.leftShoulder.isPressed || g.rightShoulder.isPressed) return true;
            return false;
        }
    }
}
