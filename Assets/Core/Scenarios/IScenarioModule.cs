using System.Collections.Generic;

namespace Host.Scenarios
{
    /// One AR training scenario the host can launch. Each scenario lives in its own folder
    /// under `Assets/Scenarios/<Id>/` and provides a concrete IScenarioModule that the
    /// ScenarioRegistry knows about.
    public interface IScenarioModule
    {
        /// Stable machine identifier; matches the `scenario` field in telemetry events.
        string Id { get; }

        /// Human-readable label shown in the main menu.
        string DisplayName { get; }

        /// One-paragraph description shown in the scenario card.
        string Description { get; }

        /// Unity scene name in build settings (no extension).
        string SceneName { get; }

        /// XR capabilities this scenario needs.
        ModuleRequirements Requirements { get; }

        /// Modes this scenario supports; usually a subset of {Novice, Intermediate, Expert}.
        IReadOnlyList<string> SupportedModes { get; }
    }
}
