using System.Collections.Generic;
using UnityEngine;

public enum GrimHazardKind { Bolt, Pillar, Burst, DashTrail }

// A bounded pool owns these. Telegraphs have no live hitbox; all damage goes through the
// existing resolver, so L parry, pogo, hit protection and horizontal knockback still apply.
// One pooled boss spell (bolt, pillar or burst). Life cycle: Arm -> warning (outline only) -> Activate
// (real hitbox via CombatAttack) -> Stop (short fade) -> Despawn back into the boss's pool.
public sealed class GrimDeathHazard : MonoBehaviour
{
    private GrimDeathBoss boss;
    private CombatAttack attack;
    private SpriteRenderer art;
    private LineRenderer outline;
    private GrimHazardKind kind;
    private Vector2 size, velocity;
    // age = seconds since Arm (reset to 0 on Activate); warning = telegraph length; active = damaging length;
    // fade = time left in the fade-out; radius = circle hitbox radius (bursts only).
    private float gravity, age, warning, active, fade, radius;
    private bool activated, stopping;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(12);
    public CombatAttack Attack => attack;
    public bool IsDamaging => activated && !stopping && attack.IsAttacking;
    public bool IsTelegraphing => !activated && !stopping;
    public GrimHazardKind Kind => kind;
    public static Sprite Square
    {
        get
        {
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1, 0, SpriteMeshType.FullRect);
            return square;
        }
    }
    private static Sprite square, disc;
    private static Material lineMaterial;

    // Shared white circle sprite, generated once in code (soft 1 px edge); tinted by whoever uses it.
    public static Sprite Disc
    {
        get
        {
            if (disc != null)
                return disc;
            const int n = 64;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * (n * .5f));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(n * .5f - d) * 255));
                }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "Shared shadow disc";
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(pixels);
            t.Apply(false, true);
            disc = Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * .5f, n, 0, SpriteMeshType.FullRect);
            return disc;
        }
    }

    // Unit-diameter circle outline (48 points); scale it to resize. Used for warning boundaries and the step marker.
    public static LineRenderer Ring(Transform parent, string name, int order)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.widthMultiplier = .05f;
        line.sortingOrder = order;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * .5f, Mathf.Sin(a) * .5f, 0));
        }
        return line;
    }

    // One-time setup by the boss when it grows its pool; the hazard starts inactive.
    public void Configure(GrimDeathBoss owner)
    {
        boss = owner;
        art = new GameObject("Shadow shape").AddComponent<SpriteRenderer>();
        art.transform.SetParent(transform, false);
        art.sortingOrder = 9;
        outline = Ring(transform, "Warning boundary", 10);
        attack = gameObject.AddComponent<CombatAttack>();
        attack.SetTeam(CombatTeam.Enemy);
        gameObject.SetActive(false);
    }

    // (Re)starts the spell: `tell` seconds of warning, then `duration` seconds of danger.
    // `motion` is the starting velocity and `acceleration` the downward gravity (bolts only).
    public void Arm(GrimHazardKind type, Vector2 position, Vector2 dimensions, float tell, float duration, Vector2 motion = default, float acceleration = 0)
    {
        kind = type;
        size = dimensions;
        warning = Mathf.Max(0, tell);
        active = Mathf.Max(.05f, duration);
        velocity = motion;
        gravity = acceleration;
        radius = type == GrimHazardKind.Burst ? size.x * .5f : 0;
        age = fade = 0;
        activated = stopping = false;
        transform.position = position;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        art.sprite = type == GrimHazardKind.Pillar ? Square : Disc;
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        outline.enabled = warning > 0;
        outline.transform.localScale = new Vector3(size.x, size.y, 1);
        gameObject.SetActive(true);
        attack.CancelAll();
        if (warning <= 0)
            Activate();
        else
            DrawWarning();
    }

    // Warning is over: starts the real damaging swing (60 fps frames, with 2 spare) and plays pillar / burst effects.
    private void Activate()
    {
        activated = true;
        age = 0;
        outline.enabled = false;
        var swing = attack.BeginAttack(
            velocity.x < 0 ? CombatDirection.Left : CombatDirection.Right,
            Vector2.zero,
            size,
            1,
            Mathf.CeilToInt(active * 60) + 2,
            ~0,
            Time.time);
        if (swing != null)
            swing.CircleRadius = radius;
        if (kind == GrimHazardKind.Pillar)
            boss.Play(boss.Settings.pillarSound, .65f);
        else if (kind == GrimHazardKind.Burst)
        {
            boss.Play(boss.Settings.burstSound);
            AscendJigoku.Gameplay.EncounterFeedback.Shake(.32f);
        }
    }

    private void Update()
    {
        if (boss == null || !boss.IsAlive || boss.Player == null || boss.Player.CurrentHealth <= 0)
        {
            Despawn();
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0)
            return;
        age += dt;
        // Fading out after Stop.
        if (stopping)
        {
            fade -= dt;
            art.color = new Color(.4f, .15f, .65f, Mathf.Clamp01(fade / .16f) * .65f);
            if (fade <= 0)
                Despawn();
            return;
        }
        if (!activated)
        {
            DrawWarning();
            if (age >= warning)
                Activate();
            return;
        }
        // The swing ended early (cancelled, or a bolt hit something): a parry also stuns the boss.
        var swing = attack.CurrentSwing;
        if (swing == null || swing.Cancelled || (kind == GrimHazardKind.Bolt && swing.TargetsHit.Count > 0))
        {
            if (swing != null && swing.Parried)
                boss.Stun(boss.Settings.parryStunSeconds);
            Stop();
            return;
        }
        if (kind == GrimHazardKind.Bolt)
        {
            velocity.y -= gravity * dt;
            if (!Move(velocity * dt))
                return;
            art.transform.localScale = new Vector3(size.x, size.y, 1) * (1 + .08f * Mathf.Sin(age * 30));
        }
        else if (kind == GrimHazardKind.Pillar)
        {
            // The pillar is tall immediately when active, matching its full-height warning.
            art.transform.localScale = new Vector3(size.x, size.y, 1);
        }
        else
        {
            // Burst: grows from 75% to full size in .12 s.
            float t = Mathf.Clamp01(age / .12f);
            art.transform.localScale = new Vector3(size.x, size.y, 1) * Mathf.Lerp(.75f, 1, t);
            outline.enabled = true;
            outline.transform.localScale = new Vector3(size.x, size.y, 1);
        }
        var color = boss.Settings.shadowColor;
        art.color = new Color(color.r, color.g, color.b, .88f);
        outline.startColor = outline.endColor = boss.Settings.warningColor;
        if (age >= active)
            Stop();
        // Safety net: end hazards that leave the arena sideways or fall below it.
        var floor = boss.Arena;
        if (floor != null && (transform.position.x < floor.bounds.min.x - 2 || transform.position.x > floor.bounds.max.x + 2 || transform.position.y < floor.bounds.max.y - 3))
            Stop();
    }

    // Fills the area faintly and brightens the outline as the warning runs out.
    private void DrawWarning()
    {
        float t = warning <= 0 ? 1 : Mathf.Clamp01(age / warning);
        var c = boss.Settings.warningColor;
        art.color = new Color(c.r, c.g, c.b, .08f + .15f * t);
        art.transform.localScale = new Vector3(size.x, size.y, 1);
        outline.startColor = outline.endColor = new Color(c.r, c.g, c.b, .5f + .5f * t);
        outline.widthMultiplier = .035f + .04f * t;
    }

    // Bolt movement: box-casts along the step. Hitting terrain (layer 3) stops the bolt and returns false;
    // reaching the player moves the bolt up to them, zeroes its motion and lets the combat resolver apply the hit.
    private bool Move(Vector2 delta)
    {
        float distance = delta.magnitude;
        if (distance < .00001f)
            return true;
        hits.Clear();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(~0);
        Physics2D.BoxCast(transform.position, size, 0, delta / distance, filter, hits, distance);
        float nearest = float.PositiveInfinity;
        bool terrain = false;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.transform.IsChildOf(boss.transform) || CombatAttack.IsHitbox(hit.collider))
                continue;
            bool wall = hit.collider.gameObject.layer == 3;
            if (!wall && hit.collider.GetComponentInParent<PlayerHealth>() != boss.Player)
                continue;
            if (hit.distance < nearest || (Mathf.Approximately(hit.distance, nearest) && wall))
            {
                nearest = hit.distance;
                terrain = wall;
            }
        }
        if (!float.IsPositiveInfinity(nearest))
        {
            transform.position += (Vector3)(delta / distance * (nearest + (terrain ? 0 : .025f)));
            if (terrain)
            {
                Stop();
                return false;
            }
            // Remain overlapping until CombatResolver applies a hit or a parry this frame.
            velocity = Vector2.zero;
            gravity = 0;
            return true;
        }
        transform.position += (Vector3)delta;
        return true;
    }

    // Ends the damage and starts a .16 s fade; Update despawns it afterwards.
    private void Stop()
    {
        if (stopping)
            return;
        stopping = true;
        fade = .16f;
        outline.enabled = false;
        attack.CancelAll();
    }

    // Deactivates and returns the hazard to the boss's pool.
    public void Despawn()
    {
        if (!gameObject.activeSelf)
            return;
        attack.CancelAll();
        gameObject.SetActive(false);
        if (boss != null)
            boss.Recycle(this);
    }

    private void OnDisable()
    {
        if (attack != null)
            attack.CancelAll();
    }
}
