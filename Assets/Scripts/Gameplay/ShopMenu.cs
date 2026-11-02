using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AscendJigoku.Gameplay
{
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(PlayerControlLock), typeof(PlayerUpgrades))]
    public sealed class ShopMenu : MonoBehaviour
    {
        public CurrencyWallet wallet;
        private ActiveInputPrompts prompts;
        private InputAction interact, close;
        private PlayerControlLock controlLock;
        private PlayerUpgrades upgrades;
        private Shopkeeper current, nearest;
        private GameObject root, panel;
        private Text prompt, title, balance, greeting, message, stats;
        private Button[] buttons;
        private Button leaveButton;
        private float oldTimeScale;
        private int lastClosedFrame = -1;
        private AudioSource shopVoice;
        public bool IsOpen => current != null;
        public bool ConsumesBackInput => IsOpen || lastClosedFrame == Time.frameCount;
        public Shopkeeper Nearest => nearest;
        private void Awake()
        {
            controlLock = GetComponent<PlayerControlLock>(); upgrades = GetComponent<PlayerUpgrades>();
            interact = new InputAction("TalkShop", InputActionType.Button, "<Keyboard>/e"); interact.AddBinding("<Gamepad>/buttonNorth");
            close = new InputAction("CloseShop", InputActionType.Button, "<Keyboard>/escape"); close.AddBinding("<Gamepad>/buttonEast");
            shopVoice = gameObject.AddComponent<AudioSource>(); shopVoice.playOnAwake = false; shopVoice.spatialBlend = 0;
        }
        private void Start()
        {
            prompts = FindAnyObjectByType<ActiveInputPrompts>(); if (prompts == null) prompts = gameObject.AddComponent<ActiveInputPrompts>();
            CreateUI();
        }
        private void OnEnable() { interact.Enable(); close.Enable(); }
        private void OnDisable() { interact.Disable(); close.Disable(); Close(); }
        private void OnDestroy() { interact.Dispose(); close.Dispose(); if (root != null) Destroy(root); }
        private void Update()
        {
            if (panel == null) return;
            if (IsOpen)
            {
                balance.text = wallet != null ? $"{wallet.CurrencyName}: {wallet.Balance}" : "Currency wallet is not connected.";
                if (close.WasPressedThisFrame()) Close();
                return;
            }
            nearest = null; prompt.text = "";
            if (Time.timeScale <= 0 || controlLock.IsLocked || GetComponent<PlayerHealth>().CurrentHealth <= 0) return;
            float best = float.PositiveInfinity;
            foreach (var shop in Shopkeeper.Actors)
            {
                if (shop == null || shop.catalog == null || shop.gameObject.scene != gameObject.scene) continue;
                float distance = ((Vector2)(shop.transform.position - transform.position)).sqrMagnitude;
                if (distance < shop.interactionRadius * shop.interactionRadius && distance < best) { best = distance; nearest = shop; }
            }
            if (nearest == null) return;
            prompt.text = $"[{prompts.Interact}] Talk to {nearest.displayName}";
            if (interact.WasPressedThisFrame()) Open(nearest);
        }
        public bool Open(Shopkeeper shop)
        {
            if (shop == null || shop.catalog == null || IsOpen || panel == null || Time.timeScale <= 0 || controlLock.IsLocked
                || GetComponent<PlayerHealth>().CurrentHealth <= 0
                || ((Vector2)(shop.transform.position - transform.position)).sqrMagnitude > shop.interactionRadius * shop.interactionRadius) return false;
            current = shop; oldTimeScale = Time.timeScale; controlLock.Lock(); Time.timeScale = 0;
            title.text = shop.displayName; greeting.text = shop.catalog.greeting; message.text = ""; panel.SetActive(true); prompt.text = "";
            if (shop.catalog.openSound != null)
            {
                shopVoice.Stop(); shopVoice.clip = shop.catalog.openSound;
                shopVoice.volume = shop.catalog.openSoundVolume; shopVoice.pitch = shop.catalog.openSoundPitch; shopVoice.Play();
            }
            Refresh(); EventSystem.current.SetSelectedGameObject(buttons[0].gameObject); return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            lastClosedFrame = Time.frameCount;
            if (shopVoice != null) shopVoice.Stop();
            current = null; if (panel != null) panel.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Time.timeScale = oldTimeScale > 0 ? oldTimeScale : 1; controlLock.Unlock();
        }
        public bool Buy(int index)
        {
            if (!IsOpen || index < 0 || index >= current.catalog.offers.Length) return false;
            bool bought = upgrades.TryBuy(current.catalog.offers[index], wallet, out string result);
            message.text = result;
            if (bought) CombatFeedback.PlayClip(current.catalog.purchaseSound, 0.4f);
            Refresh(); return bought;
        }
        private void Refresh()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool available = current != null && i < current.catalog.offers.Length && current.catalog.offers[i] != null;
                buttons[i].gameObject.SetActive(available); if (!available) continue;
                var offer = current.catalog.offers[i]; bool maxed = upgrades.IsMaxed(offer);
                buttons[i].GetComponentInChildren<Text>().text = $"{offer.title}   {(maxed ? "MAX" : offer.Price(upgrades.Count(offer.kind)).ToString() + " coins")}\n{offer.description}";
                // Keep disabled purchases selectable so controller users can read the reason.
            }
            stats.text = $"Health: {GetComponent<PlayerHealth>().CurrentHealth}/{GetComponent<PlayerHealth>().MaxHealth}    Damage: {GetComponent<PlayerCombat>().AttackDamage}    Jumps: {GetComponent<PlayerController>().MaxJumps}\nPurchases and test coins reset when the scene reloads.";
        }
        private void CreateUI()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            root = new GameObject("Shop Runtime UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            prompt = Label(root.transform, "Nearby shop prompt", new Vector2(0, -300), new Vector2(1300, 60), 28);
            panel = new GameObject("Shop", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.055f, 0.98f);
            title = Label(panel.transform, "Merchant", new Vector2(0, 355), new Vector2(1300, 70), 45); title.fontStyle = FontStyle.Bold;
            greeting = Label(panel.transform, "Greeting", new Vector2(0, 265), new Vector2(1300, 90), 25);
            balance = Label(panel.transform, "Wallet", new Vector2(0, 190), new Vector2(1300, 60), 28);
            buttons = new Button[3];
            for (int i = 0; i < buttons.Length; i++) { int index = i; buttons[i] = Button(panel.transform, "Upgrade " + i, new Vector2(0, 90 - i * 115), new Vector2(1200, 98), () => Buy(index)); }
            leaveButton = Button(panel.transform, "Leave shop", new Vector2(0, -280), new Vector2(500, 60), Close); leaveButton.GetComponentInChildren<Text>().text = "LEAVE SHOP";
            for (int i = 0; i < buttons.Length; i++) { var nav = buttons[i].navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnUp = i == 0 ? leaveButton : buttons[i - 1]; nav.selectOnDown = i == 2 ? leaveButton : buttons[i + 1]; buttons[i].navigation = nav; }
            var backNav = leaveButton.navigation; backNav.mode = Navigation.Mode.Explicit; backNav.selectOnUp = buttons[2]; backNav.selectOnDown = buttons[0]; leaveButton.navigation = backNav;
            message = Label(panel.transform, "Purchase result", new Vector2(0, -210), new Vector2(1300, 60), 26);
            stats = Label(panel.transform, "Stats and reset rules", new Vector2(0, -365), new Vector2(1350, 95), 23);
            panel.SetActive(false);
        }
        private Button Button(Transform parent, string name, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ShopCancelHandler)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.27f); var button = go.GetComponent<Button>();
            var colors = button.colors; colors.selectedColor = new Color(0.5f, 0.7f, 1); colors.highlightedColor = colors.selectedColor; button.colors = colors;
            go.GetComponent<ShopCancelHandler>().shop = this; button.onClick.AddListener(action);
            Label(go.transform, "Text", Vector2.zero, size - new Vector2(15, 5), 24); return button;
        }
        private static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
    }
}
