using Newtonsoft.Json;

namespace Sonar.Scenario
{
    /// Green/yellow/red gates from dossier/05_feedback_script.md.
    /// Phase 1 ships fixed thresholds; Phase 2+ may swap per mode (novice/intermediate/expert).
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ThresholdProfile
    {
        [JsonProperty("lateral_green_m")] public float LateralGreenM = 0.0015f;
        [JsonProperty("lateral_red_m")] public float LateralRedM = 0.003f;

        [JsonProperty("angular_green_deg")] public float AngularGreenDeg = 5f;
        [JsonProperty("angular_red_deg")] public float AngularRedDeg = 10f;

        [JsonProperty("depth_green_m")] public float DepthGreenM = 0.002f;
        [JsonProperty("depth_red_m")] public float DepthRedM = 0.005f;

        [JsonProperty("registration_gate_m")] public float RegistrationGateM = 0.002f;

        /// Hysteresis: once a metric is in yellow/red, require the next-better band
        /// minus this margin before flipping back to green. Prevents oscillation
        /// when the user holds at exactly the boundary.
        [JsonProperty("hysteresis_fraction")] public float HysteresisFraction = 0.2f;

        public Gate ClassifyLateral(float lateralM) =>
            Classify(lateralM, LateralGreenM, LateralRedM);

        public Gate ClassifyAngular(float angularDeg) =>
            Classify(angularDeg, AngularGreenDeg, AngularRedDeg);

        public Gate ClassifyDepthError(float depthErrorM) =>
            Classify(System.Math.Abs(depthErrorM), DepthGreenM, DepthRedM);

        static Gate Classify(float value, float greenMax, float redMin)
        {
            if (value < greenMax) return Gate.Green;
            if (value < redMin) return Gate.Yellow;
            return Gate.Red;
        }
    }

    public enum Gate
    {
        Green = 0,
        Yellow = 1,
        Red = 2,
    }
}
