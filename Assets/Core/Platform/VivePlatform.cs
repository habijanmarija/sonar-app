namespace Host.Platform
{
    public sealed class VivePlatform : IXrPlatform
    {
        public XrVendor Vendor => XrVendor.ViveFocus;
        public string RuntimeName { get; }

        public bool HasPassthrough => true;
        public bool HasHandTracking => true;

        // VIVE Focus Vision ships with eye tracking; older Focus 3 does not.
        // Phase 1 will gate this on SystemInfo.deviceModel.
        public bool HasEyeTracking => true;

        public VivePlatform(string runtimeName)
        {
            RuntimeName = string.IsNullOrEmpty(runtimeName) ? "VIVE Wave" : runtimeName;
        }

        public bool TryEnablePassthrough(bool enabled) => false;
        public bool TrySetRefreshRate(float hz) => false;
    }
}
