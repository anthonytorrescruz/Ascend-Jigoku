using System;
using System.Collections.Generic;
using UnityEngine;

public enum GrimDeathMove { Scythe, ReapingDash, ShadowBolts, ShadowPillars, Eclipse, Summon }
public enum GrimDeathState { Dormant, Entrance, Hunt, ScytheWindup, ScytheFirst, ScytheGap, ScytheSecond, DashWindup, Dash, Cast, Recovery, StepWindup, StepFade, StepRecovery, Stunned, Dying, Dead }

// Final boss state machine. GrimDeathEncounter spawns it, calls Initialize with the arena floor and player,
// then Activate when the player lands on the floor; Died fires when the death animation ends.
// Flow: Dormant -> Entrance -> Hunt. In Hunt he walks toward the player and picks the next move from a fixed
// rotation (Scythe, Bolts, Dash, Summon, Pillars, Eclipse) once nextMoveAt passes. Every move ends in
// Recovery (an opening for the player), then back to Hunt. A parry stuns him; Shadow Step is a teleport
// behind the player when they are far away or standing too close.
[DefaultExecutionOrder(80)]
[RequireComponent(typeof(EnemyHealth), typeof(CombatAttack), typeof(Rigidbody2D))]
public sealed class GrimDeathBoss : MonoBehaviour
{
    public GrimDeathSettings settings;
    private EnemyHealth health;
    private CombatAttack attack;
    private Rigidbody2D body;
    private BoxCollider2D shape;
    private SpriteRenderer art, halo;
    private LineRenderer path;
    private AudioSource voice;
    private Collider2D floor;
    private PlayerHealth player;
    // Timers are absolute Time.time values: until = end of the current state, nextMoveAt / nextStepAt /
    // nextSummonAt = earliest start of the next attack, step or summon. stateAt = when the state began (animation).
    // hurtUntil = end of the damage flash. velocity = horizontal speed applied in FixedUpdate.
    private float until,
        nextMoveAt,
        nextStepAt,
        nextSummonAt,
        stateAt,
        hurtUntil,
        velocity,
        deadAt,
        lastParry = -1,
        nextPillarSoundAt;
    private int facing = 1, lastHealth, moveIndex, volleyIndex;
    // lockedTarget = player position captured when a move starts; stepTarget = Shadow Step landing spot.
    private Vector2 lockedTarget, stepTarget;
    private bool initialized, activated, invulnerable;
    private GrimDeathMove move;
    // Spell hazards are pooled: the stack holds idle ones, the list holds every one ever created.
    private readonly Stack<GrimDeathHazard> pool = new Stack<GrimDeathHazard>(32);
    private readonly List<GrimDeathHazard> hazards = new List<GrimDeathHazard>(48);
    private readonly List<GrimDeathSpirit> spirits = new List<GrimDeathSpirit>(4);
    private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    private readonly GrimDeathMove[] rotation =
    {
        GrimDeathMove.Scythe,
        GrimDeathMove.ShadowBolts,
        GrimDeathMove.ReapingDash,
        GrimDeathMove.Summon,
        GrimDeathMove.ShadowPillars,
        GrimDeathMove.Eclipse
    };
    public GrimDeathSettings Settings => settings;
    public PlayerHealth Player => player;
    public Collider2D Arena => floor;
    public EnemyHealth Health => health;
    public CombatAttack Attack => attack;
    public GrimDeathState State { get; private set; }
    public bool IsAlive => health != null && health.CurrentHealth > 0;
    public bool FacingRight => facing > 0;
    // Tests can turn this off to drive moves manually through TryStartMove.
    public bool AutomaticAttacks { get; set; } = true;
    public int ShadowSteps { get; private set; }
    public int[] MoveCounts { get; } = new int[6];
    public int ActiveHazardCount
    {
        get
        {
            int n = 0;
            foreach (var h in hazards)
                if (h != null && h.gameObject.activeSelf)
                    n++;
            return n;
        }
    }
    public int PooledHazardCount => hazards.Count;
    // True while any spell is still telegraphing or can still hurt; the boss waits for it to clear before a new move.
    public bool HasLingeringDanger
    {
        get
        {
            foreach (var hazard in hazards)
                if (hazard != null && hazard.gameObject.activeSelf && (hazard.IsDamaging || hazard.IsTelegraphing))
                    return true;
            return false;
        }
    }
    public int SpiritCount
    {
        get
        {
            spirits.RemoveAll(s => s == null);
            return spirits.Count;
        }
    }
    public event Action Died;
    // Warning text shown under the boss bar for the current attack.
    public string Telegraph => State == GrimDeathState.StepWindup ? "SHADOW STEP — watch the violet landing rune"
        : State == GrimDeathState.ScytheWindup || State == GrimDeathState.ScytheGap ? "TWO SCYTHE CUTS — parry, pogo, or retreat"
        : State == GrimDeathState.DashWindup ? "REAPING DASH — jump or parry the charge"
        : State == GrimDeathState.Cast ? move == GrimDeathMove.ShadowPillars ? "SHADOW PILLARS — stand between the marked columns"
            : move == GrimDeathMove.Eclipse ? "ECLIPSE — leave the marked circle"
            : move == GrimDeathMove.Summon ? "SOUL CALL — destroy the spirits or dodge their locked lunges" : "SOUL RAIN — dodge the falling arcs"
        : State == GrimDeathState.Recovery || State == GrimDeathState.Stunned || State == GrimDeathState.StepRecovery ? "OPENING — strike now" : "";

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        attack = GetComponent<CombatAttack>();
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<BoxCollider2D>();
        art = GetComponent<SpriteRenderer>();
        if (settings != null)
            health.InitializeEncounterHealth(settings.health, settings.death.Length / settings.framesPerSecond + .35f, 1);
        lastHealth = health.CurrentHealth;
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0;
        halo = new GameObject("Violet soul halo").AddComponent<SpriteRenderer>();
        halo.transform.SetParent(transform, false);
        halo.sprite = GrimDeathHazard.Disc;
        halo.sortingOrder = art.sortingOrder - 1;
        halo.transform.localPosition = new Vector3(0, 1.45f, 0);
        halo.transform.localScale = new Vector3(2.8f, 3.5f, 1);
        path = GrimDeathHazard.Ring(transform, "Shadow step destination", 12);
        path.enabled = false;
        attack.SetTeam(CombatTeam.Enemy);
        for (int i = 0; i < 16; i++)
            NewHazard();
        health.HealthChanged += OnHealth;
        State = GrimDeathState.Dormant;
    }

    // Called by the encounter. The boss cannot be damaged until Activate.
    public void Initialize(Collider2D platform, PlayerController target)
    {
        floor = platform;
        player = target != null ? target.GetComponent<PlayerHealth>() : null;
        initialized = true;
        health.DamageEnabled = false;
    }

    public void Activate()
    {
        activated = true;
        health.DamageEnabled = true;
    }

    private GrimDeathHazard NewHazard()
    {
        var h = new GameObject("Pooled shadow spell").AddComponent<GrimDeathHazard>();
        h.transform.SetParent(transform.parent, false);
        h.Configure(this);
        hazards.Add(h);
        pool.Push(h);
        return h;
    }

    // Takes a hazard from the pool (growing it up to 64) and arms it; returns null if the pool is exhausted.
    public GrimDeathHazard Hazard(GrimHazardKind kind, Vector2 origin, Vector2 size, float warning, float duration, Vector2 motion = default, float gravity = 0)
    {
        if (pool.Count == 0)
        {
            if (hazards.Count >= 64)
                return null;
            NewHazard();
        }
        var h = pool.Pop();
        h.Arm(kind, origin, size, warning, duration, motion, gravity);
        return h;
    }

    public void Recycle(GrimDeathHazard h) => pool.Push(h);

    public void Play(AudioClip clip, float multiplier = 1)
    {
        if (AscendJigoku.Gameplay.VictoryFlow.IsPlaying || clip == null || voice == null)
            return;
        // A whole pillar row erupts together; one sound represents the row without stacking 19 copies.
        if (clip == settings.pillarSound)
        {
            if (Time.time < nextPillarSoundAt)
                return;
            nextPillarSoundAt = Time.time + .1f;
        }
        voice.PlayOneShot(clip, settings.effectsVolume * multiplier * AscendJigoku.Settings.GameSettings.SfxVolume);
    }

    private void OnHealth(int hp, int max)
    {
        if (hp >= lastHealth)
        {
            lastHealth = hp;
            return;
        }
        lastHealth = hp;
        if (hp <= 0)
        {
            CancelMove();
            State = GrimDeathState.Dying;
            stateAt = Time.time;
            deadAt = Time.time;
            velocity = 0;
            ClearSpells();
            Play(settings.deathSound);
            return;
        }
        hurtUntil = Time.time + .1f;
        Play(settings.hitSound, .7f);
        // The first hit while Dormant wakes the boss up.
        if (State == GrimDeathState.Dormant && activated)
            Engage();
        // Taking damage does not erase an advertised attack. Parries create the long stun opening.
    }

    private void Engage()
    {
        Change(GrimDeathState.Entrance, settings.entranceSeconds);
        Play(settings.stepSound);
    }

    private void Change(GrimDeathState state, float seconds)
    {
        State = state;
        stateAt = Time.time;
        until = Time.time + Mathf.Max(0, seconds);
    }

    // Per-frame brain. Order: handle death / dead player, apply parry stun, then Dormant and Hunt logic;
    // timed states fall through to the switch at the bottom, which advances to the next state once `until` passes.
    private void Update()
    {
        if (settings == null || !initialized)
            return;
        if (State == GrimDeathState.Dying)
        {
            if (Time.time >= deadAt + settings.death.Length / settings.framesPerSecond)
            {
                State = GrimDeathState.Dead;
                Died?.Invoke();
            }
            return;
        }
        if (!IsAlive || player == null)
            return;
        // Player died: drop everything and become unhittable until the encounter resets.
        if (player.CurrentHealth <= 0)
        {
            CancelMove();
            ClearSpells();
            health.DamageEnabled = false;
            return;
        }
        if (!activated || Time.timeScale <= 0)
            return;
        if (attack.LastParryTime > lastParry && attack.LastParryTime > 0)
        {
            lastParry = attack.LastParryTime;
            Stun(settings.parryStunSeconds);
        }
        float dx = player.transform.position.x - transform.position.x;
        velocity = 0;
        if (State == GrimDeathState.Dormant)
        {
            if (Mathf.Abs(dx) <= settings.engageDistance)
                Engage();
            return;
        }
        if (State == GrimDeathState.Hunt)
        {
            if (Mathf.Abs(dx) > .15f)
                facing = dx > 0 ? 1 : -1;
            if (!HasLingeringDanger && Mathf.Abs(dx) > settings.stopDistance)
                velocity = facing * settings.moveSpeed;
            if (!AutomaticAttacks || HasLingeringDanger)
                return;
            // Shadow Step when the player is far (>15) or hugging the boss (<1.8, unless he was just hit).
            if (Time.time >= nextStepAt && Time.time >= nextMoveAt && (Mathf.Abs(dx) > 15 || (Mathf.Abs(dx) < 1.8f && Time.time - hurtUntil > .5f)))
            {
                BeginStep();
                return;
            }
            if (Time.time >= nextMoveAt)
            {
                var choice = rotation[moveIndex++ % rotation.Length];
                // Substitutions: no Summon while spirits live or on cooldown; no Scythe when the player is out of reach.
                if (choice == GrimDeathMove.Summon && (SpiritCount >= settings.maxSpirits || Time.time < nextSummonAt))
                    choice = GrimDeathMove.ShadowBolts;
                if (choice == GrimDeathMove.Scythe && Mathf.Abs(dx) > 6)
                    choice = GrimDeathMove.ReapingDash;
                TryStartMove(choice);
            }
            return;
        }
        if (State == GrimDeathState.Dash)
        {
            velocity = facing * settings.dashSpeed;
        }
        if (State == GrimDeathState.StepFade && Time.time >= until)
        {
            PlaceSafely(stepTarget.x);
            ShadowSteps++;
            RestoreBody();
            path.enabled = false;
            Change(GrimDeathState.StepRecovery, settings.stepRecovery);
            return;
        }
        if (Time.time < until)
            return;
        switch (State)
        {
            case GrimDeathState.Entrance:
                Change(GrimDeathState.Hunt, 0);
                nextMoveAt = Time.time + .35f;
                nextStepAt = Time.time + settings.stepCooldown;
                break;
            // Scythe: windup, cut, short gap, second cut (frame counts are at 60 FPS), then recovery.
            case GrimDeathState.ScytheWindup:
                Cut(false);
                Change(GrimDeathState.ScytheFirst, settings.scytheActiveFrames / 60f);
                break;
            case GrimDeathState.ScytheFirst:
                attack.CancelAll();
                Change(GrimDeathState.ScytheGap, settings.comboGapFrames / 60f);
                break;
            case GrimDeathState.ScytheGap:
                Cut(true);
                Change(GrimDeathState.ScytheSecond, settings.scytheActiveFrames / 60f);
                break;
            case GrimDeathState.ScytheSecond:
                Recover();
                break;
            // Dash: warning ends, then he turns intangible and charges, with a damaging box in front.
            case GrimDeathState.DashWindup:
                shape.enabled = false;
                health.DamageEnabled = false;
                invulnerable = true;
                attack.BeginAttack(
                    facing > 0 ? CombatDirection.Right : CombatDirection.Left,
                    new Vector2(facing * .8f, 1.35f),
                    new Vector2(3, 2.5f),
                    1,
                    Mathf.CeilToInt(settings.dashSeconds * 60) + 1,
                    ~0,
                    Time.time);
                Play(settings.scytheSound);
                Change(GrimDeathState.Dash, settings.dashSeconds);
                break;
            case GrimDeathState.Dash:
                PlaceSafely(body.position.x);
                Recover();
                break;
            case GrimDeathState.Cast:
                ReleaseSpell();
                Recover();
                break;
            // Shadow Step: warning ends, he fades out (intangible), then reappears in the StepFade branch above.
            case GrimDeathState.StepWindup:
                shape.enabled = false;
                health.DamageEnabled = false;
                invulnerable = true;
                Change(GrimDeathState.StepFade, settings.stepFadeSeconds);
                break;
            case GrimDeathState.Recovery:
            case GrimDeathState.StepRecovery:
            case GrimDeathState.Stunned:
                Change(GrimDeathState.Hunt, 0);
                nextMoveAt = Time.time + .15f;
                break;
        }
    }

    // Moves the body by velocity, clamped to the arena floor and glued to its top surface.
    private void FixedUpdate()
    {
        if (!initialized || floor == null || !IsAlive || player == null || player.CurrentHealth <= 0)
            return;
        float x = Mathf.Clamp(body.position.x + velocity * Time.fixedDeltaTime, floor.bounds.min.x + 1.4f, floor.bounds.max.x - 1.4f);
        body.MovePosition(new Vector2(x, floor.bounds.max.y + .06f));
    }

    // Starts the requested attack if the boss is free (Hunt or Recovery) and nothing dangerous is lingering.
    // Public so tests and debug tools can force a move.
    public bool TryStartMove(GrimDeathMove requested)
    {
        if (!initialized || !activated || !IsAlive || player == null || HasLingeringDanger || Time.timeScale <= 0
            || (State != GrimDeathState.Hunt && State != GrimDeathState.Recovery))
            return false;
        if (requested == GrimDeathMove.Summon && (Time.time < nextSummonAt || SpiritCount >= settings.maxSpirits))
            return false;
        CancelMove();
        move = requested;
        MoveCounts[(int)requested]++;
        lockedTarget = player.GetComponent<Collider2D>().bounds.center;
        facing = lockedTarget.x >= transform.position.x ? 1 : -1;
        if (requested == GrimDeathMove.Scythe)
        {
            Change(GrimDeathState.ScytheWindup, settings.scytheWindupFrames / 60f);
        }
        else if (requested == GrimDeathMove.ReapingDash)
        {
            Change(GrimDeathState.DashWindup, settings.dashWarning);
            Play(settings.stepSound, .65f);
        }
        else
        {
            Change(GrimDeathState.Cast, settings.castWarning);
            Play(settings.castSound);
            // Ground spells are armed now: their exact damaging footprint is visible throughout the cast.
            if (requested == GrimDeathMove.ShadowPillars)
                Pillars();
            if (requested == GrimDeathMove.Eclipse)
                Hazard(GrimHazardKind.Burst, (Vector2)transform.position + Vector2.up, Vector2.one * settings.burstRadius * 2, settings.burstWarning, settings.burstActive);
        }
        return true;
    }

    // One scythe swing; the second cut is lower and shorter-reaching than the first.
    private void Cut(bool second)
    {
        attack.CancelAll();
        attack.BeginAttack(
            facing > 0 ? CombatDirection.Right : CombatDirection.Left,
            new Vector2(facing * 2, second ? 1.15f : 1.6f),
            settings.scytheSize,
            1,
            settings.scytheActiveFrames,
            ~0,
            Time.time);
        Play(settings.scytheSound);
    }

    // Fires at the end of the Cast state. Pillars and Eclipse were already armed in TryStartMove.
    private void ReleaseSpell()
    {
        if (move == GrimDeathMove.ShadowBolts)
        {
            // Lobbed arcs aimed at the position locked when the move began; the volley fans out around it.
            Vector2 origin = (Vector2)transform.position + new Vector2(facing * .6f, 2.4f);
            float flight = Mathf.Clamp(Mathf.Abs(lockedTarget.x - origin.x) / settings.boltSpeed, .55f, 1.6f);
            Vector2 baseVelocity = new Vector2((lockedTarget.x - origin.x) / flight, (lockedTarget.y - origin.y + .5f * settings.boltGravity * flight * flight) / flight);
            for (int i = 0; i < settings.boltCount; i++)
            {
                float spread = i - (settings.boltCount - 1) * .5f;
                Hazard(GrimHazardKind.Bolt, origin, Vector2.one * .48f, 0, 3.5f, baseVelocity + new Vector2(spread * 1.8f, Mathf.Abs(spread) * .65f), settings.boltGravity);
            }
            Play(settings.stepSound, .6f);
        }
        else if (move == GrimDeathMove.Summon)
        {
            nextSummonAt = Time.time + settings.summonCooldown;
            for (int i = SpiritCount; i < settings.maxSpirits; i++)
                Summon(i);
            Play(settings.spiritSound);
        }
    }

    // A row of pillars across the arena, tall enough that a full jump cannot clear them. Alternate casts
    // shift the row by half a spacing so the safe gaps move.
    private void Pillars()
    {
        if (floor == null)
            return;
        float height = Mathf.Max(
            settings.pillarHeight,
            player.GetComponent<PlayerController>().MaxJumps * player.GetComponent<PlayerController>().JumpSpeed * player.GetComponent<PlayerController>().JumpSpeed
                / (2 * Mathf.Max(1, Mathf.Abs(Physics2D.gravity.y * player.GetComponent<Rigidbody2D>().gravityScale))) + 2);
        float shift = (volleyIndex++ % 2) * settings.pillarSpacing * .5f;
        for (float x = floor.bounds.min.x + 1.5f + shift; x < floor.bounds.max.x - 1; x += settings.pillarSpacing)
            Hazard(
                GrimHazardKind.Pillar,
                new Vector2(x, floor.bounds.max.y + height * .5f + .02f),
                new Vector2(settings.pillarWidth, height),
                Mathf.Max(settings.castWarning, settings.pillarWarning),
                settings.pillarActive);
    }

    // Spawns one spirit near the player, alternating left and right and spreading further out per index.
    private void Summon(int index)
    {
        if (settings.spiritPrefab == null)
            return;
        float x = player.transform.position.x + (index % 2 == 0 ? -1 : 1) * (5 + index * 2);
        x = Mathf.Clamp(x, floor.bounds.min.x + 1, floor.bounds.max.x - 1);
        var go = Instantiate(settings.spiritPrefab, new Vector3(x, floor.bounds.max.y + settings.spiritHoverHeight, 0), Quaternion.identity, transform.parent);
        var spirit = go.GetComponent<GrimDeathSpirit>();
        spirit.Initialize(this, index * .22f);
        spirits.Add(spirit);
        var c = go.GetComponent<Collider2D>();
        var target = player.GetComponent<Collider2D>();
        if (c != null && target != null)
            Physics2D.IgnoreCollision(c, target, true);
    }

    // Picks a landing spot behind the player (opposite the way they face) and shows the violet rune there.
    private void BeginStep()
    {
        CancelMove();
        nextStepAt = Time.time + settings.stepCooldown;
        var control = player.GetComponent<PlayerController>();
        float side = control != null && control.IsFacingRight ? -1 : 1;
        float x = player.transform.position.x + side * settings.stepBehindDistance;
        x = Mathf.Clamp(x, floor.bounds.min.x + 1.5f, floor.bounds.max.x - 1.5f);
        stepTarget = new Vector2(x, floor.bounds.max.y + .06f);
        path.transform.position = stepTarget + Vector2.up * .15f;
        path.transform.localScale = new Vector3(2.1f, .35f, 1);
        path.startColor = path.endColor = settings.warningColor;
        path.enabled = true;
        Play(settings.stepSound);
        Change(GrimDeathState.StepWindup, settings.stepWarning);
    }

    // Teleports to `wanted` x, trying nearby offsets (alternating sides, 1.5 apart) until the spot
    // does not overlap the player or the ground layer (3).
    private void PlaceSafely(float wanted)
    {
        if (floor == null)
            return;
        float min = floor.bounds.min.x + 1.4f, max = floor.bounds.max.x - 1.4f;
        float x = Mathf.Clamp(wanted, min, max);
        for (int attempt = 0; attempt < 7; attempt++)
        {
            float candidate = Mathf.Clamp(x + (attempt == 0 ? 0 : (attempt % 2 == 0 ? 1 : -1) * ((attempt + 1) / 2) * 1.5f), min, max);
            Vector2 pos = new Vector2(candidate, floor.bounds.max.y + .06f);
            overlaps.Clear();
            Physics2D.OverlapBox(pos + shape.offset, shape.size * .98f, 0, new ContactFilter2D { useTriggers = false, useLayerMask = false }, overlaps);
            bool blocked = false;
            foreach (var other in overlaps)
                if (other != null && !other.transform.IsChildOf(transform) && (other.GetComponentInParent<PlayerHealth>() != null || other.gameObject.layer == 3))
                {
                    blocked = true;
                    break;
                }
            if (!blocked)
            {
                body.position = pos;
                transform.position = pos;
                Physics2D.SyncTransforms();
                return;
            }
        }
        // Keep the old safe body position if every destination is occupied.
    }

    // Summon gets a longer recovery (at least 1.1 s) than the other moves.
    private void Recover()
    {
        CancelMove();
        Change(GrimDeathState.Recovery, move == GrimDeathMove.Summon ? Mathf.Max(1.1f, settings.recoverySeconds) : settings.recoverySeconds);
    }

    public void Stun(float seconds)
    {
        if (!IsAlive)
            return;
        CancelMove();
        Change(GrimDeathState.Stunned, seconds);
        nextStepAt = Time.time + settings.stepCooldown;
    }

    // Aborts whatever attack is running and puts the body back to normal (collider on, damageable).
    public void CancelMove()
    {
        velocity = 0;
        attack.CancelAll();
        if (invulnerable && IsAlive)
            PlaceSafely(body.position.x);
        RestoreBody();
        if (path != null)
            path.enabled = false;
    }

    private void RestoreBody()
    {
        if (shape != null && IsAlive)
            shape.enabled = true;
        if (invulnerable && IsAlive)
            health.DamageEnabled = activated;
        invulnerable = false;
    }

    public void ClearSpells()
    {
        foreach (var h in hazards)
            if (h != null && h.gameObject.activeSelf)
                h.Despawn();
        foreach (var s in spirits)
            if (s != null)
                s.Vanish();
    }

    // Picks the sprite frame for the current state (frame index derived from time in state) and tints the art.
    private void LateUpdate()
    {
        if (settings == null || art == null)
            return;
        float elapsed = Time.time - stateAt;
        Sprite[] frames = settings.idle;
        int index = 0;
        switch (State)
        {
            case GrimDeathState.Dying:
            case GrimDeathState.Dead:
                frames = settings.death;
                index = Mathf.FloorToInt((Time.time - deadAt) * settings.framesPerSecond);
                break;
            case GrimDeathState.ScytheWindup:
                frames = settings.scythe;
                index = Mathf.Min(1, Mathf.FloorToInt(elapsed * 4));
                break;
            case GrimDeathState.ScytheFirst:
                frames = settings.scythe;
                index = 2 + Mathf.FloorToInt(elapsed * 24);
                break;
            case GrimDeathState.ScytheGap:
                frames = settings.scythe;
                index = 6 + Mathf.FloorToInt(elapsed * 6);
                break;
            case GrimDeathState.ScytheSecond:
                frames = settings.scythe;
                index = 9 + Mathf.FloorToInt(elapsed * 24);
                break;
            case GrimDeathState.DashWindup:
                frames = settings.skill;
                index = Mathf.Min(3, Mathf.FloorToInt(elapsed * 6));
                break;
            case GrimDeathState.Dash:
                frames = settings.skill;
                index = 4 + Mathf.FloorToInt(elapsed * 18);
                break;
            case GrimDeathState.Cast:
                frames = move == GrimDeathMove.Summon ? settings.summon : settings.skill;
                index = Mathf.RoundToInt(Mathf.Clamp01(elapsed / settings.castWarning) * (frames.Length - 1));
                break;
            case GrimDeathState.Recovery:
                frames = move == GrimDeathMove.ReapingDash ? settings.skill : settings.idle2;
                index = move == GrimDeathMove.ReapingDash
                    ? 10 + Mathf.FloorToInt(elapsed * 8)
                    : Mathf.FloorToInt(elapsed * settings.framesPerSecond) % Mathf.Max(1, frames.Length);
                break;
            case GrimDeathState.Hunt:
                frames = Mathf.Abs(velocity) > .1f ? settings.idle2 : settings.idle;
                index = Mathf.FloorToInt(Time.time * settings.framesPerSecond) % Mathf.Max(1, frames.Length);
                break;
            default:
                frames = settings.idle;
                index = Mathf.FloorToInt(Time.time * settings.framesPerSecond) % Mathf.Max(1, frames.Length);
                break;
        }
        if (frames != null && frames.Length > 0)
            art.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        art.flipX = facing < 0;
        art.color = Time.time < hurtUntil ? new Color(1, .65f, .9f) : new Color(1, 1, 1, State == GrimDeathState.StepFade ? .22f : 1);
        halo.color = new Color(.32f, .12f, .55f, IsAlive ? .32f + .04f * Mathf.Sin(Time.time * 4) : 0);
        halo.transform.localScale = new Vector3(2.8f, 3.5f, 1) * (State == GrimDeathState.StepFade ? 1.25f : 1);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.HealthChanged -= OnHealth;
        foreach (var h in hazards)
            if (h != null)
                Destroy(h.gameObject);
        foreach (var s in spirits)
            if (s != null)
                Destroy(s.gameObject);
    }
}
