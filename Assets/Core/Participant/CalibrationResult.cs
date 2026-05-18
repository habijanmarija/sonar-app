using Newtonsoft.Json;
using UnityEngine;

namespace Host.Participant
{
    /// Result of the registration + calibration workflow (dossier/03_registration_workflow.md).
    /// Stored on ParticipantSession; consumed by the scenario at scene start.
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class CalibrationResult
    {
        [JsonProperty("tre_mm")] public double TreMm;
        [JsonProperty("meets_gate")] public bool MeetsGate;
        [JsonProperty("phantom_to_world_position")] public float[] PhantomToWorldPositionRaw;
        [JsonProperty("phantom_to_world_rotation")] public float[] PhantomToWorldRotationRaw;
        [JsonProperty("landmark_count")] public int LandmarkCount;
        [JsonProperty("retry_count")] public int RetryCount;

        public Vector3 PhantomToWorldPosition =>
            PhantomToWorldPositionRaw != null && PhantomToWorldPositionRaw.Length == 3
                ? new Vector3(PhantomToWorldPositionRaw[0], PhantomToWorldPositionRaw[1], PhantomToWorldPositionRaw[2])
                : Vector3.zero;

        public Quaternion PhantomToWorldRotation =>
            PhantomToWorldRotationRaw != null && PhantomToWorldRotationRaw.Length == 4
                ? new Quaternion(PhantomToWorldRotationRaw[0], PhantomToWorldRotationRaw[1], PhantomToWorldRotationRaw[2], PhantomToWorldRotationRaw[3])
                : Quaternion.identity;

        public Matrix4x4 PhantomToWorld =>
            Matrix4x4.TRS(PhantomToWorldPosition, PhantomToWorldRotation, Vector3.one);
    }
}
