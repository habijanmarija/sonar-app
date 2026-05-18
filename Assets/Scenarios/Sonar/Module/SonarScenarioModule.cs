using System.Collections.Generic;
using Host.Scenarios;
using UnityEngine;

namespace Sonar.Module
{
    /// SONAR: pedicle screw placement / bone drilling. Registered with ScenarioRegistry
    /// at runtime before the first scene loads, so the main menu always sees it as available.
    public sealed class SonarScenarioModule : IScenarioModule
    {
        public string Id => "sonar_bone_drilling";
        public string DisplayName => "SONAR: Pedicle Screw Placement";

        public string Description =>
            "AR-guided pedicle screw placement on a 3D-printed phantom. Calibration with " +
            "Aruco + landmark touch-up; 8-step task graph with green/yellow/red feedback; " +
            "telemetry over WebSocket to the Python backend.";

        public string SceneName => "Sonar_BoneDrilling";

        public ModuleRequirements Requirements => new(
            needsPassthrough: true,
            needsHandTracking: false,
            needsEyeTracking: false,
            needsExternalTracker: false);

        public IReadOnlyList<string> SupportedModes => new[] { "Novice", "Intermediate", "Expert" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScenarioRegistry.Register(new SonarScenarioModule());
        }
    }
}
