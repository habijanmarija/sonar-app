using System.Collections.Generic;
using Host.Scenarios;
using UnityEngine;

namespace NeedleInsertion.Module
{
    /// Needle-insertion scenario stub — proves the host can list/route a second scenario.
    /// Not functionally implemented: the launcher detects `IsStub == true` (via the
    /// SceneName being empty) and surfaces a "not yet implemented" notification instead
    /// of trying to load a non-existent scene.
    public sealed class NeedleInsertionScenarioModule : IScenarioModule
    {
        public string Id => "needle_insertion";
        public string DisplayName => "Needle Insertion (stub)";

        public string Description =>
            "AR-guided needle-insertion trainer on a phantom (A1.2 Table 1, row 2). " +
            "Stub: registration + module surface exist, scene + guidance not yet implemented.";

        // Intentionally empty: signals "stub" to the launcher. When implemented, set to
        // the actual scene name (e.g. "NeedleInsertion_Trainer") and add the scene to Build Settings.
        public string SceneName => "";

        public ModuleRequirements Requirements => new(
            needsPassthrough: true,
            needsHandTracking: false,
            needsEyeTracking: false,
            needsExternalTracker: false);

        public IReadOnlyList<string> SupportedModes => new[] { "Novice" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScenarioRegistry.Register(new NeedleInsertionScenarioModule());
        }
    }
}
