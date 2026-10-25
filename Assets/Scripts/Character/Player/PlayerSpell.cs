using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerHealth), typeof(PlayerCombat))]
public sealed class PlayerSpell : MonoBehaviour
{
    [Header("Tap to Shoot / Hold to Heal")]
    [Tooltip("Tap and release for a shot; hold this long for one heal instead.")]
    [Min(0.1f), SerializeField] private float healHoldSeconds = 1.5f;
    [Tooltip("Delay before the focus visual and movement slowdown begin. Short taps remain responsive.")]
    [Min(0), SerializeField] private float focusGraceSeconds = 0.15f;
    [Min(1), SerializeField] private int healAmount = 1;
    [Range(0, 1), SerializeField] private float focusMovementMultiplier = 0.25f;
    [Min(0), SerializeField] private float cooldownSeconds = 0.3f;
    [Header("Projectile — Damage Follows Player Combat")]
    [Min(0.1f), SerializeField] private float projectileSpeed = 24;
    [Min(0.1f), SerializeField] private float projectileLifetime = 0.8f;
    [SerializeField] private Vector2 projectileSize = new Vector2(0.65f, 0.35f);
    [Min(0), SerializeField] private float muzzleClearance = 0.05f;
    [Min(1), SerializeField] private int maxProjectiles = 12;
    [SerializeField] private Color projectileColor = new Color(0.5f, 0.9f, 1);
    [Header("Focus Feedback")]
    [SerializeField] private Color focusColor = new Color(0.3f, 0.9f, 1, 0.3f);
    [SerializeField] private Color healColor = new Color(0.3f, 1, 0.4f, 0.65f);
    private PlayerActions controls;
    private InputAction castInput;
    private InputDevice holdDevice;
    private PlayerController controller;
    private PlayerHealth health;
    private PlayerCombat combat;
    private PlayerParry parry;
    private Collider2D body;
    private SpriteRenderer focusVisual;
    private Transform projectileRoot;
    private bool holding;
    private float pressedAt, nextUseAt, healFlashUntil;
    private readonly Stack<PlayerProjectile> pool = new Stack<PlayerProjectile>();
    private readonly List<PlayerProjectile> projectiles = new List<PlayerProjectile>();
    private static Sprite square;
    public bool IsHolding => holding;
    public bool IsFocusing => holding && Time.time >= pressedAt + Mathf.Min(focusGraceSeconds, healHoldSeconds);
    public float FocusMovementMultiplier => focusMovementMultiplier;
    public float ChargeProgress => holding ? Mathf.Clamp01((Time.time - pressedAt) / Mathf.Max(0.1f, healHoldSeconds)) : 0;
    public int ShotsFired { get; private set; }
    public int HealsCompleted { get; private set; }
    internal static Sprite Square
    {
        get
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            return square;
        }
    }
    private void Awake()
    {
        controller = GetComponent<PlayerController>(); health = GetComponent<PlayerHealth>();
        combat = GetComponent<PlayerCombat>(); parry = GetComponent<PlayerParry>(); body = GetComponent<Collider2D>();
        controls = new PlayerActions();
        castInput = controls.asset.FindAction("PlayerInput/CastOrHeal", true);
        focusVisual = new GameObject("Heal Focus").AddComponent<SpriteRenderer>();
        focusVisual.transform.SetParent(transform, false);
        focusVisual.sprite = Square;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) { focusVisual.sortingLayerID = renderer.sortingLayerID; focusVisual.sortingOrder = renderer.sortingOrder + 3; }
        focusVisual.gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        controls.Enable(); health.Damaged += OnDamaged;
        InputSystem.onDeviceChange += OnDeviceChange;
    }
    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        controls.Disable(); health.Damaged -= OnDamaged; Cancel(true);
    }
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (holding && device == holdDevice && (change == InputDeviceChange.Disconnected
            || change == InputDeviceChange.Removed || change == InputDeviceChange.Disabled)) Cancel();
    }
    private void OnDestroy()
    {
        controls.Dispose();
        if (projectileRoot != null) Destroy(projectileRoot.gameObject);
    }
    private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    private void OnDamaged(int amount) => Cancel();
    private void Update()
    {
        if (Time.timeScale <= 0 || !controller.isActiveAndEnabled || health.CurrentHealth <= 0) { Cancel(); return; }
        if (castInput.WasPressedThisFrame()) TryBegin(Time.time);
        if (holding && (controller.IsKnockedBack || controls.PlayerInput.Jump.WasPressedThisFrame()
            || controls.PlayerInput.Dash.WasPressedThisFrame() || controls.PlayerInput.Attack.WasPressedThisFrame()
            || controls.PlayerInput.Parry.WasPressedThisFrame())) Cancel();
        Tick(Time.time);
        if (castInput.WasReleasedThisFrame()) Release(Time.time);
    }
    public bool TryBegin(float now)
    {
        if (!isActiveAndEnabled || holding || Time.timeScale <= 0 || health.CurrentHealth <= 0
            || !controller.isActiveAndEnabled || controller.IsKnockedBack || now < nextUseAt
            || combat.Attack.IsAttacking || combat.RecoveryRemaining > 0 || (parry != null && parry.IsBusy)) return false;
        holding = true; pressedAt = now; holdDevice = castInput.activeControl?.device;
        return true;
    }
    public void Tick(float now)
    {
        if (holding && (Time.timeScale <= 0 || health.CurrentHealth <= 0 || !controller.isActiveAndEnabled || controller.IsKnockedBack))
        { Cancel(); return; }
        if (!holding || now - pressedAt < Mathf.Max(0.1f, healHoldSeconds)) return;
        // One result per press. Remaining held never repeatedly heals or emits a shot.
        holding = false; holdDevice = null; nextUseAt = now + Mathf.Max(0, cooldownSeconds);
        if (health.Heal(Mathf.Max(1, healAmount)))
        {
            HealsCompleted++;
            healFlashUntil = Time.time + 0.25f;
        }
    }
    public bool Release(float now)
    {
        if (!holding) return false;
        Tick(now); // Releasing exactly at the threshold is still a heal.
        if (!holding) return false;
        holding = false; holdDevice = null;
        if (health.CurrentHealth <= 0 || !controller.isActiveAndEnabled) return false;
        nextUseAt = now + Mathf.Max(0, cooldownSeconds);
        if (pool.Count == 0 && projectiles.Count >= Mathf.Max(1, maxProjectiles)) return false;
        if (projectileRoot == null) projectileRoot = new GameObject("Player Projectiles").transform;
        PlayerProjectile shot;
        if (pool.Count > 0) shot = pool.Pop();
        else
        {
            shot = new GameObject("Player Spell Projectile").AddComponent<PlayerProjectile>();
            shot.transform.SetParent(projectileRoot, false);
            shot.Configure(Recycle); projectiles.Add(shot);
        }
        Vector2 direction = controller.IsFacingRight ? Vector2.right : Vector2.left;
        Bounds bounds = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 size = new Vector2(Mathf.Max(0.05f, projectileSize.x), Mathf.Max(0.05f, projectileSize.y));
        var renderer = GetComponent<SpriteRenderer>();
        shot.Launch(transform, bounds.center, direction, bounds.extents.x + size.x * 0.5f + muzzleClearance,
            size, projectileSpeed, projectileLifetime, combat.AttackDamage, combat.EnemyLayers,
            combat.Attack.Settings.terrainLayers, projectileColor, renderer);
        ShotsFired++;
        return true;
    }
    private void Recycle(PlayerProjectile shot) => pool.Push(shot);
    public void Cancel(bool clearProjectiles = false)
    {
        holding = false; holdDevice = null;
        if (focusVisual != null) focusVisual.gameObject.SetActive(false);
        if (clearProjectiles)
        {
            healFlashUntil = 0;
            nextUseAt = 0;
            foreach (var shot in projectiles) if (shot != null && shot.gameObject.activeSelf) shot.Despawn();
        }
    }
    private void LateUpdate()
    {
        bool healed = Time.time < healFlashUntil;
        focusVisual.gameObject.SetActive(healed || IsFocusing);
        if (!focusVisual.gameObject.activeSelf) return;
        Bounds bounds = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
        focusVisual.transform.position = bounds.center;
        Vector3 scale = transform.lossyScale;
        float expansion = healed ? 1.3f : Mathf.Lerp(1.05f, 1.3f, ChargeProgress);
        focusVisual.transform.localScale = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1) * expansion;
        focusVisual.color = healed ? healColor : Color.Lerp(focusColor, healColor, ChargeProgress * 0.5f);
    }
}
