using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AscendJigoku.UI;

namespace AscendJigoku.Gameplay
{
    // Everything the tutorial draws: the black cover with the opening lines, the lesson prompt, the skip hint
    // and the skeleton's dialogue box. Ember style, built from MenuStyle.
    public sealed class TutorialUI : MonoBehaviour
    {
        private const string Hex = "#F08A3C";
        private CanvasGroup cover, talk, hud;
        private Text line, title, body, skip, speaker, speech, next;
        private Image portrait;
        private InputAction confirm;
        private ActiveInputPrompts prompts;
        private float flashUntil;
        public float CoverAlpha => cover.alpha;
        public bool ConfirmPressed => confirm.WasPressedThisFrame();

        public static string Key(string label) => $"<color={Hex}>[{label}]</color>";

        public static TutorialUI Create(Transform parent, ActiveInputPrompts prompts)
        {
            var ui = new GameObject("Tutorial UI").AddComponent<TutorialUI>();
            ui.transform.SetParent(parent, false);
            ui.prompts = prompts;
            ui.Build();
            return ui;
        }

        private void Awake()
        {
            confirm = new InputAction("TutorialConfirm", InputActionType.Button, "<Keyboard>/enter");
            confirm.AddBinding("<Keyboard>/numpadEnter");
            confirm.AddBinding("<Keyboard>/space");
            confirm.AddBinding("<Gamepad>/buttonSouth");
            confirm.Enable();
        }

        private void OnDestroy() { confirm.Dispose(); }

        private void Build()
        {
            var canvas = MenuStyle.CreateCanvas("Tutorial Canvas", 40, transform);

            hud = Group(canvas, "Lesson").gameObject.AddComponent<CanvasGroup>();
            var strip = MenuStyle.FullScreen(hud.transform, "Strip", new Color(0.04f, 0.02f, 0.02f, 0.62f), false);
            strip.raycastTarget = false;
            var stripRect = strip.rectTransform;
            stripRect.anchorMin = new Vector2(0, 0);
            stripRect.anchorMax = new Vector2(1, 0);
            stripRect.pivot = new Vector2(0.5f, 0);
            stripRect.sizeDelta = new Vector2(0, 104);
            stripRect.anchoredPosition = Vector2.zero;
            title = MenuStyle.Label(hud.transform, "Title", "", new Vector2(0, -292), new Vector2(1100, 30), 22, TextAnchor.MiddleCenter, MenuStyle.Ember);
            title.font = MenuStyle.TitleFont;
            body = MenuStyle.Label(hud.transform, "Text", "", new Vector2(0, -330), new Vector2(1180, 40), 28);
            body.supportRichText = true;
            hud.alpha = 0;

            cover = Group(canvas, "Cover").gameObject.AddComponent<CanvasGroup>();
            MenuStyle.FullScreen(cover.transform, "Black", MenuStyle.Background);
            line = MenuStyle.Label(cover.transform, "Line", "", new Vector2(0, 10), new Vector2(900, 160), 36);
            line.supportRichText = true;
            line.lineSpacing = 1.2f;
            cover.alpha = 1;

            talk = Group(canvas, "Talk").gameObject.AddComponent<CanvasGroup>();
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(talk.transform, false);
            MenuStyle.Place((RectTransform)portraitGo.transform, new Vector2(0, 90), new Vector2(560, 420));
            portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var panel = MenuStyle.FullScreen(talk.transform, "Panel", new Color(0.04f, 0.02f, 0.02f, 0.9f), false);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0);
            panelRect.anchorMax = new Vector2(0.5f, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(1080, 190);
            panelRect.anchoredPosition = new Vector2(0, 40);
            panel.raycastTarget = false;
            speaker = MenuStyle.Label(talk.transform, "Speaker", "MEMENTO MORI", new Vector2(0, -190), new Vector2(980, 30), 24, TextAnchor.MiddleLeft, MenuStyle.Ember);
            speaker.font = MenuStyle.TitleFont;
            speech = MenuStyle.Label(talk.transform, "Speech", "", new Vector2(0, -250), new Vector2(1000, 100), 30, TextAnchor.UpperLeft, MenuStyle.TitleColor);
            next = MenuStyle.Label(talk.transform, "Next", "", new Vector2(0, -318), new Vector2(1000, 24), 18, TextAnchor.MiddleRight, MenuStyle.MutedText);
            talk.alpha = 0;

            skip = MenuStyle.Label(canvas, "Skip", "", Vector2.zero, new Vector2(520, 28), 18, TextAnchor.MiddleRight, MenuStyle.MutedText);
            var skipRect = skip.rectTransform;
            skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = Vector2.one;
            skipRect.anchoredPosition = new Vector2(-28, -24);
        }

        private static RectTransform Group(Transform parent, string name) => MenuStyle.Group(parent, name);

        // ---------- Cover ----------

        public IEnumerator FadeCover(float to, float seconds)
        {
            float from = cover.alpha;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                cover.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            cover.alpha = to;
        }

        public void SetCover(float alpha) => cover.alpha = alpha;

        public void ShowCoverText(bool on) { line.gameObject.SetActive(on); }

        // Lines fade in and out over black. Confirm moves on; otherwise each line times out.
        public IEnumerator Opening(string[] lines)
        {
            cover.alpha = 1;
            line.gameObject.SetActive(true);
            foreach (string text in lines)
            {
                line.text = text;
                SetLineAlpha(0);
                for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime)
                {
                    SetLineAlpha(t / 0.9f);
                    yield return null;
                }

                SetLineAlpha(1);
                // Hold up to 3.6s; Confirm skips ahead, but only after 0.7s so a held key doesn't skip every line.
                for (float t = 0; t < 3.6f && !(t > 0.7f && ConfirmPressed); t += Time.unscaledDeltaTime) yield return null;
                for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    SetLineAlpha(1 - t / 0.5f);
                    yield return null;
                }
            }
            line.gameObject.SetActive(false);
        }

        private void SetLineAlpha(float a)
        {
            var c = line.color;
            c.a = a;
            line.color = c;
        }

        // ---------- Lesson prompt ----------

        public void ShowPrompt(string heading, string text)
        {
            hud.alpha = 1;
            title.text = heading;
            body.text = text;
            body.color = MenuStyle.TextColor;
            flashUntil = 0;
        }

        public void HidePrompt() { hud.alpha = 0; talk.alpha = 0; } // talk too: skipping mid-dialogue must not leave the box up

        // Briefly shows a completion message in ember color; Flashing stays true for 0.9s so the caller can wait.
        public void FlashDone(string text)
        {
            body.text = text;
            body.color = MenuStyle.Ember;
            flashUntil = Time.unscaledTime + 0.9f;
        }

        public bool Flashing => Time.unscaledTime < flashUntil;

        public void SetSkip(string label, float progress)
        {
            skip.text = progress > 0 ? $"Skipping... {Mathf.RoundToInt(progress * 100)}%" : $"Hold {label} to skip the tutorial";
            skip.color = progress > 0 ? MenuStyle.Ember : MenuStyle.MutedText;
        }

        // ---------- Skeleton dialogue ----------

        // Typewriter dialogue: Confirm finishes the current line, then advances. Fades the box in and out around it.
        public IEnumerator Talk(Sprite[] frames, string name, string[] lines, AudioClip voice)
        {
            speaker.text = name;
            next.text = $"{prompts.Confirm}: continue";
            portrait.enabled = frames != null && frames.Length > 0;
            talk.alpha = 0;
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                talk.alpha = t / 0.5f;
                Pose(frames, 0, 0);
                yield return null;
            }
            talk.alpha = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                float volume = AscendJigoku.Settings.GameSettings.SfxVolume;
                if (voice != null)
                    AudioSource.PlayClipAtPoint(voice, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.8f * volume);
                float shown = 0, clock = 0;
                string text = lines[i];
                while (true)
                {
                    clock += Time.unscaledDeltaTime;
                    bool typing = shown < text.Length;
                    if (typing) shown = Mathf.Min(text.Length, shown + 48 * Time.unscaledDeltaTime);
                    speech.text = text.Substring(0, Mathf.FloorToInt(shown));
                    Pose(frames, clock, typing && i == 0 ? 1 : 0.15f);
                    if (ConfirmPressed)
                    {
                        if (typing) shown = text.Length; else break;
                    }
                    yield return null;
                }
            }
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                talk.alpha = 1 - t / 0.4f;
                yield return null;
            }
            talk.alpha = 0;
        }

        // Cycles the shopkeeper's frames; a laugh rattles the portrait.
        private void Pose(Sprite[] frames, float clock, float shake)
        {
            if (frames == null || frames.Length == 0) return;
            portrait.sprite = frames[Mathf.FloorToInt(clock * 12.5f) % frames.Length];
            portrait.rectTransform.anchoredPosition = new Vector2(0, 90 + Mathf.Sin(clock * 34) * 7 * shake);
        }
    }
}
