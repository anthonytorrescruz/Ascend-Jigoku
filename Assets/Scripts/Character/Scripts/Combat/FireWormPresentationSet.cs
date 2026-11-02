using UnityEngine;

[CreateAssetMenu(menuName = "Ascend Jigoku/Fire Worm Presentation")]
public sealed class FireWormPresentationSet : ScriptableObject
{
    public Sprite[] idle, walk, attack, hit, death, fireball, explosion;
    [Header("Dedicated Attack Animations (legacy Attack is the fallback)")]
    public Sprite[] fireballAttack, slamAttack;
    [Min(0)] public float slamLift = 0.55f;
    [Range(0.2f, 1)] public float slamSquash = 0.55f;
    [Range(0, 45)] public float slamLeanDegrees = 18;
    [Tooltip("Visible art as a fraction of its full sprite cell; compensates for transparent padding without enlarging the hitbox.")]
    public Vector2 fireballVisibleFraction = new Vector2(18f / 46, 12f / 46);
    public Vector2 explosionVisibleFraction = new Vector2(27f / 46, 25f / 46);
    [Min(1)] public float framesPerSecond = 12;
    public AudioClip windupSound, fireSound, slamSound, hitSound, deathSound;
    [Range(0, 1)] public float volume = 0.45f;
    [Header("Encounter Music")]
    public AudioClip battleMusic;
    [Range(0, 1)] public float musicVolume = 0.32f;
    [Min(0.1f)] public float musicFadeSeconds = 1.2f;
}
