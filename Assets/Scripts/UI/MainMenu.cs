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

        public void StartGame()
        {
            if (starting) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Debug.LogError("Add " + gameplayScene + " to the build scene list.");
                return;
            }
            starting = true;
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
