using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using UnityEngine;

// Optional clips replace the small placeholder sounds. Set volume to zero to mute them.
[DisallowMultipleComponent]
public sealed class CombatFeedback : MonoBehaviour
{
    [Range(0, 1), SerializeField] private float volume = 0.12f;
    [SerializeField] private AudioClip hitSound, parrySound, pogoSound;
    private static AudioClip defaultHit, defaultParry, defaultPogo;
    private SpriteRenderer body;
    private PlayerHealth health;
    private PlayerParry parry;
    private static AudioSource audioSource;
    private Color originalColor, flashColor;
    private float flashUntil, windupStart, windupUntil;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        health = GetComponent<PlayerHealth>();
        parry = GetComponent<PlayerParry>();
        if (body != null) originalColor = body.color;
    }

    public void Hit()
    {
        Flash(Color.white, 0.08f);
        Play(hitSound, ref defaultHit, 150, 60, 0.07f);
        // Health is reduced before Hit() runs, so zero here means this was the killing blow.
        if (health != null) EncounterFeedback.OnPlayerHit(health.CurrentHealth <= 0);
        else
        {
            var enemy = GetComponent<EnemyHealth>();
            EncounterFeedback.OnEnemyHit(this, enemy != null && enemy.CurrentHealth <= 0);
        }
    }
    public void Parry()
    {
        Flash(new Color(0.5f, 1, 0.8f), 0.12f);
        Play(parrySound, ref defaultParry, 550, 1100, 0.1f);
        EncounterFeedback.OnParry();
    }
    public void Pogo()
    {
        Flash(new Color(0.6f, 0.85f, 1), 0.08f);
        Play(pogoSound, ref defaultPogo, 250, 650, 0.08f);
        EncounterFeedback.OnPogo();
    }
    public void Windup(float seconds)
    {
        windupStart = Time.time;
        windupUntil = Time.time + seconds;
        if (health == null) TelegraphMarker.Show(gameObject, seconds);
    }
    public void CancelWindup() { windupUntil = 0; TelegraphMarker.Hide(gameObject); }
    public void ResetFeedback()
    {
        flashUntil = windupUntil = 0;
        TelegraphMarker.Hide(gameObject);
        if (body != null) body.color = originalColor;
    }
    private void Flash(Color color, float seconds)
    {
        flashColor = color;
        flashUntil = Time.time + seconds;
    }
    private void LateUpdate()
    {
        if (body == null) return;
        bool gentle = GameSettings.ReduceFlashing;
        if (Time.time < flashUntil) body.color = gentle ? Color.Lerp(originalColor, flashColor, 0.45f) : flashColor;
        else if (Time.time < windupUntil)
        {
            float progress = Mathf.InverseLerp(windupStart, windupUntil, Time.time);
            body.color = Color.Lerp(originalColor, new Color(1, 0.65f, 0.15f), 0.35f + progress * 0.65f);
        }
        else if (health != null && health.ProtectionRemaining > 0)
            body.color = Color.Lerp(originalColor, Color.white, gentle ? 0.35f : 0.35f + 0.35f * Mathf.Sin(Time.time * 60));
        else if (parry != null && parry.Phase == ParryPhase.Active)
            body.color = Color.Lerp(originalColor, Color.cyan, 0.65f);
        else body.color = originalColor;
    }
    private void Play(AudioClip clip, ref AudioClip fallback, float startHz, float endHz, float duration)
    {
        if (volume <= 0) return;
        if (clip == null)
        {
            if (fallback == null)
            {
                const int rate = 22050;
                var samples = new float[Mathf.CeilToInt(duration * rate)];
                float phase = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = i / (float)samples.Length;
                    phase += 2 * Mathf.PI * Mathf.Lerp(startHz, endHz, t) / rate;
                    samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * (1 - t);
                }
                fallback = AudioClip.Create("Combat cue", samples.Length, 1, rate, false);
                fallback.SetData(samples, 0);
            }
            clip = fallback;
        }
        PlayClip(clip, volume);
    }
    public static void PlayClip(AudioClip clip, float clipVolume)
    {
        if (clip == null || clipVolume <= 0) return;
        if (audioSource == null)
        {
            var host = new GameObject("Combat Audio");
            DontDestroyOnLoad(host);
            audioSource = host.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
        }
        float scaled = Mathf.Clamp01(clipVolume) * GameSettings.SfxVolume;
        if (scaled > 0) audioSource.PlayOneShot(clip, scaled);
    }
    private void OnDisable() => ResetFeedback();
}
