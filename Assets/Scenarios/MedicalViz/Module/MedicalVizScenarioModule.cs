using System.Collections.Generic;
using Host.Scenarios;
using UnityEngine;

namespace MedicalViz.Module
{
    /// Medical Visualization — imhotep-equivalent: load a patient case, visualise meshes
    /// and DICOM, annotate, save. Not a training scenario; no telemetry-driven step graph.
    /// Phase A registers the module; the scene + tools come online incrementally per
    /// IMHOTEP_PORT_PLAN.md phases B → G.
    public sealed class MedicalVizScenarioModule : IScenarioModule
    {
        public string Id => "medical_viz";
        public string DisplayName => "Medical Visualization";

        public string Description =>
            "Load a patient case (DICOM + segmented organ meshes), visualise in 3D, " +
            "drop annotations, save. Imhotep-equivalent workflow.";

        public string SceneName => "MedicalViz_Workstation";

        public ModuleRequirements Requirements => new(
            needsPassthrough: false,
            needsHandTracking: false,
            needsEyeTracking: false,
            needsExternalTracker: false);

        public IReadOnlyList<string> SupportedModes => new[] { "Default" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ScenarioRegistry.Register(new MedicalVizScenarioModule());
        }
    }
}
