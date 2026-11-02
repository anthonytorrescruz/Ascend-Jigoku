using UnityEngine;

// Full-cell sprite frames retain a stable pivot; colliders never follow animation bounds.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(FireWormBoss), typeof(EnemyHealth))]
public sealed class FireWormPresentation : MonoBehaviour
{
    public FireWormPresentationSet presentation;
    private FireWormBoss boss;
    private EnemyHealth health;
    private SpriteRenderer visual;
    private AudioSource audioSource;
    private Rigidbody2D body;
    private FireWormState previousState;
    private float stateStarted, hurtUntil, diedAt = -1;
    private int lastHealth, lastShots;
    private Bounds deathBounds;
    private void Awake()
    {
        boss = GetComponent<FireWormBoss>(); health = GetComponent<EnemyHealth>(); lastHealth = health.CurrentHealth;
        body = GetComponent<Rigidbody2D>();
        var existing = transform.Find("Fire Worm Animated Sprite");
        var child = existing != null ? existing.gameObject : new GameObject("Fire Worm Animated Sprite"); child.transform.SetParent(transform, false);
        visual = child.GetComponent<SpriteRenderer>() ?? child.AddComponent<SpriteRenderer>();
        var placeholder = GetComponent<SpriteRenderer>();
        visual.sharedMaterial = placeholder.sharedMaterial; visual.sortingLayerID = placeholder.sortingLayerID; visual.sortingOrder = placeholder.sortingOrder;
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
        else { hurtUntil = Time.time + 0.12f; Play(presentation != null ? presentation.hitSound : null); }
    }
    private void Play(AudioClip clip)
    { if (presentation != null && clip != null) audioSource.PlayOneShot(clip, presentation.volume); }
    private void LateUpdate()
    {
        if (presentation == null) return;
        var state = boss.State;
        if (state != previousState)
        {
            previousState = state; stateStarted = Time.time;
            if (state == FireWormState.FireballWindup || state == FireWormState.VolleyWindup || state == FireWormState.SlamWindup) Play(presentation.windupSound);
            if (state == FireWormState.SlamActive) Play(presentation.slamSound);
        }
        if (boss.FireballsLaunched != lastShots) { lastShots = boss.FireballsLaunched; Play(presentation.fireSound); }
        bool winding = state == FireWormState.FireballWindup || state == FireWormState.VolleyWindup || state == FireWormState.SlamWindup;
        Sprite[] frames = diedAt >= 0 ? presentation.death : Time.time < hurtUntil ? presentation.hit
            : winding || state == FireWormState.SlamActive ? presentation.attack
            : Mathf.Abs(body.linearVelocityX) > 0.1f ? presentation.walk : presentation.idle;
        float elapsed = diedAt >= 0 ? Time.time - diedAt : Time.time - stateStarted;
        if (frames != null && frames.Length > 0)
        {
            int index;
            if (diedAt >= 0) index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed * presentation.framesPerSecond));
            else if (winding)
            {
                float duration = (state == FireWormState.VolleyWindup ? boss.Settings.volleyWindupFrames
                    : state == FireWormState.SlamWindup ? boss.Settings.slamWindupFrames : boss.Settings.fireballWindupFrames) / 60f;
                index = Mathf.Clamp(Mathf.FloorToInt(elapsed / Mathf.Max(0.1f, duration) * (frames.Length - 1)), 0, frames.Length - 1);
            }
            else index = Mathf.FloorToInt(elapsed * presentation.framesPerSecond) % frames.Length;
            visual.sprite = frames[index];
        }
        Bounds bounds = diedAt >= 0 ? deathBounds : boss.BodyBounds;
        // Art faces right. The head remains at the leading solid collider in either direction.
        visual.flipX = !boss.IsFacingRight;
        visual.transform.position = new Vector3(bounds.center.x + (boss.IsFacingRight ? 0.25f : -0.25f), bounds.min.y + 0.01f, transform.position.z);
        visual.color = Time.time < hurtUntil ? new Color(1, 0.6f, 0.6f) : Color.white;
    }
}
