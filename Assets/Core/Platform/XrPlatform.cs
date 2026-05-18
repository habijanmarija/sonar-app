using System;
using UnityEngine;

namespace Host.Platform
{
    public static class XrPlatform
    {
        static IXrPlatform _current;

        public static IXrPlatform Current => _current ??= Detect();

        public static void Override(IXrPlatform platform)
        {
            _current = platform;
        }

        static IXrPlatform Detect()
        {
#if UNITY_EDITOR
            return new EditorFlatPlatform();
#else
            var model = SystemInfo.deviceModel ?? string.Empty;
            if (Match(model, "Quest") || Match(model, "Oculus") || Match(model, "Meta"))
                return new MetaPlatform(model);
            if (Match(model, "VIVE") || Match(model, "Focus") || Match(model, "HTC") || Match(model, "Wave"))
                return new VivePlatform(model);
            return new EditorFlatPlatform();
#endif
        }

        static bool Match(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
