using System.Collections.Generic;
using UnityEngine;

public enum CombatTeam { Player, Enemy }
public enum CombatDirection { Left, Right, Up, Down }

[DisallowMultipleComponent]
public class CombatAttack : MonoBehaviour
{
    public const float ReferenceFramesPerSecond = 60f;
    [SerializeField] private CombatTeam team;
    [SerializeField] private bool showAttack = true;
    [SerializeField] private CombatSettings settings;
    private static CombatSettings fallbackSettings;
    private Collider2D bodyCollider;

    public CombatSettings Settings
    {
        get
        {
            if (settings != null) return settings;
            if (fallbackSettings == null)
            {
                fallbackSettings = ScriptableObject.CreateInstance<CombatSettings>();
                fallbackSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            return fallbackSettings;
        }
    }
    public CombatFeedback Feedback { get; private set; }

    public CombatTeam Team => team;
    public float LastParryTime { get; private set; } = float.NegativeInfinity;
    public Swing CurrentSwing { get; private set; }
    public bool IsAttacking => CurrentSwing != null && CurrentSwing.IsActive(Time.time);
    public PlayerController PlayerController { get; private set; }
    public EnemyContactDamage ContactDamage { get; private set; }

    // One record per swing; overlap results and the visual/collider are reused.
    public sealed class Swing
    {
        public CombatAttack Owner;
        public CombatDirection Direction;
        public Vector2 Offset, Size;
        public Rect Box;
        public float StartedAt, EndsAt;
        public int Damage;
        public LayerMask DamageLayers;
        public bool Cancelled, Parried, PogoTriggered;
        public readonly HashSet<Component> TargetsHit = new HashSet<Component>();
        public bool IsActive(float now) => !Cancelled && Owner != null
            && Owner.isActiveAndEnabled && now >= StartedAt && now < EndsAt;
        public void RefreshBox() { if (Owner != null) Box = Owner.GetBox(Offset, Size); }
    }

    private Transform hitboxTransform;
    private BoxCollider2D hitboxCollider;
    private SpriteRenderer hitboxRenderer;
    private bool telegraphing;
    private Vector2 telegraphOffset, telegraphSize;
    private static Sprite sharedAttackSprite;

    private void Awake()
    {
        PlayerController = GetComponent<PlayerController>();
        ContactDamage = GetComponent<EnemyContactDamage>();
        bodyCollider = GetComponent<Collider2D>();
        Feedback = GetComponent<CombatFeedback>();
        if (Feedback == null) Feedback = gameObject.AddComponent<CombatFeedback>();
        var hitbox = new GameObject("Attack Hitbox");
        hitbox.layer = gameObject.layer;
        hitboxTransform = hitbox.transform;
        hitboxTransform.SetParent(transform, false);
        hitboxCollider = hitbox.AddComponent<BoxCollider2D>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
        hitbox.AddComponent<CombatAttackHitbox>().Owner = this;
        if (team == CombatTeam.Enemy) hitbox.AddComponent<Pogoable>();
        hitboxRenderer = hitbox.AddComponent<SpriteRenderer>();
        if (sharedAttackSprite == null)
            sharedAttackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        hitboxRenderer.sprite = sharedAttackSprite;
        var bodyRenderer = GetComponent<SpriteRenderer>();
        hitboxRenderer.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder + 1 : 1;
        if (bodyRenderer != null) hitboxRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        hitbox.SetActive(false);
    }

    public Swing BeginAttack(CombatDirection direction, Vector2 offset, Vector2 size,
        int damage, int activeFrames, LayerMask damageLayers, float now)
    {
        if (CurrentSwing != null && CurrentSwing.IsActive(now)) return null;
        telegraphing = false;
        CurrentSwing = new Swing {
            Owner = this, Direction = direction, Offset = offset,
            Size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y)),
            Damage = Mathf.Max(0, damage), DamageLayers = damageLayers,
            StartedAt = now, EndsAt = now + Mathf.Max(1, activeFrames) / ReferenceFramesPerSecond
        };
        CurrentSwing.RefreshBox();
        CombatResolver.Instance.Register(CurrentSwing);
        UpdateDisplay(now);
        return CurrentSwing;
    }

    public void SetTeam(CombatTeam value)
    {
        team = value;
        if (team == CombatTeam.Enemy && hitboxTransform != null
            && hitboxTransform.GetComponent<Pogoable>() == null)
            hitboxTransform.gameObject.AddComponent<Pogoable>();
    }

    public Rect GetBox(Vector2 offset, Vector2 size) => new Rect((Vector2)transform.position + offset - size * 0.5f, size);
    public Vector2 GetAttackOffset(CombatDirection direction, Vector2 size)
    {
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        Bounds body = bodyCollider != null && bodyCollider.enabled
            ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 offset = body.center - transform.position;
        float gap = Mathf.Max(0, Settings.bodyClearance);
        switch (direction)
        {
            case CombatDirection.Left: offset.x -= body.extents.x + size.x * 0.5f + gap; break;
            case CombatDirection.Right: offset.x += body.extents.x + size.x * 0.5f + gap; break;
            case CombatDirection.Up: offset.y += body.extents.y + size.y * 0.5f + gap; break;
            case CombatDirection.Down: offset.y -= body.extents.y + size.y * 0.5f + gap; break;
        }
        return offset;
    }
    public void ShowTelegraph(Vector2 offset, Vector2 size)
    {
        telegraphing = true;
        telegraphOffset = offset;
        telegraphSize = size;
        UpdateDisplay(Time.time);
    }
    public void CancelTelegraph() { telegraphing = false; UpdateDisplay(Time.time); }
    public void Parry(Swing swing, float now)
    {
        swing.Parried = true;
        swing.Cancelled = true;
        LastParryTime = now;
        UpdateDisplay(now);
    }
    public void Cancel(Swing swing)
    {
        swing.Cancelled = true;
        UpdateDisplay(Time.time);
    }
    public void CancelAll()
    {
        if (CurrentSwing != null) CurrentSwing.Cancelled = true;
        CurrentSwing = null;
        telegraphing = false;
        LastParryTime = float.NegativeInfinity;
        Feedback?.CancelWindup();
        if (hitboxTransform != null) hitboxTransform.gameObject.SetActive(false);
    }
    private void LateUpdate() => UpdateDisplay(Time.time);
    private void OnDisable()
    {
        CancelAll();
    }
    private void UpdateDisplay(float now)
    {
        if (hitboxTransform == null) return;
        bool active = CurrentSwing != null && CurrentSwing.IsActive(now);
        bool flash = now - LastParryTime < 0.08f;
        bool visible = active || telegraphing || flash;
        if (hitboxTransform.gameObject.activeSelf != visible) hitboxTransform.gameObject.SetActive(visible);
        if (!visible) return;
        Rect box = telegraphing ? GetBox(telegraphOffset, telegraphSize) : CurrentSwing.Box;
        if (active) { CurrentSwing.RefreshBox(); box = CurrentSwing.Box; }
        hitboxTransform.position = new Vector3(box.center.x, box.center.y, transform.position.z);
        Vector3 scale = transform.lossyScale;
        hitboxTransform.localScale = new Vector3(box.width / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            box.height / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f);
        hitboxCollider.enabled = active;
        hitboxRenderer.enabled = showAttack && AscendJigoku.Debugging.DebugMenu.IsOpen;
        hitboxRenderer.color = flash ? new Color(0, 1, 1, 0.65f)
            : telegraphing ? new Color(1, 0.75f, 0, 0.25f)
            : team == CombatTeam.Enemy ? new Color(1, 0.2f, 0.1f, 0.5f) : new Color(0.2f, 0.45f, 1, 0.55f);
    }
}
