using System.Collections.Generic;

namespace Host.Patients.Loaders
{
    /// Maps format token → IImageSeriesLoader. Loaders self-register at boot.
    public static class ImageSeriesLoaderRegistry
    {
        static readonly Dictionary<string, IImageSeriesLoader> _loaders = new();

        public static void Register(IImageSeriesLoader loader)
        {
            if (loader == null || string.IsNullOrEmpty(loader.Format)) return;
            _loaders[loader.Format.ToLowerInvariant()] = loader;
        }

        public static IImageSeriesLoader For(string format)
        {
            if (string.IsNullOrEmpty(format)) return null;
            _loaders.TryGetValue(format.ToLowerInvariant(), out var loader);
            return loader;
        }

        public static IReadOnlyCollection<string> SupportedFormats => _loaders.Keys;
    }
}
