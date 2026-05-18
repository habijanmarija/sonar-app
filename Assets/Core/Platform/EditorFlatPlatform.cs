namespace Host.Platform
{
    public sealed class EditorFlatPlatform : IXrPlatform
    {
        public XrVendor Vendor => XrVendor.EditorFlat;
        public string RuntimeName => "Editor (flat-mode)";

        public bool HasPassthrough => false;
        public bool HasHandTracking => false;
        public bool HasEyeTracking => false;

        public bool TryEnablePassthrough(bool enabled) => false;
        public bool TrySetRefreshRate(float hz) => false;
    }
}
