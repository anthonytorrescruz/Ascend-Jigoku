using UnityEngine;

// Full-cell sprite frames retain a stable pivot; colliders never follow animation bounds.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(FireWormBoss), typeof(EnemyHealth))]
public sealed class FireWormPresentation : MonoBehaviour
{
    public FireWormPresentationSet presentation;
    private FireWormBoss boss;
    private EnemyHealth health;
    private SpriteRenderer visual, impact;
    private AudioSource audioSource;
    private Rigidbody2D body;
    private FireWormState previousState;
    private float stateStarted, hurtStarted, hurtUntil, diedAt = -1, impactStarted = -10;
    private Vector3 impactPoint;
    private bool slamRecovery, castRelease;
    private int lastHealth, lastShots;
    private Bounds deathBounds;
    private void Awake()
    {
        boss = GetComponent<FireWormBoss>(); health = GetComponent<EnemyHealth>(); lastHealth = health.CurrentHealth;
        body = GetComponent<Rigidbody2D>();
        var existing = transform.Find("Fire Worm Animated Sprite");
        var child = existing != null ? existing.gameObject : new GameObject("Fire Worm Animated Sprite"); child.transform.SetParent(transform, false);
        visual = child.GetComponent<SpriteRenderer>(); if (visual == null) visual = child.AddComponent<SpriteRenderer>();
        var placeholder = GetComponent<SpriteRenderer>();
        visual.sharedMaterial = placeholder.sharedMaterial; visual.sortingLayerID = placeholder.sortingLayerID; visual.sortingOrder = placeholder.sortingOrder;
        var impactObject = new GameObject("Slam Impact Animation"); impactObject.transform.SetParent(transform, false);
        impact = impactObject.AddComponent<SpriteRenderer>(); impact.sharedMaterial = visual.sharedMaterial;
        impact.sortingLayerID = visual.sortingLayerID; impact.sortingOrder = visual.sortingOrder + 2; impactObject.SetActive(false);
        placeholder.enabled = false;
        var tail = transform.Find("Tail"); if (tail != null) tail.GetComponent<SpriteRenderer>().enabled = false;
        audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0; audioSource.pitch = 0.85f;
    }
    private void OnEnable() => health.HealthChanged += HealthChanged;
    private void OnDisable() { if (health != null) health.HealthChanged -= HealthChanged; }
    private void HealthChanged(int current, int maximum)
    {
        if (current >= lastHealth) { lastHealth = current; return; }
        lastHealth = current;
        if (current <= 0) { diedAt = Time.time; deathBounds = boss.BodyBounds; Play(presentation != null ? presentation.deathSound : null); }
        else { hurtStarted = Time.time; hurtUntil = Time.time + (presentation != null && presentation.hit != null ? Mathf.Max(.12f, presentation.hit.Length / Mathf.Max(1, presentation.framesPerSecond)) : .12f); Play(presentation != null ? presentation.hitSound : null); }
    }
    private void Play(AudioClip clip)
    { if (presentation != null && clip != null) audioSource.PlayOneShot(clip, presentation.volume * AscendJigoku.Settings.GameSettings.SfxVolume); }
    private void LateUpdate()
    {
        if (presentation == null) return;
        var state = boss.State;
        if (state != previousState)
        {
            slamRecovery = state == FireWormState.Recovery && previousState == FireWormState.SlamActive;
            castRelease = state == FireWormState.Recovery && (previousState == FireWormState.FireballWindup || previousState == FireWormState.VolleyWindup);
            previousState = state; stateStarted = Time.time;
            if (state == FireWormState.FireballWindup || state == FireWormState.VolleyWindup || state == FireWormState.SlamWindup) Play(presentation.windupSound);
            if (state == FireWormState.SlamActive)
            {
                Play(presentation.slamSound); impactStarted = Time.time;
                impactPoint = boss.GetComponent<CombatAttack>().CurrentSwing != null
                    ? (Vector3)boss.GetComponent<CombatAttack>().CurrentSwing.Box.center : boss.BodyBounds.center;
            }
        }
        if (boss.FireballsLaunched != lastShots) { lastShots = boss.FireballsLaunched; Play(presentation.fireSound); }
        bool slam = state == FireWormState.SlamWindup || state == FireWormState.SlamActive || slamRecovery;
        bool winding = state == FireWormState.FireballWindup || state == FireWormState.VolleyWindup || state == FireWormState.SlamWindup;
        Sprite[] castFrames = HasFrames(presentation.fireballAttack) ? presentation.fireballAttack : presentation.attack;
        Sprite[] slamFrames = HasFrames(presentation.slamAttack) ? presentation.slamAttack : presentation.attack;
        Sprite[] frames = diedAt >= 0 ? presentation.death : Time.time < hurtUntil ? presentation.hit
            : slam ? slamFrames : winding || castRelease ? castFrames
            : Mathf.Abs(body.linearVelocityX) > .1f ? presentation.walk : presentation.idle;
        float elapsed = diedAt >= 0 ? Time.time - diedAt : Time.time < hurtUntil ? Time.time - hurtStarted : Time.time - stateStarted;
        if (HasFrames(frames))
        {
            int index;
            if (diedAt >= 0 || Time.time < hurtUntil) index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * presentation.framesPerSecond));
            else if (winding)
            {
                float duration = (state == FireWormState.VolleyWindup ? boss.Settings.volleyWindupFrames
                    : state == FireWormState.SlamWindup ? boss.Settings.slamWindupFrames : boss.Settings.fireballWindupFrames) / 60f;
                int last = state == FireWormState.SlamWindup ? frames.Length - 1 : Mathf.Max(0, Mathf.CeilToInt(frames.Length * .625f) - 1);
                index = Mathf.Clamp(Mathf.RoundToInt(elapsed / Mathf.Max(.01f, duration) * last), 0, last);
            }
            else if (slam) index = state == FireWormState.SlamActive ? frames.Length - 1 : Mathf.RoundToInt((1 - Mathf.Clamp01(elapsed / .25f)) * (frames.Length - 1));
            else if (castRelease)
            {
                int split = Mathf.Min(frames.Length - 1, Mathf.CeilToInt(frames.Length * .625f));
                index = Mathf.Min(frames.Length - 1, split + Mathf.FloorToInt(elapsed * presentation.framesPerSecond));
            }
            else index = Mathf.FloorToInt(elapsed * presentation.framesPerSecond) % frames.Length;
            visual.sprite = frames[index];
        }
        Bounds bounds = diedAt >= 0 ? deathBounds : boss.BodyBounds;
        float facing = boss.IsFacingRight ? 1 : -1;
        Vector3 position = new Vector3(bounds.center.x + facing * .25f, bounds.min.y + .01f, transform.position.z);
        Vector3 scale = Vector3.one; float lean = 0;
        if (diedAt < 0 && state == FireWormState.SlamWindup)
        {
            float progress = Mathf.Clamp01((Time.time - stateStarted) / Mathf.Max(.01f, boss.Settings.slamWindupFrames / 60f));
            position.y += presentation.slamLift * progress; lean = facing * 8 * progress;
            scale = new Vector3(1 - .05f * progress, 1 + .08f * progress, 1);
        }
        else if (diedAt < 0 && (state == FireWormState.SlamActive || slamRecovery))
        {
            float strength = state == FireWormState.SlamActive ? 1 : 1 - Mathf.Clamp01((Time.time - stateStarted) / .25f);
            scale = new Vector3(1 + .18f * strength, Mathf.Lerp(1, presentation.slamSquash, strength), 1);
            lean = -facing * presentation.slamLeanDegrees * strength; position.x += facing * .65f * strength;
        }
        visual.flipX = !boss.IsFacingRight; visual.transform.position = position;
        visual.transform.localScale = scale; visual.transform.localRotation = Quaternion.Euler(0, 0, lean);
        visual.color = Time.time < hurtUntil && diedAt < 0 ? new Color(1, .6f, .6f) : Color.white;
        UpdateImpact();
    }
    private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
    private void UpdateImpact()
    {
        int index = Mathf.FloorToInt((Time.time - impactStarted) * presentation.framesPerSecond);
        bool active = HasFrames(presentation.explosion) && index >= 0 && index < presentation.explosion.Length;
        impact.gameObject.SetActive(active); if (!active) return;
        impact.sprite = presentation.explosion[index]; impact.transform.position = impactPoint;
        Vector2 size = impact.sprite.bounds.size;
        Vector2 fraction = presentation.explosionVisibleFraction;
        impact.transform.localScale = new Vector3(boss.Settings.slamSize.x / Mathf.Max(.01f, size.x * fraction.x),
            boss.Settings.slamSize.y / Mathf.Max(.01f, size.y * fraction.y), 1);
    }
}
