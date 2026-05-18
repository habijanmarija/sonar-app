using System.Collections.Generic;

namespace Host.Patients.Loaders
{
    /// Maps format token → IMeshLoader instance. Loaders self-register at boot via
    /// RuntimeInitializeOnLoadMethod, same pattern as ScenarioRegistry.
    public static class MeshLoaderRegistry
    {
        static readonly Dictionary<string, IMeshLoader> _loaders = new();

        public static void Register(IMeshLoader loader)
        {
            if (loader == null || string.IsNullOrEmpty(loader.Format)) return;
            _loaders[loader.Format.ToLowerInvariant()] = loader;
        }

        public static IMeshLoader For(string format)
        {
            if (string.IsNullOrEmpty(format)) return null;
            _loaders.TryGetValue(format.ToLowerInvariant(), out var loader);
            return loader;
        }

        public static IReadOnlyCollection<string> SupportedFormats => _loaders.Keys;
    }
}
