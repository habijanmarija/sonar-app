using UnityEngine;
using UnityEngine.SceneManagement;

namespace Host.App
{
    /// Cold-boot entry point. Placed in the Boot.unity scene that ships as the first
    /// scene in build settings. Instantiates the persistent host services and loads
    /// the main menu scene.
    [AddComponentMenu("SONAR Host/Application Bootstrap")]
    public sealed class ApplicationBootstrap : MonoBehaviour
    {
        [SerializeField] string _mainMenuSceneName = "MainMenu";

        void Awake()
        {
            // Spawn the singleton if it isn't already present (it will mark itself
            // DontDestroyOnLoad in its own Awake).
            if (AppLifecycle.Instance == null)
            {
                var go = new GameObject("AppLifecycle");
                go.AddComponent<AppLifecycle>();
            }
        }

        void Start()
        {
            SceneManager.LoadScene(_mainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
