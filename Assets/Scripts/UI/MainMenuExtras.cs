using System.Collections.Generic;
using AscendJigoku.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Runtime additions to MainMenu.unity, built by copying the scene's own buttons so they keep the authored look:
    //  - SETTINGS between START and QUIT, with TUTORIAL (replays the opening and tutorial) above it.
    //  - CONTINUE above START when a saved run exists (AJ-75). START then reads NEW RUN and asks for a second click.
    // Nothing is saved into the scene. A hand-authored button named "Settings Button" / "Continue Button" disables that part.
    public static class MainMenuExtras
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AddTo(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddTo(scene);

        public static void OpenSettings() => SettingsMenu.Open(null);

        private static bool continuing;

        public static void ContinueSavedRun()
        {
            if (continuing || !RunSave.TryLoad(out var save)) return;
            if (!Application.CanStreamedLevelBeLoaded(save.scene)) { Debug.LogError("Saved run scene is not in the build list: " + save.scene); return; }
            continuing = true;
            Time.timeScale = 1;
            VerticalLevelGenerator.ContinueRun(save);
            SceneManager.LoadScene(save.scene);
        }

        private static void AddTo(Scene scene)
        {
            continuing = false;
            var menu = Object.FindAnyObjectByType<MainMenu>();
            if (menu == null || menu.gameObject.scene != scene) return;
            Button start = null, quit = null;
            bool hasSettings = false, hasContinue = false, hasTutorial = false;
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (button.gameObject.scene != scene) continue;
                if (button.name == "Settings Button") hasSettings = true;
                if (button.name == "Continue Button") hasContinue = true;
                if (button.name == "Tutorial Button") hasTutorial = true;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    string method = button.onClick.GetPersistentMethodName(i);
                    if (method == "StartGame") start = button;
                    else if (method == "QuitGame") quit = button;
                }
            }
            if (start == null || quit == null) return;

            Button settings = null, resume = null, tutorial = null;

            if (!hasSettings)
                settings = Copy(quit, "Settings Button", "SETTINGS", OpenSettings);

            if (!hasTutorial)
                tutorial = Copy(quit, "Tutorial Button", "TUTORIAL", menu.StartTutorial);

            bool canContinue = !hasContinue && RunSave.TryLoad(out var save) && Application.CanStreamedLevelBeLoaded(save.scene);
            if (canContinue)
            {
                resume = Copy(start, "Continue Button", "CONTINUE", ContinueSavedRun);
                // START would wipe the save, so make it explicit and ask twice.
                SetLabel(start, "NEW RUN");
                bool armed = false;
                start.onClick = new Button.ButtonClickedEvent();
                start.onClick.AddListener(() =>
                {
                    if (!armed) { armed = true; SetLabel(start, "CONFIRM NEW RUN"); return; }
                    menu.StartGame();
                });
                // TUTORIAL also starts a new run (it replaces the save), so it asks twice as well.
                if (tutorial != null)
                {
                    bool tutorialArmed = false;
                    tutorial.onClick = new Button.ButtonClickedEvent();
                    tutorial.onClick.AddListener(() =>
                    {
                        if (!tutorialArmed) { tutorialArmed = true; SetLabel(tutorial, "CONFIRM TUTORIAL"); return; }
                        menu.StartTutorial();
                    });
                }
            }

            // Stack: CONTINUE, START, TUTORIAL, SETTINGS, QUIT, then apply the Ember layout.
            var order = new List<Button>();
            if (resume != null) order.Add(resume);
            order.Add(start);
            if (tutorial != null) order.Add(tutorial);
            if (settings != null) order.Add(settings);
            order.Add(quit);
            MenuSceneSkin.SkinMainMenu(menu, order);
        }

        private static Button Copy(Button source, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var copy = Object.Instantiate(source, source.transform.parent);
            copy.name = name;
            copy.onClick = new Button.ButtonClickedEvent(); // drop the copied persistent call
            copy.onClick.AddListener(onClick);
            SetLabel(copy, label);
            return copy;
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }
    }
}
