using System.Collections.Generic;
using System.Linq;

namespace Host.Scenarios
{
    /// Hand-registered set of scenario modules available to the launcher.
    /// Scenarios call Register(...) once at boot via their ScenarioRegistrar entry point.
    /// (Reflection-based discovery is a future optimisation — not needed at two scenarios.)
    public static class ScenarioRegistry
    {
        static readonly List<IScenarioModule> _modules = new();

        public static IReadOnlyList<IScenarioModule> All => _modules;

        public static void Register(IScenarioModule module)
        {
            if (module == null) return;
            if (_modules.Any(m => m.Id == module.Id)) return;
            _modules.Add(module);
        }

        public static IScenarioModule Find(string id) =>
            _modules.FirstOrDefault(m => m.Id == id);

        public static void Clear() => _modules.Clear();
    }
}
