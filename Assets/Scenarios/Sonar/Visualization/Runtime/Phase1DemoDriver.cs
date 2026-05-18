using System.Collections;
using Sonar.Guidance;
using Sonar.Scenario;
using Host.Telemetry;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Visualization
{
    /// Phase 1 end-to-end demo, runnable in Linux Editor flat-mode (no headset required).
    /// Drives a ScriptedDrillTipSource through positions that exercise all 8 steps of
    /// the bone_drilling scenario, intentionally provoking one trajectory_deviation
    /// during insertion to confirm the error pathway.
    [AddComponentMenu("SONAR/Visualization/Phase 1 Demo Driver")]
    public sealed class Phase1DemoDriver : MonoBehaviour
    {
        [SerializeField] GuidanceController _guidance;
        [SerializeField] ScriptedDrillTipSource _drill;
        [SerializeField] SessionRunner _session;
        [SerializeField] float _holdSeconds = 1.0f;
        [SerializeField] float _deviationSeconds = 0.6f;
        [SerializeField] bool _quitAfterRun = true;

        IEnumerator Start()
        {
            // 1. Wait for telemetry to be live.
            yield return new WaitUntil(() => _session != null && _session.IsConnected);
            yield return new WaitUntil(() => _guidance != null && _guidance.Config != null);

            var traj = _guidance.Config.Trajectory;
            var entry = traj.Entry;
            var axis = traj.Axis;

            // 2. Emit calibration + begin session at step 1.
            _session.EmitCalibration(1.4);
            _session.EmitUserAction("phase1_demo_started");
            _guidance.BeginSession();

            // 3. Park on the planned trajectory near entry; all-green auto-advances steps 1..4.
            SetTip(entry, axis);
            yield return new WaitForSeconds(_holdSeconds * 4f);

            // 4. Step 5 (insert): advance along axis with a deliberate lateral deviation midway.
            for (float t = 0f; t < 1f; t += 0.05f)
            {
                var d = Mathf.Lerp(0f, traj.PlannedDepthM, t);
                bool deviate = t > 0.35f && t < 0.55f;
                var lateralOffset = deviate ? Vector3.Cross(axis, Vector3.up).normalized * 0.0045f : Vector3.zero;
                SetTip(entry + axis * d + lateralOffset, axis);
                yield return new WaitForSeconds(0.05f);
            }

            // 5. Steps 6..8: land at planned depth (green), then withdraw, then hold for confirm.
            SetTip(entry + axis * traj.PlannedDepthM, axis);
            yield return new WaitForSeconds(_holdSeconds);

            for (float t = 1f; t >= 0f; t -= 0.1f)
            {
                SetTip(entry + axis * (traj.PlannedDepthM * t), axis);
                yield return new WaitForSeconds(0.08f);
            }
            SetTip(entry, axis);
            yield return new WaitForSeconds(_holdSeconds * 2f);

            _session.EmitUserAction("phase1_demo_finished");
            yield return new WaitForSeconds(0.3f);

            if (_quitAfterRun)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void SetTip(Vector3 position, Vector3 axis)
        {
            if (_drill != null) _drill.Set(position, axis);
        }
    }
}
