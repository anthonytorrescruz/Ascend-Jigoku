// F1 also shows live stats above players and enemies. Register extra values here.
using System.Collections.Generic;
using UnityEngine;

namespace AscendJigoku.Debugging
{
    public sealed class DebugMenuGameBindings : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly HashSet<PlayerHealth> players = new HashSet<PlayerHealth>();
        private readonly HashSet<EnemyHealth> enemies = new HashSet<EnemyHealth>();
        private float nextScan;
        private GUIStyle labelStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var host = new GameObject("Debug Menu Bindings");
            DontDestroyOnLoad(host);
            host.AddComponent<DebugMenuGameBindings>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;
            players.RemoveWhere(player => {
                if (player != null && player.isActiveAndEnabled) return false;
                DebugMenu.RemoveAll(player);
                return true;
            });
            enemies.RemoveWhere(enemy => {
                if (enemy != null && enemy.isActiveAndEnabled) return false;
                DebugMenu.RemoveAll(enemy);
                return true;
            });
            foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>())
            {
                if (!player.isActiveAndEnabled || !players.Add(player)) continue;
                DebugMenu.Action(player, "Player", "Take 1 damage", 1, () => player.TakeDamage(1));
                PlayerRespawn respawn = player.GetComponent<PlayerRespawn>();
                if (respawn != null)
                    DebugMenu.Action(player, "Player", "Respawn", 2, () => {
                        if (respawn != null) respawn.Respawn();
                    });
            }
            foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>())
            {
                if (!enemy.isActiveAndEnabled || !enemies.Add(enemy)) continue;

            }
        }

        private static string AttackState(CombatAttack attack)
        {
            if (attack == null || !attack.IsAttacking) return "Idle";
            return attack.CurrentSwing.Direction + " / damage " + attack.CurrentSwing.Damage;
        }

        private static string EnemyState(CombatAttack attack, EnemyCombat combat)
        {
            if (combat != null && combat.IsWindingUp) return "Windup";
            if (attack != null && attack.IsAttacking) return AttackState(attack);
            float remaining = combat != null ? Mathf.Max(0, combat.NextAttackAt - Time.time) : 0;
            return remaining > 0 ? "Cooldown " + remaining.ToString("F1") + "s" : "Idle";
        }

        private void OnGUI()
        {
            if (!DebugMenu.IsOpen) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true,
                    normal = { textColor = Color.white }
                };
            GUI.depth = 0;
            Matrix4x4 previous = GUI.matrix;
            float scale = DebugMenu.DisplayScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            foreach (PlayerHealth player in players)
            {
                if (player == null || !player.isActiveAndEnabled) continue;
                var controller = player.GetComponent<PlayerController>();
                string text = player.name + "  HP " + player.CurrentHealth + "/" + player.MaxHealth
                    + Velocity(player) + "\nAttack: " + AttackState(player.GetComponent<CombatAttack>());
                if (controller != null) text += "  |  " + (controller.IsGrounded ? "Grounded" : "Airborne");
                text += DebugMenu.Readout(player);
                DrawLabel(camera, player, text, scale);
            }
            foreach (EnemyHealth enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                string text = enemy.name + "  HP " + enemy.CurrentHealth + "/" + enemy.MaxHealth
                    + Velocity(enemy) + "\nAttack: " + EnemyState(enemy.GetComponent<CombatAttack>(), enemy.GetComponent<EnemyCombat>());
                text += DebugMenu.Readout(enemy);
                DrawLabel(camera, enemy, text, scale);
            }
            GUI.matrix = previous;
        }

        private static string Velocity(Component entity)
        {
            var body = entity.GetComponent<Rigidbody2D>();
            return body != null ? "\nVelocity " + body.linearVelocity.ToString("F1") : "";
        }

        private void DrawLabel(Camera camera, Component entity, string text, float scale)
        {
            Vector3 top = entity.transform.position + Vector3.up;
            var collider = entity.GetComponent<Collider2D>();
            var renderer = entity.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.enabled)
                top = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z);
            else if (collider != null && collider.enabled)
                top = new Vector3(collider.bounds.center.x, collider.bounds.max.y, collider.bounds.center.z);
            Vector3 screen = camera.WorldToScreenPoint(top + Vector3.up * 0.15f);
            if (screen.z <= 0 || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y))) return;
            float width = Mathf.Min(250, Screen.width / scale - 16);
            float height = labelStyle.CalcHeight(new GUIContent(text), width - 12) + 8;
            var rect = new Rect(screen.x / scale - width / 2, (Screen.height - screen.y) / scale - height, width, height);
            rect.x = Mathf.Clamp(rect.x, 8, Screen.width / scale - width - 8);
            rect.y = Mathf.Max(8, rect.y);
            DebugMenu.DrawBackdrop(rect);
            GUI.Label(new Rect(rect.x + 6, rect.y + 4, width - 12, height - 8), text, labelStyle);
        }

        private void OnDestroy()
        {
            foreach (PlayerHealth player in players) DebugMenu.RemoveAll(player);
            foreach (EnemyHealth enemy in enemies) DebugMenu.RemoveAll(enemy);
        }
#endif
    }
}
