using UnityEngine;
using UnityEngine.InputSystem;

public enum ParryPhase { Idle, Startup, Active, Recovery }

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(CombatAttack), typeof(PlayerHealth))]
public class PlayerParry : MonoBehaviour
{
    [Header("Parry Timing - Frames at 60 FPS")]
    [Min(0), SerializeField] private int startupFrames = 0;
    [Min(1), SerializeField] private int activeFrames = 10;
    [Min(0), SerializeField] private int recoveryFrames = 16;
    [Min(1), SerializeField] private int enemyStunFrames = 30;
    [Header("Feedback")]
    [SerializeField] private bool showParry = true;
    private PlayerActions controls;
    private InputAction parryInput;
    private CombatAttack attack;
    private PlayerHealth health;
    private Collider2D bodyCollider;
    private float activeStartsAt = float.NegativeInfinity;
    private float activeEndsAt = float.NegativeInfinity;
    private float recoveryEndsAt = float.NegativeInfinity;
    private Transform feedback;
    private readonly SpriteRenderer[] borders = new SpriteRenderer[4];
    private static Sprite borderSprite;
    public float LastSuccessTime { get; private set; } = float.NegativeInfinity;
    public ParryPhase Phase => PhaseAt(Time.time);
    public bool IsBusy => Phase != ParryPhase.Idle;
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
        bodyCollider = GetComponent<Collider2D>();
        controls = new PlayerActions();
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
        if (parryInput.WasPressedThisFrame()) TryBegin(Time.time);
    }
    public bool TryBegin(float now)
    {
        if (!isActiveAndEnabled || health.CurrentHealth <= 0 || PhaseAt(now) != ParryPhase.Idle
            || (attack.CurrentSwing != null && attack.CurrentSwing.IsActive(now))) return false;
        activeStartsAt = now + startupFrames / CombatAttack.ReferenceFramesPerSecond;
        activeEndsAt = activeStartsAt + Mathf.Max(1, activeFrames) / CombatAttack.ReferenceFramesPerSecond;
        recoveryEndsAt = activeEndsAt + recoveryFrames / CombatAttack.ReferenceFramesPerSecond;
        return true;
    }
    public bool TryDeflect(CombatAttack.Swing incoming, float now)
    {
        // Deflect every connecting attack in the same resolution batch, even after the first success enters recovery.
        if (!isActiveAndEnabled || health.CurrentHealth <= 0 || incoming == null
            || incoming.Owner == null || incoming.Owner.Team != CombatTeam.Enemy || !incoming.IsActive(now)
            || (PhaseAt(now) != ParryPhase.Active && Mathf.Abs(now - LastSuccessTime) > 0.00001f)) return false;
        incoming.Owner.Parry(incoming, now);
        float stunSeconds = enemyStunFrames / CombatAttack.ReferenceFramesPerSecond;
        incoming.Owner.GetComponent<EnemyCombat>()?.Stun(now, stunSeconds);
        incoming.Owner.ContactDamage?.Suppress(health, now + stunSeconds);
        LastSuccessTime = now;
        activeEndsAt = now;
        recoveryEndsAt = now + recoveryFrames / CombatAttack.ReferenceFramesPerSecond;
        return true;
    }
    public void Cancel()
    {
        activeStartsAt = activeEndsAt = recoveryEndsAt = float.NegativeInfinity;
        LastSuccessTime = float.NegativeInfinity;
        if (feedback != null) feedback.gameObject.SetActive(false);
    }
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
        bool visible = showParry && phase != ParryPhase.Idle;
        feedback.gameObject.SetActive(visible);
        if (!visible) return;
        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2((bounds.size.x + 0.12f) / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            (bounds.size.y + 0.12f) / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
        feedback.position = bounds.center;
        Color color = Time.time - LastSuccessTime < 0.1f ? Color.green
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
