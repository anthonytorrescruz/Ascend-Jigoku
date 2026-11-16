using System;
using UnityEngine;

// Owns only this encounter. Checkpoint/route unlocking remains in the level generator.
public sealed class BossFightGate : MonoBehaviour
{
    private PlayerController player;
    private EnemyHealth health;
    private Action cleared;
    private BossEncounterSettings settings;
    private Collider2D floor;
    private GameObject ceiling, leftWall, rightWall;
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
        // Shown in the HUD's boss bar (AscendJigoku.UI.GameplayHud).
        if (!nearby || (Defeated && Time.time >= clearedUntil)) { AscendJigoku.UI.GameplayHud.HideBoss(this); return; }
        if (Defeated) { ShowBar("DEFEATED - the checkpoint path is open"); return; }
        if (!Started) { ShowBar("Enter the arena. Defeat the Fire Worm to open the blue checkpoint."); return; }
        string tell = Boss.State == FireWormState.SlamWindup ? "SLAM - jump back, parry, or pogo. Watch for ground waves."
            : Boss.State == FireWormState.VolleyWindup ? "FAN VOLLEY - move through the gaps, parry, or pogo."
            : Boss.State == FireWormState.FireballWindup ? "AIMED FIREBALL - dodge after the windup."
            : Boss.State == FireWormState.Recovery || Boss.State == FireWormState.Stunned ? "OPENING - counterattack!"
            : Boss.State == FireWormState.Transition ? "PHASE CHANGE - recover and reposition." : "";
        ShowBar(tell);
    }
    private void ShowBar(string tell)
    {
        var config = Boss != null ? Boss.Settings : null; // the worm can be gone while DEFEATED is still showing
        float[] phases = config != null && config.healthPhases ? new[] { config.phaseTwoHealth, config.phaseThreeHealth } : null;
        int phase = Boss != null ? Boss.HealthPhase : 3;
        AscendJigoku.UI.GameplayHud.ShowBoss(this, "FIRE WORM", health != null ? health.CurrentHealth : 0, health != null ? health.MaxHealth : 1, phase, phases, tell);
    }
    public void BeginFight()
    {
        if (Started || Defeated) return;
        Started = true; leftWall.SetActive(true); rightWall.SetActive(true);
        health.DamageEnabled = true; Boss.SetEncounterActive(true);
        Boss.GetComponent<BossEncounterMusic>()?.Begin();
    }
    private void OnHealth(int current, int maximum)
    {
        if (current > 0 || Defeated) return;
        Defeated = true; clearedUntil = Time.time + 4;
        ceiling.SetActive(false); leftWall.SetActive(false); rightWall.SetActive(false); cleared?.Invoke();
        var weapons = player != null ? player.GetComponent<PlayerWeapons>() : null;
        if (weapons == null || weapons.Owns(WeaponId.Kanabo)) return;
        weapons.Unlock(WeaponId.Kanabo, false);
        AscendJigoku.UI.GameplayHud.Announce("ONI KANABO ACQUIRED - a heavy club torn from the worm. Press {swap} to wield it.");
        if (VerticalLevelGenerator.Active != null) VerticalLevelGenerator.Active.SaveAfterPurchase(); // keep the drop across death
    }
    private void OnDestroy()
    { if (health != null) health.HealthChanged -= OnHealth; AscendJigoku.UI.GameplayHud.HideBoss(this); }
}
