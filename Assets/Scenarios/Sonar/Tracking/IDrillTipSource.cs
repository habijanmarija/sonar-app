namespace Sonar.Tracking
{
    /// Source of drill-tip pose. Phase 1 implementation is controller-based with a printed jig offset.
    /// Phase 2 may add an optical-tracker source (e.g. VIVE Tracker on a real drill).
    /// Phase 3 may add a hand-tracking-based source for ungloved trainers demonstrating.
    public interface IDrillTipSource
    {
        DrillTipPose Current { get; }
    }
}
