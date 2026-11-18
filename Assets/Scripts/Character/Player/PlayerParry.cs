using UnityEngine;
using UnityEngine.InputSystem;

public enum ParryPhase { Idle, Startup, Active, Recovery }

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
// Parry: a press runs startup -> active -> recovery. An enemy swing that lands during the active window is deflected and the enemy
// is stunned. Phases are tracked as Time.time timestamps (seconds) built from the frame counts below (60 FPS).
[RequireComponent(typeof(PlayerController), typeof(CombatAttack), typeof(PlayerHealth))]
public class PlayerParry : MonoBehaviour
{
    [Header("Parry Timing - Frames at 60 FPS")]
    [Min(0), SerializeField] private int startupFrames = 0;
    [Min(1), SerializeField] private int activeFrames = 10;
    [Min(0), SerializeField] private int recoveryFrames = 16;
    [Min(1), SerializeField] private int enemyStunFrames = 30;
    [Header("Successful Parry - Frames at 60 FPS")]
    [Tooltip("Brief protection against follow-up attacks and body contact. A miss grants none.")]
    [Min(0), SerializeField] private int successProtectionFrames = 24;
    [Tooltip("Recovery after a success, so the player can counterattack quickly. Misses use Recovery Frames above.")]
    [Min(0), SerializeField] private int successRecoveryFrames = 6;
    [Header("Feedback")]
    [SerializeField] private bool showParry = true;
    private PlayerActions controls;
    private InputAction parryInput;
    private CombatAttack attack;
    private PlayerController playerController;
    private PlayerHealth health;
    private Collider2D bodyCollider;
    // Timestamps (Time.time) bounding the current parry: active starts, active ends, recovery ends (back to Idle).
    private float activeStartsAt = float.NegativeInfinity;
    private float activeEndsAt = float.NegativeInfinity;
    private float recoveryEndsAt = float.NegativeInfinity;
    // A parry press stays queued until this time, so it still fires if pressed during another action.
    private float bufferedUntil = float.NegativeInfinity;
    // After a timed success, follow-up enemy attacks are still deflected until this time.
    private float successfulParryUntil = float.NegativeInfinity;
    private Transform feedback;
    private readonly SpriteRenderer[] borders = new SpriteRenderer[4];
    private static Sprite borderSprite;
    public float LastSuccessTime { get; private set; } = float.NegativeInfinity;
    public float SuccessProtectionRemaining => Mathf.Max(0, successfulParryUntil - Time.time);
    public ParryPhase Phase => PhaseAt(Time.time);
    public bool IsBusy => Phase != ParryPhase.Idle;
    public float RecoveryRemaining => Mathf.Max(0, recoveryEndsAt - Time.time);
    [UnityEngine.Serialization.FormerlySerializedAs("perryClip")]
    public AudioClip parryClip;
    [Range(0, 1), SerializeField] private float parryVolume = 0.12f;

    public ParryPhase PhaseAt(float now)
    {
        if (now >= recoveryEndsAt) return ParryPhase.Idle;
        if (now < activeStartsAt) return ParryPhase.Startup;
        return now < activeEndsAt ? ParryPhase.Active : ParryPhase.Recovery;
    }

    private void Awake()
    {
        attack = GetComponent<CombatAttack>();
        health = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();
        bodyCollider = GetComponent<Collider2D>();
        controls = new PlayerActions();
        AscendJigoku.Settings.KeybindSettings.Register(controls.asset); // apply saved keybinds
        parryInput = controls.asset.FindAction("PlayerInput/Parry", true);
        CreateFeedback();
    }

    private void OnEnable() => controls.Enable();

    private void OnDisable()
    {
        controls.Disable();
        Cancel();
    }
    private void OnDestroy() => controls.Dispose();

    private void Update()
    {
        if (parryInput.WasPressedThisFrame())
        {
            QueueParry();
           //CombatFeedback.PlayClip(parryClip, parryVolume);
        } 
        Tick(Time.time);
    }

    public void QueueParry()
    {
        GetComponent<PlayerSpell>()?.Cancel();
        bufferedUntil = Time.time + Mathf.Max(0, attack.Settings.inputBufferSeconds);
    }

    public void Tick(float now)
    {
        if (now <= bufferedUntil && TryBegin(now)) bufferedUntil = float.NegativeInfinity;
    }

    // Starts a parry if idle and not mid-swing, laying out the three phase timestamps from the frame counts.
    public bool TryBegin(float now)
    {
        if (!isActiveAndEnabled || health.CurrentHealth <= 0 || PhaseAt(now) != ParryPhase.Idle
            || (attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))) return false;
        activeStartsAt = now + startupFrames / CombatAttack.ReferenceFramesPerSecond;
        activeEndsAt = activeStartsAt + Mathf.Max(1, activeFrames) / CombatAttack.ReferenceFramesPerSecond;
        recoveryEndsAt = activeEndsAt + recoveryFrames / CombatAttack.ReferenceFramesPerSecond;
        playerController.PlayParryAnimation();
        CombatFeedback.PlayClip(parryClip, parryVolume);
        return true;
    }

    // Called when an enemy swing connects. Returns true if it was deflected. Only a hit in the active window (timedSuccess) grants
    // protection and the short recovery; hits shortly after a success are still deflected as follow-ups.
    public bool TryDeflect(CombatAttack.Swing incoming, float now)
    {
        // A timed success covers a short cluster of attacks, not just hits in the same resolution batch.
        bool timedSuccess = PhaseAt(now) == ParryPhase.Active;
        bool followUp = now < successfulParryUntil || Mathf.Abs(now - LastSuccessTime) <= 0.00001f;
        if (!isActiveAndEnabled || health.CurrentHealth <= 0 || incoming == null
            || incoming.Owner == null || incoming.Owner.Team != CombatTeam.Enemy || !incoming.IsActive(now)
            || (!timedSuccess && !followUp)) return false;
        incoming.Owner.Parry(incoming, now);
        float stunSeconds = enemyStunFrames / CombatAttack.ReferenceFramesPerSecond;
        incoming.Owner.GetComponent<EnemyCombat>()?.Stun(now, stunSeconds);
        incoming.Owner.ContactDamage?.Suppress(health, now + stunSeconds);
        LastSuccessTime = now;
        if (timedSuccess)
        {
            float protection = successProtectionFrames / CombatAttack.ReferenceFramesPerSecond;
            successfulParryUntil = now + protection;
            health.ProtectFor(protection);
            GetComponent<CombatFeedback>()?.Parry();
            activeEndsAt = now; // end the active window now and recover quickly so the player can counterattack
            recoveryEndsAt = now + successRecoveryFrames / CombatAttack.ReferenceFramesPerSecond;
        }
        // Follow-up deflections neither extend protection nor restart recovery.
        return true;
    }

    public void Cancel()
    {
        activeStartsAt = activeEndsAt = recoveryEndsAt = float.NegativeInfinity;
        bufferedUntil = float.NegativeInfinity;
        LastSuccessTime = successfulParryUntil = float.NegativeInfinity;
        if (feedback != null) feedback.gameObject.SetActive(false);
    }

    // Builds a four-sided outline (child sprites) shown around the player during the parry window.
    private void CreateFeedback()
    {
        feedback = new GameObject("Parry Feedback").transform;
        feedback.SetParent(transform, false);
        if (borderSprite == null)
            borderSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
        var bodyRenderer = GetComponent<SpriteRenderer>();
        for (int i = 0; i < borders.Length; i++)
        {
            var edge = new GameObject("Parry Edge " + i).AddComponent<SpriteRenderer>();
            edge.transform.SetParent(feedback, false);
            edge.sprite = borderSprite;
            edge.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 2 : 2;
            if (bodyRenderer != null) edge.sortingLayerID = bodyRenderer.sortingLayerID;
            borders[i] = edge;
        }
        feedback.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        ParryPhase phase = Phase;
        bool protectedFollowUp = SuccessProtectionRemaining > 0;
        bool visible = showParry && (phase == ParryPhase.Active || protectedFollowUp
            || (AscendJigoku.Debugging.DebugMenu.IsOpen && phase != ParryPhase.Idle));
        feedback.gameObject.SetActive(visible);
        if (!visible) return;
        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2((bounds.size.x + 0.12f) / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            (bounds.size.y + 0.12f) / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
        feedback.position = bounds.center;
        Color color = protectedFollowUp ? new Color(.7f,1,.9f,.9f)
            : phase == ParryPhase.Active ? Color.cyan
            : phase == ParryPhase.Startup ? Color.yellow : new Color(0.6f, 0.6f, 0.6f, 0.7f);
        for (int i = 0; i < borders.Length; i++)
        {
            bool horizontal = i < 2;
            borders[i].color = color;
            borders[i].transform.localPosition = horizontal ? new Vector3(0, (i == 0 ? -1 : 1) * size.y * 0.5f, 0)
                : new Vector3((i == 2 ? -1 : 1) * size.x * 0.5f, 0, 0);
            borders[i].transform.localScale = horizontal ? new Vector3(size.x, 0.04f, 1) : new Vector3(0.04f, size.y, 1);
        }
    }
}
