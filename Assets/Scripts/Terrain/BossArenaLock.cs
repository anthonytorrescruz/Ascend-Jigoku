using System;
using UnityEngine;

// Keeps the player inside a boss arena once the fight starts: the one-way floor turns solid,
// and anyone who still slips out (below the floor, past a seal, over the ceiling) is put back on the floor.
public sealed class BossArenaLock : MonoBehaviour
{
    private Collider2D floor;
    private PlatformEffector2D effector;
    private PlayerController player;
    private PlayerHealth health;
    private Rigidbody2D body;
    private Collider2D shape;
    private Func<bool> fighting;
    private float halfWidth, height;
    private bool locked;

    // Called by the level generator when it builds the arena; inFight reports whether the boss fight is currently active.
    public void Initialize(Collider2D arenaFloor, PlayerController actor, float arenaHalfWidth, float ceilingHeight, Func<bool> inFight)
    {
        floor = arenaFloor;
        effector = floor.GetComponent<PlatformEffector2D>();
        player = actor;
        health = actor.GetComponent<PlayerHealth>();
        body = actor.GetComponent<Rigidbody2D>();
        shape = actor.GetComponent<Collider2D>();
        halfWidth = arenaHalfWidth;
        height = ceilingHeight;
        fighting = inFight;
    }

    private void FixedUpdate()
    {
        if (floor == null || player == null) return;
        bool shouldLock = health.CurrentHealth > 0 && fighting();
        if (shouldLock != locked)
        {
            // Locked: the floor stops being one-way so the player can't drop through or jump up through it.
            locked = shouldLock;
            if (effector != null) effector.enabled = !locked;
            floor.usedByEffector = !locked && effector != null;
        }

        if (!locked) return;

        // Inside the arena means feet between the floor top and the ceiling, and within the half width (plus a little slack).
        Bounds b = shape.bounds;
        float top = floor.bounds.max.y, x = b.center.x - transform.position.x;
        if (b.min.y >= top - 0.3f && b.min.y <= top + height && Mathf.Abs(x) <= halfWidth + 0.5f) return;

        // Escaped: stand the player back on the floor, kept at least 1.5 units inside the side walls.
        float footOffset = body.position.y - b.min.y;
        var back = new Vector2(transform.position.x + Mathf.Clamp(x, -halfWidth + 1.5f, halfWidth - 1.5f), top + footOffset + 0.05f);
        body.linearVelocity = Vector2.zero;
        body.position = back;
        player.transform.position = back;
        Physics2D.SyncTransforms();
    }
}
