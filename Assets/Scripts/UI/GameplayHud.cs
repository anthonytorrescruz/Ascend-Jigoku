using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // In-game HUD in the Ember style:
    //   lower-left   health pips (the next pip fills while you hold to heal)
    //   lower-right  ability slots for dash, parry, cast and the equipped weapon's ability, with key labels and cooldowns;
    //                the equipped weapon is named under the row
    //   top-center   boss bar, shown by encounters through ShowBoss / HideBoss
    //   top-right    coin count from the player's CurrencyWallet (hidden if there is none)
    // Installed automatically in any scene with a PlayerHealth, except AnthonyScene (it has its own test UI).
    // Replaces the old segment health bar (GameHud / HealthBarUI) where a scene still has one.
    public sealed class GameplayHud : MonoBehaviour
    {
        private const float Margin = 44, PipSize = 40, PipStep = 62, SlotSize = 64, SlotStep = 78;

        // ---------- Boss bar API ----------

        private static Object bossOwner;
        private static string bossName, bossTell;
        private static int bossHealth, bossMax = 1, bossPhase;
        private static float[] bossPhaseMarks;

        // Call every frame (or on every change) while the fight is on; HideBoss removes the bar.
        // phaseMarks: health fractions where a new phase starts (e.g. 0.67, 0.34), or null for a single-phase boss.
        public static void ShowBoss(Object owner, string name, int health, int maxHealth, int phase, float[] phaseMarks, string tell)
        {
            bossOwner = owner;
            bossName = name;
            bossTell = tell;
            bossHealth = Mathf.Max(0, health);
            bossMax = Mathf.Max(1, maxHealth);
            bossPhase = phase;
            bossPhaseMarks = phaseMarks;
        }

        private static string notice;
        private static float noticeAt;

        // One-line pickup message; "{swap}" becomes the swap-weapon key.
        public static void Announce(string message)
        {
            notice = message;
            noticeAt = Time.unscaledTime;
        }

        public static void HideBoss(Object owner)
        {
            if (bossOwner == owner) bossOwner = null;
        }

        // ---------- Install ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachTo(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachTo(scene);

        // Adds the HUD to scenes that have a player, and switches off any old scene-authored health bar.
        private static void AttachTo(Scene scene)
        {
            bossOwner = null;
            if (!scene.IsValid() || FindAnyObjectByType<GameplayHud>() != null || FindAnyObjectByType<AnthonySession>() != null)
                return;
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null || health.gameObject.scene != scene) return;
            // Old HUD objects are switched off before GameHud's Start can spawn its bar.
            foreach (var old in FindObjectsByType<GameHud>()) old.gameObject.SetActive(false);
            foreach (var old in FindObjectsByType<HealthBarUI>()) old.gameObject.SetActive(false);
            var go = new GameObject("Gameplay HUD");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<GameplayHud>().player = health;
        }

        // ---------- State ----------

        // One health diamond.
        private sealed class Pip
        {
            public RectTransform Root;
            public Image Frame, Core;
            public float FlashUntil;
        }

        // One ability square. Action is the input action name used to look up the key label; Cover is the cooldown
        // overlay and Charge the fill used by the cast/heal slot. Peak is the longest recovery seen (for the parry fraction).
        private sealed class Slot
        {
            public string Action;
            public RectTransform Root;
            public Image Frame, Cover, Charge;
            public Text Name, Key;
            public bool WasReady = true;
            public float FlashUntil, CooldownStart, Peak;
        }

        private PlayerHealth player;
        private PlayerController controller;
        private PlayerParry parry;
        private PlayerSpell spell;
        private PlayerWeapons weapons;
        private ActiveInputPrompts prompts;

        private RectTransform canvas, healthRow;
        private CanvasGroup group;
        private Pip[] pips = new Pip[0];
        private Slot dash, parrySlot, cast, ability;
        private Text weaponText, noticeText;
        private CanvasGroup noticeGroup;
        private WeaponId shownWeapon = (WeaponId)(-1);
        private RectTransform bossRoot, bossFill, bossChip;
        private CanvasGroup bossGroup;
        private Text bossNameText, bossTellText;
        private RectTransform[] bossTicks = new RectTransform[0];
        private float chip = 1, shakeUntil;
        private CurrencyWallet wallet;
        private RectTransform coinRow;
        private Text coinText;
        private int shownCoins = -1;
        private float coinPopUntil;

        // Looks up the player's components (any may be missing, e.g. in the tutorial) and builds the canvas once.
        private void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerHealth>();
            if (player == null)
            {
                Destroy(gameObject);
                return;
            }
            controller = player.GetComponent<PlayerController>();
            parry = player.GetComponent<PlayerParry>();
            spell = player.GetComponent<PlayerSpell>();
            weapons = player.GetComponent<PlayerWeapons>();
            prompts = FindAnyObjectByType<ActiveInputPrompts>();
            if (prompts == null) prompts = gameObject.AddComponent<ActiveInputPrompts>();

            canvas = MenuStyle.CreateCanvas("HUD Canvas", 5, transform);
            Destroy(canvas.GetComponent<GraphicRaycaster>()); // display only, never eats clicks
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            BuildHealth();
            BuildAbilities();
            BuildBossBar();
            BuildCoins();
            player.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (player != null) player.Damaged -= OnDamaged;
        }

        // Shakes the health row and flashes the pips that were just lost.
        private void OnDamaged(int amount)
        {
            shakeUntil = Time.unscaledTime + 0.25f;
            for (int i = Mathf.Max(0, player.CurrentHealth); i < Mathf.Min(pips.Length, player.CurrentHealth + amount); i++)
                pips[i].FlashUntil = Time.unscaledTime + 0.18f;
        }

        private void Update()
        {
            if (player == null || canvas == null) return;
            // Hidden on death and while a menu has frozen time (pause, shop). Hit-stop never reaches 0.
            bool menuOpen = Time.timeScale <= 0;
            float targetAlpha = player.CurrentHealth > 0 && !menuOpen ? 1 : 0;
            group.alpha = menuOpen ? 0 : Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4);
            UpdateHealth();
            UpdateAbilities();
            UpdateBossBar();
            UpdateCoins();
        }

        // ---------- Health ----------

        private void BuildHealth()
        {
            // Height matches the ability slots so the two rows line up.
            healthRow = Corner("Health", new Vector2(0, 0), new Vector2(Margin, Margin), new Vector2(560, 64));
        }

        // Pips are rebuilt whenever max health changes (upgrades, new run).
        private void RebuildPips(int max)
        {
            foreach (var pip in pips) Destroy(pip.Root.gameObject);
            pips = new Pip[max];
            for (int i = 0; i < max; i++)
            {
                var root = Box(healthRow, "Pip " + (i + 1), new Vector2(PipSize, PipSize), MenuStyle.DimText);
                root.anchorMin = root.anchorMax = new Vector2(0, 0.5f);
                root.anchoredPosition = new Vector2(PipSize * 0.75f + i * PipStep, 0);
                root.localRotation = Quaternion.Euler(0, 0, 45);
                var core = Box(root, "Core", new Vector2(PipSize - 10, PipSize - 10), MenuStyle.Ember);
                pips[i] = new Pip { Root = root, Frame = root.GetComponent<Image>(), Core = core.GetComponent<Image>() };
            }
        }

        private void UpdateHealth()
        {
            int max = Mathf.Max(1, player.MaxHealth), hp = Mathf.Clamp(player.CurrentHealth, 0, max);
            if (pips.Length != max) RebuildPips(max);
            float now = Time.unscaledTime;
            bool low = hp == 1 && max > 1;
            float charge = spell != null && spell.IsFocusing ? spell.ChargeProgress : 0;
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                bool full = i < hp;
                float scale = 1;
                Color core;
                if (now < pip.FlashUntil && !GameSettings.ReduceFlashing) core = MenuStyle.TextColor;
                else if (full) core = MenuStyle.Ember;
                else if (i == hp && charge > 0)
                {
                    // The next pip grows while the player holds to heal.
                    core = MenuStyle.TextColor;
                    scale = charge;
                }
                else core = MenuStyle.Background;
                if (full && low && !GameSettings.ReduceFlashing) scale = 0.85f + 0.15f * Mathf.Sin(now * 6);
                pip.Core.color = core;
                pip.Core.rectTransform.localScale = new Vector3(scale, scale, 1);
                pip.Frame.color = full ? MenuStyle.Ember * 0.75f : MenuStyle.DimText;
            }
            // Row jitters for 0.25s after a hit, scaled by the screen shake setting.
            float shake = now < shakeUntil ? 6 * GameSettings.ScreenShake * ((shakeUntil - now) / 0.25f) : 0;
            healthRow.anchoredPosition = new Vector2(Margin + Random.Range(-shake, shake), Margin + Random.Range(-shake, shake));
        }

        // ---------- Abilities ----------

        private void BuildAbilities()
        {
            var row = Corner("Abilities", new Vector2(1, 0), new Vector2(-Margin, Margin), new Vector2(SlotStep * 4, SlotSize + 30));
            dash = BuildSlot(row, "DASH", "Dash", 0);
            parrySlot = BuildSlot(row, "PARRY", "Parry", 1);
            cast = BuildSlot(row, "CAST", "CastOrHeal", 2);
            if (controller == null) dash.Root.gameObject.SetActive(false);
            if (parry == null) parrySlot.Root.gameObject.SetActive(false);
            if (spell == null) cast.Root.gameObject.SetActive(false);
            ability = BuildSlot(row, "ABILITY", "Ability", 3);
            if (weapons == null) ability.Root.gameObject.SetActive(false);
            weaponText = MenuStyle.Label(
                row, "Weapon", "", new Vector2(-SlotStep * 2 + SlotSize * 0.5f - 1, -22), new Vector2(SlotStep * 4, 22), 14,
                TextAnchor.MiddleRight, MenuStyle.MutedText);
            weaponText.rectTransform.anchorMin = weaponText.rectTransform.anchorMax = weaponText.rectTransform.pivot = new Vector2(1, 0);
            weaponText.rectTransform.anchoredPosition = new Vector2(0, -22);
            noticeText = MenuStyle.Label(
                canvas, "Notice", "", new Vector2(0, 150), new Vector2(1200, 30), 20, TextAnchor.MiddleCenter, MenuStyle.Ember);
            noticeText.rectTransform.anchorMin = noticeText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            noticeGroup = noticeText.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.alpha = 0;
        }

        // Square with a bright border, a dark inside, a rising Charge fill, a Cooldown cover that shrinks as it recharges,
        // the ability name inside and the key label above. Slots are laid out left to right by index.
        private Slot BuildSlot(RectTransform row, string name, string action, int index)
        {
            var root = Box(row, name, new Vector2(SlotSize, SlotSize), MenuStyle.Ember);
            root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.anchoredPosition = new Vector2(SlotSize * 0.5f + index * SlotStep, SlotSize * 0.5f);
            var inner = Box(root, "Inner", new Vector2(SlotSize - 4, SlotSize - 4), new Color(0.05f, 0.03f, 0.03f, 0.9f));

            // Charge and Cooldown are anchored to the bottom and stretched upward by setting anchorMax.y (0-1).
            var charge = Box(inner, "Charge", Vector2.zero, new Color(MenuStyle.Ember.r, MenuStyle.Ember.g, MenuStyle.Ember.b, 0.45f));
            charge.anchorMin = Vector2.zero;
            charge.anchorMax = new Vector2(1, 0);
            charge.pivot = new Vector2(0.5f, 0);
            var cover = Box(inner, "Cooldown", Vector2.zero, new Color(0, 0, 0, 0.7f));
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = new Vector2(1, 0);
            cover.pivot = new Vector2(0.5f, 0);

            var label = MenuStyle.Label(
                inner, "Name", name, new Vector2(0, -2), new Vector2(SlotSize, 24), 15, TextAnchor.MiddleCenter, MenuStyle.TextColor);
            var key = MenuStyle.Label(
                root, "Key", "", new Vector2(0, SlotSize * 0.5f + 14), new Vector2(SlotStep, 22), 14, TextAnchor.MiddleCenter,
                MenuStyle.MutedText);
            return new Slot
            {
                Action = action,
                Root = root,
                Frame = root.GetComponent<Image>(),
                Cover = cover.GetComponent<Image>(),
                Charge = charge.GetComponent<Image>(),
                Name = label,
                Key = key
            };
        }

        // Per-frame cooldown state for each slot, from the player's components. `remaining` passed to DrawSlot is 0-1.
        private void UpdateAbilities()
        {
            float now = Time.unscaledTime;

            if (controller != null)
            {
                bool cooling = controller.DashOnCooldown;
                if (cooling && dash.WasReady) dash.CooldownStart = Time.time;
                float remaining = cooling
                    ? 1 - Mathf.Clamp01((Time.time - dash.CooldownStart) / Mathf.Max(0.01f, controller.DashCycleSeconds))
                    : 0;
                bool spent = !cooling && controller.DashSpentInAir; // must land before dashing again
                DrawSlot(dash, spent ? 1 : remaining, !cooling && !spent, now);
            }

            if (parry != null)
            {
                // While the parry is busy but not yet recovering the slot stays fully covered (remaining = 1).
                float left = parry.RecoveryRemaining;
                if (parry.IsBusy && parrySlot.WasReady) parrySlot.Peak = 0;
                parrySlot.Peak = Mathf.Max(parrySlot.Peak, left);
                float remaining = !parry.IsBusy ? 0 : left > 0 ? left / Mathf.Max(0.01f, parrySlot.Peak) : 1;
                DrawSlot(parrySlot, remaining, !parry.IsBusy, now);
            }

            if (weapons != null)
            {
                var weapon = weapons.Current;
                ability.Name.text = weapon.AbilityShort;
                float remaining = weapons.CooldownFraction;
                DrawSlot(ability, remaining, remaining <= 0, now);
                if (weapon.Id != shownWeapon || weaponText.text.Length == 0 || weapons.HasChoice != weaponText.text.Contains("["))
                {
                    shownWeapon = weapon.Id;
                    weaponText.text = weapons.HasChoice ? weapon.Name + "   [" + SwapKey() + "] swap" : weapon.Name;
                }
            }
            UpdateNotice();

            if (spell != null)
            {
                float charge = spell.IsFocusing ? spell.ChargeProgress : 0;
                cast.Name.text = spell.IsFocusing ? "HEAL" : "CAST";
                cast.Charge.rectTransform.anchorMax = new Vector2(1, charge);
                cast.Charge.rectTransform.sizeDelta = Vector2.zero;
                DrawSlot(cast, 0, true, now);
            }
        }

        private string SwapKey() => prompts != null ? prompts.Button("SwapWeapon").ToUpperInvariant() : "";

        // Announce() text shows for 6 seconds, fading in quickly and out over the last half second.
        private void UpdateNotice()
        {
            float age = Time.unscaledTime - noticeAt;
            if (notice == null || age > 6)
            {
                noticeGroup.alpha = 0;
                return;
            }
            noticeText.text = notice.Replace("{swap}", SwapKey());
            noticeGroup.alpha = Mathf.Clamp01(Mathf.Min(age * 3, (6 - age) * 2));
        }

        // Applies cooldown cover, ready flash, colors and the (shortened) key label to one slot.
        private void DrawSlot(Slot slot, float remaining, bool ready, float now)
        {
            if (ready && !slot.WasReady) slot.FlashUntil = now + 0.2f;
            slot.WasReady = ready;
            slot.Cover.rectTransform.anchorMax = new Vector2(1, Mathf.Clamp01(remaining));
            slot.Cover.rectTransform.sizeDelta = Vector2.zero;
            bool flash = now < slot.FlashUntil && !GameSettings.ReduceFlashing;
            slot.Frame.color = flash ? MenuStyle.TextColor : ready ? MenuStyle.Ember : MenuStyle.DimText;
            slot.Name.color = ready ? MenuStyle.TextColor : MenuStyle.DimText;
            float pop = flash ? 1 + 0.1f * ((slot.FlashUntil - now) / 0.2f) : 1;
            slot.Root.localScale = new Vector3(pop, pop, 1);
            string key = prompts != null
                ? prompts.Button(slot.Action).Replace("Left ", "").Replace("Right ", "").ToUpperInvariant()
                : "";
            slot.Key.text = key.Length > 8 ? key.Substring(0, 8) : key;
        }

        // ---------- Boss bar ----------

        // Name above, thin health track with a pale "recent damage" chip behind the fill, tell text below.
        private void BuildBossBar()
        {
            bossRoot = Corner("Boss", new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(620, 110));
            bossRoot.pivot = new Vector2(0.5f, 1);
            bossGroup = bossRoot.gameObject.AddComponent<CanvasGroup>();
            bossGroup.alpha = 0;
            bossNameText = MenuStyle.Label(bossRoot, "Name", "", new Vector2(0, 34), new Vector2(620, 34), 24, TextAnchor.MiddleCenter);
            MenuStyle.StyleTitle(bossNameText, 24);

            var track = Box(bossRoot, "Track", new Vector2(560, 10), MenuStyle.Ash);
            track.anchoredPosition = new Vector2(0, 8);
            bossChip = Box(track, "Recent damage", Vector2.zero, new Color(1, 0.85f, 0.7f, 0.8f));
            bossFill = Box(track, "Health", Vector2.zero, MenuStyle.EmberDeep);
            // Both bars grow from the left edge; UpdateBossBar sets anchorMax.x to the health fraction.
            foreach (var bar in new[] { bossChip, bossFill })
            {
                bar.anchorMin = Vector2.zero;
                bar.pivot = new Vector2(0, 0.5f);
                bar.sizeDelta = Vector2.zero;
            }
            var bracketLeft = Box(track, "Cap L", new Vector2(4, 18), MenuStyle.Ember);
            bracketLeft.anchorMin = bracketLeft.anchorMax = new Vector2(0, 0.5f);
            bracketLeft.anchoredPosition = new Vector2(-6, 0);
            var bracketRight = Box(track, "Cap R", new Vector2(4, 18), MenuStyle.Ember);
            bracketRight.anchorMin = bracketRight.anchorMax = new Vector2(1, 0.5f);
            bracketRight.anchoredPosition = new Vector2(6, 0);

            bossTellText = MenuStyle.Label(
                bossRoot, "Tell", "", new Vector2(0, -22), new Vector2(900, 28), 18, TextAnchor.MiddleCenter, MenuStyle.MutedText);
        }

        private void UpdateBossBar()
        {
            bool visible = bossOwner != null; // a destroyed owner compares equal to null
            bossGroup.alpha = Mathf.MoveTowards(bossGroup.alpha, visible ? 1 : 0, Time.unscaledDeltaTime * 3);
            if (!visible)
            {
                chip = 1;
                return;
            }

            float fraction = Mathf.Clamp01((float)bossHealth / bossMax);
            if (chip < fraction) chip = fraction;
            chip = Mathf.MoveTowards(chip, fraction, Time.unscaledDeltaTime * 0.35f); // the chunk you just took drains away
            bossFill.anchorMax = new Vector2(fraction, 1);
            bossChip.anchorMax = new Vector2(chip, 1);
            int marks = bossPhaseMarks != null ? bossPhaseMarks.Length : 0;
            bossNameText.text = marks > 0 ? $"{bossName}   {Roman(bossPhase)}" : bossName;
            bossTellText.text = bossTell ?? "";

            if (bossTicks.Length != marks)
            {
                foreach (var tick in bossTicks) Destroy(tick.gameObject);
                bossTicks = new RectTransform[marks];
                for (int i = 0; i < marks; i++)
                {
                    var tick = Box(bossFill.parent, "Phase tick", new Vector2(2, 10), MenuStyle.Background);
                    bossTicks[i] = tick;
                }
            }
            for (int i = 0; i < marks; i++)
                bossTicks[i].anchorMin = bossTicks[i].anchorMax = new Vector2(Mathf.Clamp01(bossPhaseMarks[i]), 0.5f);
        }

        // ---------- Coins ----------

        private void BuildCoins()
        {
            coinRow = Corner("Coins", new Vector2(1, 1), new Vector2(-Margin, -28), new Vector2(240, 36));
            var coin = Box(coinRow, "Coin", new Vector2(20, 20), MenuStyle.Ember);
            coin.anchorMin = coin.anchorMax = new Vector2(1, 0.5f);
            coin.anchoredPosition = new Vector2(-14, 0);
            coin.localRotation = Quaternion.Euler(0, 0, 45);
            Box(coin, "Shine", new Vector2(8, 8), MenuStyle.TextColor);
            coinText = MenuStyle.Label(coinRow, "Count", "", Vector2.zero, new Vector2(200, 36), 26, TextAnchor.MiddleRight);
            coinText.rectTransform.anchorMin = coinText.rectTransform.anchorMax = coinText.rectTransform.pivot = new Vector2(1, 0.5f);
            coinText.rectTransform.anchoredPosition = new Vector2(-38, 0);
            MenuStyle.StyleTitle(coinText, 26);
            coinRow.gameObject.SetActive(false);
        }

        private void UpdateCoins()
        {
            // The pillar generator adds the wallet at runtime, so keep looking until one exists.
            if (wallet == null) wallet = player.GetComponent<CurrencyWallet>();
            if (coinRow.gameObject.activeSelf != (wallet != null)) coinRow.gameObject.SetActive(wallet != null);
            if (wallet == null) return;
            float now = Time.unscaledTime;
            if (wallet.Balance != shownCoins)
            {
                if (shownCoins >= 0) coinPopUntil = now + 0.2f;
                shownCoins = wallet.Balance;
                coinText.text = shownCoins.ToString();
            }
            float pop = now < coinPopUntil ? 1 + 0.15f * ((coinPopUntil - now) / 0.2f) : 1;
            coinRow.localScale = new Vector3(pop, pop, 1);
        }

        private static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

        // ---------- Helpers ----------

        // Empty container pinned to a screen corner/edge (anchor) with an offset from it.
        private RectTransform Corner(string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        // Plain colored rectangle that never blocks clicks.
        private static RectTransform Box(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
