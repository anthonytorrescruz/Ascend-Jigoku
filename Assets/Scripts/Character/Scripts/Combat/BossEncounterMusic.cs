using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FireWormBoss), typeof(EnemyHealth), typeof(FireWormPresentation))]
public sealed class BossEncounterMusic : MonoBehaviour
{
    private FireWormBoss boss;
    private EnemyHealth health;
    private FireWormPresentationSet media;
    private AudioSource source;
    private readonly Dictionary<AudioSource, float> backgroundVolumes = new Dictionary<AudioSource, float>();
    private bool started, finished;
    private float faded;
    public bool IsPlaying => source != null && source.isPlaying;
    private void Awake()
    {
        boss = GetComponent<FireWormBoss>(); health = GetComponent<EnemyHealth>(); media = GetComponent<FireWormPresentation>().presentation;
        source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.volume = 0;
    }
    private void OnEnable() => health.HealthChanged += OnHealth;
    private void OnHealth(int hp, int maximum) { if (hp <= 0) finished = true; }
    public void Begin()
    {
        if (started || finished || media == null || media.battleMusic == null) return;
        started = true; source.clip = media.battleMusic;
        foreach (var other in FindObjectsByType<AudioSource>())
            if (other != source && other.isPlaying && other.loop && other.spatialBlend < .01f && other.clip != null)
            { backgroundVolumes[other] = other.volume; other.volume *= .08f; }
        source.Play();
    }
    private void Update()
    {
        var player = boss.Combat.Target;
        if (player != null && player.CurrentHealth <= 0) { Stop(); return; }
        if (!started && !finished && Time.timeScale > 0 && player != null && boss.Arena != null)
        {
            var point = player.transform.position; var floor = boss.Arena.bounds;
            var controller = player.GetComponent<PlayerController>();
            if (controller != null && controller.IsGrounded && point.x > floor.min.x && point.x < floor.max.x
                && point.y > floor.max.y && point.y < floor.max.y + 4) Begin();
        }
        if (!started || media == null) return;
        faded = Mathf.MoveTowards(faded, finished ? 0 : 1, Time.unscaledDeltaTime / Mathf.Max(.1f, media.musicFadeSeconds));
        source.volume = media.musicVolume * faded;
        if (finished && faded <= 0) Stop();
    }
    private void Stop()
    {
        if (source != null) source.Stop();
        foreach (var pair in backgroundVolumes) if (pair.Key != null) pair.Key.volume = pair.Value;
        backgroundVolumes.Clear(); finished = true;
    }
    private void OnDisable() { if (health != null) health.HealthChanged -= OnHealth; Stop(); }
}
