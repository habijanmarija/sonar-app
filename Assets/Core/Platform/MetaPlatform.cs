namespace Host.Platform
{
    public sealed class MetaPlatform : IXrPlatform
    {
        public XrVendor Vendor => XrVendor.MetaQuest;
        public string RuntimeName { get; }

        public bool HasPassthrough => true;
        public bool HasHandTracking => true;

        // Quest 3 has no inside-out eye tracking; Quest Pro / Quest 3S do.
        // Override per-device once we add SystemInfo-based fan-out.
        public bool HasEyeTracking => false;

        public MetaPlatform(string runtimeName)
        {
            RuntimeName = string.IsNullOrEmpty(runtimeName) ? "Meta" : runtimeName;
        }

        public bool TryEnablePassthrough(bool enabled) => false;
        public bool TrySetRefreshRate(float hz) => false;
    }
}
