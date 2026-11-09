using System;
using System.Collections.Generic;
using UnityEngine;

// Swept box queries prevent fast shots tunnelling through enemies or thin walls.
public sealed class PlayerProjectile : MonoBehaviour
{
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private Transform owner;
    private Action<PlayerProjectile> recycle;
    private SpriteRenderer visual;
    private Vector2 direction, size;
    private float speed, remaining;
    private float lifetime, unfoldSeconds;
    private int damage;
    private LayerMask enemyLayers, terrainLayers;
    public void Configure(Action<PlayerProjectile> onRecycle)
    {
        recycle = onRecycle;
        visual = gameObject.AddComponent<SpriteRenderer>();
        visual.sprite = SpellShapes.Crescent;
        gameObject.SetActive(false);
    }
    public void Launch(Transform source, Vector2 position, Vector2 heading, float muzzleDistance, Vector2 boxSize,
        float unitsPerSecond, float seconds, int hitDamage, LayerMask targets, LayerMask terrain, Color color, SpriteRenderer sourceRenderer,
        float unfoldDuration = .1f)
    {
        owner = source; direction = heading; size = boxSize;
        speed = Mathf.Max(0.1f, unitsPerSecond); remaining = Mathf.Max(0.01f, seconds);
        lifetime = remaining; unfoldSeconds = Mathf.Max(.01f, unfoldDuration);
        damage = hitDamage; enemyLayers = targets; terrainLayers = terrain;
        transform.position = new Vector3(position.x, position.y, source.position.z);
        transform.localScale = new Vector3(size.x, size.y, 1);
        transform.rotation = Quaternion.identity;
        visual.flipX = direction.x < 0;
        visual.color = color;
        if (sourceRenderer != null) { visual.sortingLayerID = sourceRenderer.sortingLayerID; visual.sortingOrder = sourceRenderer.sortingOrder + 2; }
        gameObject.SetActive(true);
        Animate();
        Move(Mathf.Max(0, muzzleDistance));
    }
    private void Update() => Tick(Time.deltaTime);
    public void Tick(float seconds)
    {
        if (!gameObject.activeSelf || seconds <= 0) return;
        if (owner == null) { Despawn(); return; }
        float step = Mathf.Min(seconds, remaining);
        Move(speed * step);
        remaining -= step;
        Animate();
        if (gameObject.activeSelf && remaining <= 0) Despawn();
    }
    private void Animate()
    {
        // Only the art unfolds; the swept damage box remains predictable in both directions.
        float opening = Mathf.SmoothStep(0, 1, (lifetime - remaining) / unfoldSeconds);
        transform.localScale = new Vector3(size.x * Mathf.Lerp(.45f, 1, opening), size.y * Mathf.Lerp(.65f, 1, opening), 1);
        float fade = Mathf.Clamp01(remaining / .12f);
        var color = visual.color; color.a = fade; visual.color = color;
    }
    private void Move(float distance)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(enemyLayers.value | terrainLayers.value);
        hits.Clear();
        Physics2D.BoxCast(transform.position, size, 0, direction, filter, hits, distance);
        EnemyHealth target = null;
        float nearest = float.PositiveInfinity;
        bool blocked = false;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.transform.IsChildOf(owner) || CombatAttack.IsHitbox(hit.collider)) continue;
            bool terrain = (terrainLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (!terrain && (enemy == null || (enemyLayers.value & (1 << hit.collider.gameObject.layer)) == 0)) continue;
            if (hit.distance > nearest || (Mathf.Approximately(hit.distance, nearest) && blocked)) continue;
            nearest = hit.distance; blocked = terrain; target = terrain ? null : enemy;
        }
        if (!float.IsPositiveInfinity(nearest))
        {
            transform.position += (Vector3)(direction * nearest);
            if (target != null) target.TakeCombatDamage(damage, direction);
            Despawn();
            return;
        }
        transform.position += (Vector3)(direction * distance);
    }
    public void Despawn()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        owner = null;
        recycle?.Invoke(this);
    }
}
