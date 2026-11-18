using UnityEngine;

// Plays an enemy's attack/hurt/death clips with slight random pitch variation and a short minimum gap between cues.
// Called by EnemyCombat (attack) and EnemyHealth (hurt/death).
[RequireComponent(typeof(AudioSource))]
public class EnemySounds : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private AudioClip deathClip;

    [Header("Variation")]
    [SerializeField, Range(0f, 0.3f)] private float pitchVariation = 0.1f;

    private AudioSource source;
    private float nextCueAt;
    [SerializeField, Range(0,1)] private float cueVolume = .75f;

    void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    public void PlayAttack()
    {
         Play(attackClip);
    }
    public void PlayHurt()   => Play(hurtClip);

    // Ordinary enemies can be destroyed on the killing hit. Play the final cue on
    // the existing shared audio host so destroying their body does not cut it off.
    public void PlayDeath() => CombatFeedback.PlayClip(deathClip, source != null ? source.volume : 1f);

    private void Play(AudioClip clip)
    {
        if (AscendJigoku.Gameplay.VictoryFlow.IsPlaying || clip == null || Time.unscaledTime < nextCueAt) return;
        nextCueAt = Time.unscaledTime + .08f;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, cueVolume * AscendJigoku.Settings.GameSettings.SfxVolume);
    }
}
