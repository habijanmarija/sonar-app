using System;
using Host.Participant;
using Host.Scenarios;
using Host.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Host.App
{
    /// Singleton orchestrator for the host shell. Lives in DontDestroyOnLoad so it
    /// persists across the boot → menu → scenario → menu navigation. Scenarios read
    /// `ActiveSession` at scene start and call `ReturnToShell` when done.
    [AddComponentMenu("SONAR Host/App Lifecycle")]
    public sealed class AppLifecycle : MonoBehaviour
    {
        public static AppLifecycle Instance { get; private set; }

        public ParticipantSession ActiveSession { get; private set; }
        public IScenarioModule ActiveScenario { get; private set; }
        public AppSettings Settings { get; private set; }

        public event Action<IScenarioModule, ParticipantSession> OnScenarioStarting;
        public event Action<SessionSummaryDto> OnScenarioFinished;

        const string MainMenuSceneName = "MainMenu";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Settings = AppSettings.Load();
        }

        public void LaunchScenario(IScenarioModule module, ParticipantSession session)
        {
            if (module == null || session == null)
            {
                Debug.LogError("[Host] LaunchScenario called with null module or session.");
                return;
            }
            ActiveScenario = module;
            ActiveSession = session;
            OnScenarioStarting?.Invoke(module, session);
            SceneManager.LoadScene(module.SceneName, LoadSceneMode.Single);
        }

        /// Scenarios call this when their session_end fires. Optional `summary` carries
        /// any in-Unity-computed metrics; the canonical metrics come from the backend.
        public void ReturnToShell(SessionSummaryDto summary = null)
        {
            OnScenarioFinished?.Invoke(summary);
            ActiveScenario = null;
            ActiveSession = null;
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
