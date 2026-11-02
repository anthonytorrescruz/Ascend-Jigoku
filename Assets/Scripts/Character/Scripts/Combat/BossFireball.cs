using System;
using System.Collections.Generic;
using UnityEngine;

// One moving CombatAttack per pooled projectile keeps parry, pogo and damage trades consistent.
[DefaultExecutionOrder(-10)]
public sealed class BossFireball : MonoBehaviour
{
    private FireWormBoss boss;
    private CombatAttack attack;
    private SpriteRenderer visual;
    private Action<BossFireball> recycle;
    private Vector2 heading, size;
    private float remaining;
    private bool reachedPlayer;
    private bool groundWave;
    private float age, explosionUntil;
    private FireWormPresentationSet presentation;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    public CombatAttack Attack => attack;
    public void Configure(FireWormBoss owner, Sprite sprite, int layer, int order, Action<BossFireball> onRecycle)
    {
        boss = owner; recycle = onRecycle;
        presentation = owner.GetComponent<FireWormPresentation>()?.presentation;
        var art = new GameObject("Projectile Art"); art.transform.SetParent(transform, false);
        visual = art.AddComponent<SpriteRenderer>(); visual.sprite = sprite;
        visual.sortingLayerID = layer; visual.sortingOrder = order;
        attack = gameObject.AddComponent<CombatAttack>(); attack.SetTeam(CombatTeam.Enemy);
        gameObject.SetActive(false);
    }
    public void Launch(Vector2 origin, Vector2 direction, bool asGroundWave = false)
    {
        var settings = boss.Settings;
        groundWave = asGroundWave; age = explosionUntil = 0;
        size = groundWave ? settings.shockwaveSize : settings.fireballSize;
        size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        heading = direction.normalized; remaining = Mathf.Max(0.1f, groundWave ? settings.shockwaveLifetime : settings.fireballLifetime); reachedPlayer = false;
        transform.position = origin; transform.localScale = Vector3.one;
        visual.transform.localScale = new Vector3(size.x, size.y, 1);
        visual.color = presentation == null ? settings.fireballColor : Color.white;
        gameObject.SetActive(true); attack.CancelAll();
        attack.BeginAttack(heading.x < 0 ? CombatDirection.Left : CombatDirection.Right, Vector2.zero, size,
            settings.fireballDamage, Mathf.CeilToInt(remaining * CombatAttack.ReferenceFramesPerSecond) + 2, ~0, Time.time);
    }
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float seconds)
    {
        if (!gameObject.activeSelf || seconds <= 0) return;
        age += seconds;
        if (explosionUntil > 0)
        {
            Animate(presentation != null ? presentation.explosion : null, age);
            if (age >= explosionUntil) Despawn();
            return;
        }
        Animate(presentation != null ? presentation.fireball : null, age);
        if (boss == null || !boss.isActiveAndEnabled) { Despawn(); return; }
        var swing = attack.CurrentSwing;
        if (swing == null || swing.Cancelled || swing.TargetsHit.Count > 0)
        {
            if (swing != null && swing.Parried)
                boss.Combat.Stun(Time.time, boss.Settings.fireballParryStunSeconds);
            Explode(); return;
        }
        float step = Mathf.Min(seconds, remaining);
        if (!reachedPlayer) Move((groundWave ? boss.Settings.shockwaveSpeed : boss.Settings.fireballSpeed) * step);
        remaining -= step;
        if (remaining <= 0) Despawn();
        if (boss.Arena != null && (transform.position.x < boss.Arena.bounds.min.x - 1 || transform.position.x > boss.Arena.bounds.max.x + 1)) Despawn();
    }
    private void Move(float distance)
    {
        hits.Clear();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(~0);
        Physics2D.BoxCast(transform.position, size, 0, heading, filter, hits, distance);
        float nearest = float.PositiveInfinity;
        bool terrainFirst = false;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.transform.IsChildOf(boss.transform) || CombatAttack.IsHitbox(hit.collider)) continue;
            bool terrain = (boss.Combat.Definition.terrainLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (!terrain && hit.collider.GetComponentInParent<PlayerHealth>() == null) continue;
            if (hit.distance > nearest || (Mathf.Approximately(hit.distance, nearest) && terrainFirst)) continue;
            nearest = hit.distance; terrainFirst = terrain;
        }
        if (float.IsPositiveInfinity(nearest)) { transform.position += (Vector3)(heading * distance); return; }
        if (terrainFirst) { Explode(); return; }
        // Leave a tiny overlap for CombatResolver to perform the normal damage/parry batch this frame.
        transform.position += (Vector3)(heading * (nearest + 0.025f));
        reachedPlayer = true;
    }
    private void Animate(Sprite[] frames, float elapsed)
    {
        if (frames == null || frames.Length == 0) return;
        visual.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * 12) % frames.Length)];
        visual.flipX = heading.x < 0;
        Vector2 spriteSize = visual.sprite.bounds.size;
        Vector2 fraction = explosionUntil > 0 ? presentation.explosionVisibleFraction : presentation.fireballVisibleFraction;
        visual.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, spriteSize.x * fraction.x), size.y / Mathf.Max(0.01f, spriteSize.y * fraction.y), 1);
    }
    private void Explode()
    {
        if (presentation == null || presentation.explosion == null || presentation.explosion.Length == 0) { Despawn(); return; }
        attack.CancelAll(); age = 0; explosionUntil = presentation.explosion.Length / 12f;
    }
    public void Despawn()
    {
        if (!gameObject.activeSelf) return;
        attack.CancelAll(); gameObject.SetActive(false); recycle?.Invoke(this);
    }
    private void OnDisable() { if (attack != null) attack.CancelAll(); }
}
