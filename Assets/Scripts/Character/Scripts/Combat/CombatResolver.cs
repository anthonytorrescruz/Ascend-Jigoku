using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CombatResolver : MonoBehaviour
{
    private static CombatResolver instance;
    public static CombatResolver Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Combat Resolver").AddComponent<CombatResolver>();
            return instance;
        }
    }
    private readonly List<CombatAttack.Swing> swings = new List<CombatAttack.Swing>();
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private readonly List<RaycastHit2D> walls = new List<RaycastHit2D>();
    private readonly List<PendingDamage> pending = new List<PendingDamage>();
    private struct PendingDamage
    {
        public CombatAttack.Swing Swing;
        public EnemyContactDamage Contact;
        public EnemyHealth Enemy;
        public PlayerHealth Player;
        public int Amount;
        public float KnockbackDirection;
        public Vector2 EnemyKnockback;
        public bool Apply;
        public bool IsContact;
    }
    public void Register(CombatAttack.Swing swing) => swings.Add(swing);
    public void QueueContact(EnemyContactDamage source, PlayerHealth target, int damage, float now)
    {
        pending.Add(new PendingDamage { Contact = source, IsContact = true, Player = target, Amount = damage,
            KnockbackDirection = KnockbackDirection(source.transform.position.x, target) });
    }
    private void LateUpdate() => Resolve(Time.time);
    public void Resolve(float now)
    {
        if (swings.Count == 0 && pending.Count == 0) return;
        for (int i = 0; i < swings.Count; i++)
            if (swings[i].IsActive(now)) swings[i].RefreshBox();

        // Weapon pogo is an airborne down-strike mechanic, independent of the L parry action.
        for (int i = 0; i < swings.Count; i++)
        {
            var player = swings[i];
            if (!player.IsActive(now) || player.Owner.Team != CombatTeam.Player
                || player.Direction != CombatDirection.Down || player.PogoTriggered) continue;
            for (int j = 0; j < swings.Count; j++)
            {
                var enemy = swings[j];
                if (enemy.IsActive(now) && enemy.Owner.Team == CombatTeam.Enemy && player.Box.Overlaps(enemy.Box)
                    && HasClearPath(player, enemy.Owner.transform.position)
                    && TryPogo(player, enemy, now)) break;
            }
        }

        bool previousTriggers = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            for (int i = 0; i < swings.Count; i++)
            {
                var swing = swings[i];
                if (!swing.IsActive(now)) continue;
                var filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(swing.Owner.Team == CombatTeam.Player && swing.Direction == CombatDirection.Down
                    ? (LayerMask)~0 : swing.DamageLayers);
                hits.Clear();
                Physics2D.OverlapBox(swing.Box.center, swing.Size, 0, filter, hits);
                for (int h = 0; h < hits.Count; h++) ResolveHit(swing, hits[h], now);
            }
        }
        finally { Physics2D.queriesHitTriggers = previousTriggers; }

        // Snapshot valid hits before changing health: a killing hit must not erase a simultaneous trade.
        for (int i = 0; i < pending.Count; i++)
        {
            var hit = pending[i];
            hit.Apply = (hit.Swing == null || (!hit.Swing.Cancelled && hit.Swing.Owner != null))
                && (!hit.IsContact || (hit.Contact != null && hit.Contact.isActiveAndEnabled && !hit.Contact.IsSuppressed(hit.Player, now)));
            pending[i] = hit;
        }
        // Commit enemy damage before player damage, which may reload the scene on death.
        for (int i = 0; i < pending.Count; i++)
            if (pending[i].Apply && pending[i].Enemy != null) pending[i].Enemy.TakeCombatDamage(pending[i].Amount, pending[i].EnemyKnockback);
        for (int i = 0; i < pending.Count; i++)
            if (pending[i].Apply && pending[i].Player != null)
                pending[i].Player.TakeCombatDamage(pending[i].Amount, pending[i].KnockbackDirection);
        pending.Clear();
        for (int i = swings.Count - 1; i >= 0; i--)
            if (swings[i].Owner == null || now >= swings[i].EndsAt || swings[i].Cancelled) swings.RemoveAt(i);
    }
    private void ResolveHit(CombatAttack.Swing swing, Collider2D hit, float now)
    {
        if (hit == null || hit.transform.IsChildOf(swing.Owner.transform)
            || CombatAttack.IsHitbox(hit)) return;
        if (!HasClearPath(swing, hit.ClosestPoint(swing.Owner.transform.position), hit)) return;
        if (swing.Owner.Team == CombatTeam.Player)
        {
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (swing.Direction == CombatDirection.Down && (enemy != null || hit.GetComponentInParent<Pogoable>() != null))
            {
                if (TryPogo(swing, null, now) && enemy != null)
                    SuppressContact(enemy.GetComponent<EnemyContactDamage>(), swing.Owner.GetComponent<PlayerHealth>(), swing.EndsAt);
            }
            if (enemy != null && swing.Damage > 0 && (swing.DamageLayers.value & (1 << hit.gameObject.layer)) != 0
                && swing.TargetsHit.Add(enemy))
                pending.Add(new PendingDamage { Swing = swing, Enemy = enemy, Amount = swing.Damage,
                    EnemyKnockback = enemy.transform.position - swing.Owner.transform.position });
        }
        else
        {
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player == null || swing.Damage <= 0 || swing.TargetsHit.Contains(player)) return;
            var parry = player.GetComponent<PlayerParry>();
            if (parry != null && parry.TryDeflect(swing, now)) return;
            swing.TargetsHit.Add(player);
            pending.Add(new PendingDamage { Swing = swing, Player = player, Amount = swing.Damage,
                KnockbackDirection = KnockbackDirection(swing.Owner.transform.position.x, player) });
        }
    }
    private bool HasClearPath(CombatAttack.Swing swing, Vector2 point, Collider2D target = null)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        var profile = swing.Owner.GetComponent<EnemyCombat>()?.Definition;
        filter.SetLayerMask(profile != null ? profile.terrainLayers : swing.Owner.Settings.terrainLayers);
        walls.Clear();
        Physics2D.Linecast(swing.Owner.transform.position, point, filter, walls);
        foreach (var wall in walls)
            if (wall.collider != target && !wall.collider.transform.IsChildOf(swing.Owner.transform)) return false;
        return true;
    }
    private static float KnockbackDirection(float sourceX, PlayerHealth player)
    {
        float delta = player.transform.position.x - sourceX;
        if (Mathf.Abs(delta) > 0.001f) return Mathf.Sign(delta);
        var controller = player.GetComponent<PlayerController>();
        return controller != null && controller.IsFacingRight ? -1f : 1f;
    }
    private static bool TryPogo(CombatAttack.Swing player, CombatAttack.Swing enemy, float now)
    {
        if (player.Direction != CombatDirection.Down || player.PogoTriggered || player.Owner == null
            || player.Owner.PlayerController == null || player.Owner.PlayerController.IsGrounded) return false;
        player.PogoTriggered = true;
        player.Owner.PlayerController.PogoBounce();
        player.Owner.Feedback?.Pogo();
        if (enemy != null)
        {
            enemy.Owner.Cancel(enemy);
            SuppressContact(enemy.Owner.ContactDamage, player.Owner.GetComponent<PlayerHealth>(), player.EndsAt);
        }
        return true;
    }
    private static void SuppressContact(EnemyContactDamage contact, PlayerHealth player, float until)
    {
        if (contact != null && player != null) contact.Suppress(player, until);
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}
