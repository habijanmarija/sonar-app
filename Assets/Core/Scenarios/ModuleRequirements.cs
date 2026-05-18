namespace Host.Scenarios
{
    /// Capabilities a scenario module needs from the runtime. Used by the launcher to
    /// gate selection: a scenario requiring hand tracking on a headset without it is
    /// listed as "unavailable" instead of being launchable.
    public readonly struct ModuleRequirements
    {
        public readonly bool NeedsPassthrough;
        public readonly bool NeedsHandTracking;
        public readonly bool NeedsEyeTracking;
        public readonly bool NeedsExternalTracker;

        public ModuleRequirements(
            bool needsPassthrough = false,
            bool needsHandTracking = false,
            bool needsEyeTracking = false,
            bool needsExternalTracker = false)
        {
            NeedsPassthrough = needsPassthrough;
            NeedsHandTracking = needsHandTracking;
            NeedsEyeTracking = needsEyeTracking;
            NeedsExternalTracker = needsExternalTracker;
        }
    }
}
