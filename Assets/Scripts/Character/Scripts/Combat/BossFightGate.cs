using System;
using UnityEngine;
using UnityEngine.UI;

// Owns only this encounter. Checkpoint/route unlocking remains in the level generator.
public sealed class BossFightGate : MonoBehaviour
{
    private PlayerController player;
    private EnemyHealth health;
    private Action cleared;
    private BossEncounterSettings settings;
    private Collider2D floor;
    private GameObject ceiling, leftWall, rightWall, uiRoot;
    private Text status;
    private Slider healthBar;
    private float clearedUntil;
    public FireWormBoss Boss { get; private set; }
    public bool Started { get; private set; }
    public bool Defeated { get; private set; }
    public void Initialize(BossEncounterSettings config, Collider2D platform, PlayerController actor, Action onCleared)
    {
        settings = config; floor = platform; player = actor; cleared = onCleared;
        var source = platform.GetComponent<SpriteRenderer>();
        ceiling = Barrier("Checkpoint Seal", new Vector2(0, config.ceilingHeight), new Vector2(floor.bounds.size.x + 2, 0.4f), source.sharedMaterial);
        leftWall = Barrier("Arena Left Seal", new Vector2(-config.combatWidth * 0.5f, config.ceilingHeight * 0.5f), new Vector2(0.4f, config.ceilingHeight), source.sharedMaterial);
        rightWall = Barrier("Arena Right Seal", new Vector2(config.combatWidth * 0.5f, config.ceilingHeight * 0.5f), new Vector2(0.4f, config.ceilingHeight), source.sharedMaterial);
        leftWall.SetActive(false); rightWall.SetActive(false);
        var actorObject = Instantiate(config.bossPrefab, transform.position + new Vector3(7, 1.85f, 0), Quaternion.identity, transform);
        Boss = actorObject.GetComponent<FireWormBoss>(); Boss.BindArena(floor); Boss.SetEncounterActive(false);
        health = actorObject.GetComponent<EnemyHealth>(); health.DamageEnabled = false; health.HealthChanged += OnHealth;
        CreateUI(); UpdateHealth(health.CurrentHealth, health.MaxHealth);
    }
    private GameObject Barrier(string name, Vector2 position, Vector2 size, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.localPosition = position; go.layer = 3;
        go.AddComponent<BoxCollider2D>().size = size;
        var sprite = go.AddComponent<SpriteRenderer>(); sprite.sprite = settings.barrierSprite; sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = size; sprite.sharedMaterial = material; sprite.color = settings.barrierColor; sprite.sortingOrder = 3;
        return go;
    }
    private void Update()
    {
        if (player == null || floor == null || Time.timeScale <= 0) return;
        float x = Mathf.Abs(player.transform.position.x - transform.position.x);
        bool nearby = Mathf.Abs(player.transform.position.y - transform.position.y) < 12;
        if (!Started && !Defeated && x < settings.combatWidth * 0.5f - 1 && player.IsGrounded
            && player.transform.position.y > floor.bounds.max.y && player.transform.position.y < floor.bounds.max.y + 4) BeginFight();
        uiRoot.SetActive(nearby && (!Defeated || Time.time < clearedUntil));
        if (Defeated) { status.text = "FIRE WORM DEFEATED - the checkpoint path is open"; return; }
        if (!Started) { status.text = "Enter the arena. Defeat the Fire Worm to open the blue checkpoint."; return; }
        string tell = Boss.State == FireWormState.SlamWindup ? "SLAM - jump back, parry, or pogo. Watch for ground waves."
            : Boss.State == FireWormState.VolleyWindup ? "FAN VOLLEY - move through the gaps, parry, or pogo."
            : Boss.State == FireWormState.FireballWindup ? "AIMED FIREBALL - dodge after the windup."
            : Boss.State == FireWormState.Recovery || Boss.State == FireWormState.Stunned ? "OPENING - counterattack!"
            : Boss.State == FireWormState.Transition ? "PHASE CHANGE - recover and reposition." : "";
        status.text = $"FIRE WORM  {health.CurrentHealth}/{health.MaxHealth}   Phase {Boss.HealthPhase}/3\n{tell}";
    }
    public void BeginFight()
    {
        if (Started || Defeated) return;
        Started = true; leftWall.SetActive(true); rightWall.SetActive(true);
        health.DamageEnabled = true; Boss.SetEncounterActive(true);
    }
    private void OnHealth(int current, int maximum)
    {
        UpdateHealth(current, maximum);
        if (current > 0 || Defeated) return;
        Defeated = true; clearedUntil = Time.time + 4;
        ceiling.SetActive(false); leftWall.SetActive(false); rightWall.SetActive(false); cleared?.Invoke();
    }
    private void UpdateHealth(int value, int maximum)
    { if (healthBar != null) { healthBar.maxValue = maximum; healthBar.value = value; } }
    private void OnDestroy()
    { if (health != null) health.HealthChanged -= OnHealth; if (uiRoot != null) Destroy(uiRoot); }
    private void CreateUI()
    {
        uiRoot = new GameObject("Boss Encounter HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = uiRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = uiRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900);
        var textObject = new GameObject("Boss status", typeof(RectTransform), typeof(Text)); textObject.transform.SetParent(uiRoot.transform, false);
        var rect = (RectTransform)textObject.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -28); rect.sizeDelta = new Vector2(1100, 90);
        status = textObject.GetComponent<Text>(); status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); status.fontSize = 25; status.alignment = TextAnchor.UpperCenter; status.color = Color.white;
        var bar = new GameObject("Boss health", typeof(RectTransform), typeof(Image), typeof(Slider)); bar.transform.SetParent(uiRoot.transform, false);
        rect = (RectTransform)bar.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1); rect.anchoredPosition = new Vector2(0, -115); rect.sizeDelta = new Vector2(780, 18);
        bar.GetComponent<Image>().color = new Color(0.12f, 0.05f, 0.04f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(bar.transform, false);
        var fillRect = (RectTransform)fill.transform; fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.95f, 0.22f, 0.08f);
        healthBar = bar.GetComponent<Slider>(); healthBar.fillRect = fillRect; healthBar.interactable = false; healthBar.transition = UnityEngine.UI.Selectable.Transition.None;
    }
}
