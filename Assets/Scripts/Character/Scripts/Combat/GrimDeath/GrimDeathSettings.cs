using UnityEngine;

// Tuning, sprites and audio for the Grim Death boss, shared by GrimDeathBoss, GrimDeathEncounter and its spells.
[CreateAssetMenu(menuName = "Ascend Jigoku/Grim Death Boss")]
public sealed class GrimDeathSettings : ScriptableObject
{
    [Header("Encounter")]
    public string bossName = "GRIM DEATH";
    [Min(1)] public int health = 10;
    [Min(0)] public int checkpointMarkerIndex = 1;
    [Min(3)] public int rowsBeforeCheckpoint = 3;
    [Min(10)] public float ceilingHeight = 18;
    [Min(0)] public float engageDistance = 12;
    [Min(0)] public float entranceSeconds = 1.2f;
    [Header("Pursuit and Shadow Step")]
    [Min(0)] public float moveSpeed = 12;
    [Min(0)] public float stopDistance = 2.6f;
    [Min(.1f)] public float stepCooldown = 3;
    [Min(.1f)] public float stepWarning = .38f;
    [Min(0)] public float stepFadeSeconds = .18f;
    [Min(0)] public float stepRecovery = .28f;
    [Min(1)] public float stepBehindDistance = 3.4f;
    [Min(0)] public float recoverySeconds = .9f;
    [Min(0)] public float parryStunSeconds = .8f;
    [Header("Scythe Combo - Frames at 60 FPS")]
    [Min(1)] public int scytheWindupFrames = 27;
    [Min(1)] public int scytheActiveFrames = 8;
    [Min(1)] public int comboGapFrames = 21;
    public Vector2 scytheSize = new Vector2(4.2f, 2.8f);
    [Header("Reaping Dash")]
    [Min(.1f)] public float dashWarning = .6f;
    [Min(1)] public float dashSpeed = 42;
    [Min(.05f)] public float dashSeconds = .32f;
    [Header("Shadow Spells - All Available From Full Health")]
    [Min(.2f)] public float castWarning = .85f;
    [Range(3, 9)] public int boltCount = 5;
    [Min(1)] public float boltSpeed = 15;
    [Min(1)] public float boltGravity = 14;
    [Min(.1f)] public float pillarWidth = 1.1f;
    [Min(3)] public float pillarSpacing = 4;
    [Min(5)] public float pillarHeight = 12;
    [Min(.3f)] public float pillarWarning = 1.15f;
    [Min(.1f)] public float pillarActive = .48f;
    [Min(1)] public float burstRadius = 5.8f;
    [Min(.4f)] public float burstWarning = 1.25f;
    [Min(.1f)] public float burstActive = .3f;
    [Header("Spirits")]
    [Range(1, 6)] public int maxSpirits = 3;
    [Min(.1f)] public float summonCooldown = 9;
    [Min(1)] public float spiritSpeed = 10;
    [Min(.2f)] public float spiritWarning = .7f;
    [Min(1)] public float spiritLifetime = 14;
    [Tooltip("Feet above the arena floor while waiting; keep low enough for grounded melee.")]
    [Min(0)] public float spiritHoverHeight = .35f;
    public Vector2 spiritHurtboxSize = new Vector2(.9f, 1.4f);
    public Vector2 spiritHurtboxOffset = new Vector2(0, .7f);
    [Header("Presentation - Full Source Cells")]
    public Sprite[] idle, idle2, scythe, skill, summon, death, spiritAppear, spiritIdle, spiritDeath;
    [Min(1)] public float framesPerSecond = 10;
    public Color shadowColor = new Color(.52f, .26f, .9f, 1);
    public Color warningColor = new Color(1, .74f, .32f, .9f);
    [Header("Audio")]
    public AudioClip music, scytheSound, stepSound, castSound, spiritSound, spiritDeathSound, hitSound, deathSound, pillarSound, burstSound;
    [Range(0, 1)] public float musicVolume = .32f;
    [Range(0, 1)] public float effectsVolume = .55f;
    public GameObject bossPrefab;
    public GameObject spiritPrefab;
}
