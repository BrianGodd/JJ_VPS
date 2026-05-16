using UnityEngine;
using UnityEngine.SceneManagement;

namespace JJ.Menu
{
    public class MenuApplicationActions : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "MultiSet_Menu";
        [SerializeField] private bool clearSelectedMapOnReturn = true;

        public void ExitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ReturnToMenuScene()
        {
            if (string.IsNullOrWhiteSpace(menuSceneName))
            {
                Debug.LogError("MenuApplicationActions: menuSceneName is empty.");
                return;
            }

            if (clearSelectedMapOnReturn && SelectedMapContext.Instance != null)
            {
                SelectedMapContext.Instance.Clear();
            }

            SceneManager.LoadScene(menuSceneName);
        }
    }
}
