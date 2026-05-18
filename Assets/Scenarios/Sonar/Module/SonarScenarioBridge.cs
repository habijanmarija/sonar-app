using System.Collections;
using Host.App;
using Host.Telemetry;
using Host.Tools.Notifications;
using Sonar.Guidance;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Module
{
    /// Lives on the root GameObject of `Sonar_BoneDrilling.unity`. Wires the scenario's
    /// runtime objects (GuidanceController, SessionRunner) to the host shell so a single
    /// scene plays cleanly under either of two entry paths:
    ///
    /// 1. **Host-launched** (normal flow). AppLifecycle.ActiveSession is non-null.
    ///    Bridge configures mode + calibration from the session, runs the scenario,
    ///    and on completion of the final step returns to the main menu.
    ///
    /// 2. **Standalone** (developer iteration). AppLifecycle.Instance is null or its
    ///    ActiveSession is null. Bridge runs the scenario with default mode and
    ///    no host-supplied calibration; on completion it stops Play instead of
    ///    trying to load MainMenu.
    [AddComponentMenu("SONAR Module/Sonar Scenario Bridge")]
    public sealed class SonarScenarioBridge : MonoBehaviour
    {
        [SerializeField] GuidanceController _guidance;
        [SerializeField] SessionRunner _session;

        [Header("Completion hold")]
        [Tooltip("Seconds to dwell after the final step transition before returning to the host shell. " +
                 "Gives the user a moment to see the result; also gives telemetry the chance to flush.")]
        [SerializeField, Range(0f, 5f)] float _completionDwellSeconds = 1.5f;

        bool _completionFired;

        void Awake()
        {
            if (_guidance == null) _guidance = GetComponentInChildren<GuidanceController>();
            if (_session == null) _session = GetComponentInChildren<SessionRunner>();
        }

        IEnumerator Start()
        {
            if (_guidance == null || _session == null)
            {
                Debug.LogError("[SonarBridge] GuidanceController or SessionRunner not assigned and not found in children; scenario cannot run.");
                yield break;
            }

            ApplyHostSession();

            // Wait for the WebSocket sink to reach connected, with a 5 s budget. If
            // the backend is down we still let the scenario run (offline-degraded mode);
            // SessionRunner's drop-on-overflow keeps the queue bounded.
            float waitedFor = 0f;
            while (!_session.IsConnected && waitedFor < 5f)
            {
                yield return new WaitForSeconds(0.1f);
                waitedFor += 0.1f;
            }
            if (!_session.IsConnected)
            {
                NotificationSystem.Show(
                    "Backend not reachable — scenario will run, telemetry dropped.",
                    NotificationSeverity.Warning);
            }

            EmitHostCalibration();
            _guidance.OnStepTransition += HandleStepTransition;
            _guidance.BeginSession();
        }

        void OnDestroy()
        {
            if (_guidance != null) _guidance.OnStepTransition -= HandleStepTransition;
        }

        void ApplyHostSession()
        {
            var lifecycle = AppLifecycle.Instance;
            if (lifecycle == null || lifecycle.ActiveSession == null)
            {
                Debug.LogWarning("[SonarBridge] No AppLifecycle/ActiveSession — running standalone with GuidanceController's serialized mode.");
                return;
            }

            var s = lifecycle.ActiveSession;
            if (!string.IsNullOrEmpty(s.Mode) && System.Enum.TryParse<SessionMode>(s.Mode, out var mode))
                _guidance.SetMode(mode);
        }

        void EmitHostCalibration()
        {
            var s = AppLifecycle.Instance?.ActiveSession;
            if (s?.Calibration == null) return;
            // The host's calibration UI already produced a TRE; we re-emit it on the
            // wire so the JSONL session timeline carries the same number the operator
            // saw on screen before launch.
            _session.EmitCalibration(s.Calibration.TreMm);
            _session.EmitUserAction("host_calibration_applied", new System.Collections.Generic.Dictionary<string, object>
            {
                ["tre_mm"] = s.Calibration.TreMm,
                ["meets_gate"] = s.Calibration.MeetsGate,
                ["landmark_count"] = s.Calibration.LandmarkCount,
            });
        }

        void HandleStepTransition(int newStep, int totalSteps)
        {
            if (_completionFired) return;
            if (newStep < totalSteps) return;
            _completionFired = true;
            StartCoroutine(CompleteAfterDwell());
        }

        IEnumerator CompleteAfterDwell()
        {
            yield return new WaitForSeconds(_completionDwellSeconds);

            _session.EmitUserAction("scenario_complete");
            // Give the WebSocket pump a tick to flush the trailing events before the
            // scene unloads. EndSessionAsync also fires when SessionRunner disables.
            yield return new WaitForSeconds(0.3f);

            var lifecycle = AppLifecycle.Instance;
            if (lifecycle != null)
            {
                lifecycle.ReturnToShell();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
