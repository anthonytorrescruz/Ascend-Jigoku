// Button On Click events call StartGame and QuitGame. Set the gameplay scene below.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AscendJigoku.UI
{
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private string gameplayScene = "Main";
        private bool starting;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void StartGame() => Begin(false);

        // Replays the opening and tutorial, then starts a new run.
        public void StartTutorial() => Begin(true);

        private void Begin(bool tutorial)
        {
            if (starting) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Debug.LogError("Add " + gameplayScene + " to the build scene list.");
                return;
            }
            starting = true;
            VerticalLevelGenerator.BeginNewRun();
            if (tutorial) AscendJigoku.Gameplay.Tutorial.ArmReplay(); else AscendJigoku.Gameplay.Tutorial.ArmForNewRun(); // first run only
            AscendJigoku.Gameplay.RunSave.Delete(); // START always replaces the saved run
            SceneManager.LoadScene(gameplayScene);
        }

        public void QuitGame() => QuitApplication();

        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
