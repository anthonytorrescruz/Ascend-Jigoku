using UnityEngine;
using UnityEngine.SceneManagement;

namespace AscendJigoku.Gameplay
{
    // Rest clears threats silently: no free coin farming, death screams, or fresh projectiles.
    public static class CheckpointSanctuary
    {
        // True while the player is resting at a safe checkpoint (or during the victory flow), so hostiles can't hurt them.
        public static bool Protects(PlayerHealth player)
        {
            var level = VerticalLevelGenerator.Active;
            return VictoryFlow.IsPlaying || (level != null && level.IsSafeCheckpointFor(player));
        }

        // Removes enemies and enemy projectiles in the region (or the whole scene) without death effects or rewards.
        public static void ClearHostiles(Scene scene, Bounds region, bool entireScene = false)
        {
            foreach (var enemy in Object.FindObjectsByType<EnemyHealth>())
            {
                if (enemy.gameObject.scene != scene) continue;
                var shape = enemy.GetComponent<Collider2D>();
                if (!entireScene && !region.Intersects(shape != null ? shape.bounds : new Bounds(enemy.transform.position, Vector3.one))) continue;
                // A checkpoint must never count as defeating a living boss.
                if (!entireScene && enemy.CurrentHealth > 0
                    && (enemy.GetComponent<GrimDeathBoss>() != null || enemy.GetComponent<FireWormBoss>() != null)) continue;
                VerticalLevelGenerator.Active?.ForgetClearedEnemy(enemy);
                foreach (var audio in enemy.GetComponentsInChildren<AudioSource>()) audio.Stop();
                enemy.DamageEnabled = false;
                enemy.gameObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
            }
            foreach (var attack in Object.FindObjectsByType<CombatAttack>())
            {
                if (attack.Team != CombatTeam.Enemy || attack.gameObject.scene != scene || attack.GetComponent<EnemyHealth>() != null) continue;
                if (!entireScene && !region.Contains(attack.transform.position)) continue;
                attack.CancelAll();
                attack.gameObject.SetActive(false);
                Object.Destroy(attack.gameObject);
            }
        }

        // Box around the checkpoint, widened to also cover whatever the main orthographic camera currently shows.
        public static Bounds VisibleRegion(Vector3 center, float halfWidth)
        {
            var camera = Camera.main;
            var region = new Bounds(center, new Vector3(halfWidth * 2 + 6, 16, 100));
            if (camera != null && camera.orthographic)
                region.Encapsulate(new Bounds(camera.transform.position,
                    new Vector3(camera.orthographicSize * camera.aspect * 2 + 6, camera.orthographicSize * 2 + 6, 100)));
            return region;
        }
    }
}
