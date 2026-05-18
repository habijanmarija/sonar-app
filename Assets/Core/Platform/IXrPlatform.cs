namespace Host.Platform
{
    public interface IXrPlatform
    {
        XrVendor Vendor { get; }
        string RuntimeName { get; }

        bool HasPassthrough { get; }
        bool HasHandTracking { get; }
        bool HasEyeTracking { get; }

        bool TryEnablePassthrough(bool enabled);
        bool TrySetRefreshRate(float hz);
    }
}
