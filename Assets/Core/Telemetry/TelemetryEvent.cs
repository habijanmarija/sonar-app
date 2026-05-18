using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Telemetry
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Vec3
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Quat
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
        [JsonProperty("w")] public float W = 1f;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TelemetryPose
    {
        [JsonProperty("position")] public Vec3 Position;
        [JsonProperty("rotation")] public Quat Rotation;

        public static TelemetryPose From(Transform t)
        {
            var p = t.position;
            var r = t.rotation;
            return new TelemetryPose
            {
                Position = new Vec3 { X = p.x, Y = p.y, Z = p.z },
                Rotation = new Quat { X = r.x, Y = r.y, Z = r.z, W = r.w },
            };
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TelemetryEvent
    {
        [JsonProperty("session_id")] public string SessionId;
        [JsonProperty("timestamp_utc")] public string TimestampUtc;
        [JsonProperty("device_timestamp_ms")] public long DeviceTimestampMs;
        [JsonProperty("event_type")] public string EventType;
        [JsonProperty("scenario")] public string Scenario = "bone_drilling";
        [JsonProperty("current_step")] public int CurrentStep;
        [JsonProperty("total_steps")] public int TotalSteps;

        [JsonProperty("head_pose", NullValueHandling = NullValueHandling.Ignore)]
        public TelemetryPose HeadPose;

        [JsonProperty("controller_pose", NullValueHandling = NullValueHandling.Ignore)]
        public TelemetryPose ControllerPose;

        [JsonProperty("drill_tip_pose", NullValueHandling = NullValueHandling.Ignore)]
        public TelemetryPose DrillTipPose;

        [JsonProperty("hand_joint_data", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, object> HandJointData;

        [JsonProperty("target_registration_error_mm", NullValueHandling = NullValueHandling.Ignore)]
        public double? TargetRegistrationErrorMm;

        [JsonProperty("error_code", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode;

        [JsonProperty("error_detail", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorDetail;

        [JsonProperty("custom_data", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, object> CustomData;
    }

    public static class TelemetryEventType
    {
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
        public const string PoseUpdate = "pose_update";
        public const string StepTransition = "step_transition";
        public const string Calibration = "calibration";
        public const string UserAction = "user_action";
        public const string Error = "error";
    }
}
