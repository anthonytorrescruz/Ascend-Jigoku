using System;
using UnityEngine;

// Owned weapons, the equipped one and each weapon's ability cooldown. Input is read by PlayerCombat.
[DisallowMultipleComponent]
public sealed class PlayerWeapons : MonoBehaviour
{
    private const float FlashCutDistance = 4.5f;
    private int owned = 1 << (int)WeaponId.Katana;
    private WeaponId equipped = WeaponId.Katana;
    private readonly float[] readyAt = new float[2];
    private PlayerCombat combat;
    private CombatAttack attack;
    private PlayerController controller;

    public event Action Changed;
    public Weapon Current => Weapon.Get(equipped);
    public int OwnedMask => owned;
    public bool Owns(WeaponId id) => (owned & (1 << (int)id)) != 0;
    public bool HasChoice => owned != 1 << (int)WeaponId.Katana;
    public float CooldownRemaining => Mathf.Max(0, readyAt[(int)equipped] - Time.time);
    public float CooldownFraction => Mathf.Clamp01(CooldownRemaining / Mathf.Max(0.01f, Current.AbilityCooldown));

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>(); attack = GetComponent<CombatAttack>(); controller = GetComponent<PlayerController>();
    }

    public void Unlock(WeaponId id, bool equip)
    {
        owned |= 1 << (int)id;
        if (equip) equipped = id;
        Changed?.Invoke();
    }

    public void SetOwnedMask(int mask)
    {
        owned = (mask | (1 << (int)WeaponId.Katana)) & 3;
        if (!Owns(equipped)) equipped = WeaponId.Katana;
        Changed?.Invoke();
    }

    public bool Swap()
    {
        float now = Time.time;
        if (!HasChoice || !combat.CanAct(now)) return false;
        equipped = equipped == WeaponId.Katana ? WeaponId.Kanabo : WeaponId.Katana;
        Changed?.Invoke();
        return true;
    }

    public bool TryAbility()
    {
        float now = Time.time;
        var weapon = Current;
        if (now < readyAt[(int)equipped] || !combat.CanAct(now)) return false;
        bool used = weapon.Id == WeaponId.Kanabo ? QuakeSlam(weapon, now) : FlashCut(weapon, now);
        if (!used) return false;
        readyAt[(int)equipped] = now + weapon.AbilityCooldown;
        CombatFeedback.PlayClip(combat.attackClip, 0.2f);
        return true;
    }

    // Dash through terrain-free space and cut everything along the path.
    private bool FlashCut(Weapon weapon, float now)
    {
        var body = GetComponent<Collider2D>();
        var rb = GetComponent<Rigidbody2D>();
        float sign = controller.IsFacingRight ? 1 : -1;
        float distance = FlashCutDistance;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(attack.Settings.terrainLayers);
        var hits = new RaycastHit2D[4];
        int count = body.Cast(new Vector2(sign, 0), filter, hits, distance);
        for (int i = 0; i < count; i++) distance = Mathf.Min(distance, Mathf.Max(0, hits[i].distance - 0.02f));

        Vector2 start = rb.position;
        Vector2 end = start + new Vector2(sign * distance, 0);
        rb.position = end; transform.position = new Vector3(end.x, end.y, transform.position.z);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

        float left = -distance - 0.7f, right = 1.5f;
        Vector2 offset = new Vector2(sign * (left + right) * 0.5f, body.bounds.center.y - transform.position.y);
        var swing = attack.BeginAttack(sign > 0 ? CombatDirection.Right : CombatDirection.Left, offset,
            new Vector2(right - left, 2f), combat.AttackDamage + 1, 8, combat.EnemyLayers, now);
        if (swing == null) return false;
        controller.PlayAttackAnimation();
        combat.HoldAttacks(swing.EndsAt + 0.12f);
        Vector2 mid = (start + end) * 0.5f + Vector2.up * (body.bounds.center.y - start.y);
        WeaponFx.Burst(mid, new Vector2(distance + 1f, 0.5f), new Vector2(distance + 1.6f, 0.05f), new Color(0.95f, 0.89f, 0.8f, 0.9f), 0.2f, 20);
        return true;
    }

    // Wide ground shockwave around the feet: heavy damage and knockback, and it erases enemy projectiles.
    private bool QuakeSlam(Weapon weapon, float now)
    {
        var body = GetComponent<Collider2D>();
        Vector2 size = new Vector2(9f, 2.6f);
        Vector2 offset = new Vector2(0, body.bounds.min.y - transform.position.y + size.y * 0.5f - 0.2f);
        var swing = attack.BeginAttack(CombatDirection.Right, offset, size, combat.AttackDamage + 2, 8, combat.EnemyLayers, now, 6);
        if (swing == null) return false;
        swing.Knockback = 2.6f;
        controller.PlayAttackAnimation();
        combat.HoldAttacks(swing.EndsAt + 0.3f);
        CombatResolver.Instance.CancelProjectiles(swing.Box);
        Vector2 feet = new Vector2(transform.position.x, body.bounds.min.y + 0.15f);
        WeaponFx.Burst(feet, new Vector2(1f, 0.3f), new Vector2(size.x, 0.6f), new Color(0.94f, 0.54f, 0.24f, 0.9f), 0.35f, 20);
        return true;
    }
}
