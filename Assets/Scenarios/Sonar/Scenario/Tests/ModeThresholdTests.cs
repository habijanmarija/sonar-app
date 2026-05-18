using NUnit.Framework;
using Sonar.Scenario;

namespace Sonar.Scenario.Tests
{
    public class ModeThresholdTests
    {
        const string Json = @"{
          ""scenario"": ""bone_drilling"",
          ""trajectory"": {
            ""entry_m"":[0,0,0], ""axis_unit"":[0,0,1],
            ""planned_depth_m"": 0.035, ""min_depth_m"": 0.030, ""max_depth_m"": 0.040
          },
          ""steps"": [{""number"":1,""name"":""a"",""hold_seconds"":0,""errors"":[]}],
          ""thresholds"": {
            ""lateral_green_m"": 0.0015, ""lateral_red_m"": 0.003,
            ""angular_green_deg"": 5.0, ""angular_red_deg"": 10.0,
            ""depth_green_m"": 0.002, ""depth_red_m"": 0.005,
            ""registration_gate_m"": 0.002, ""hysteresis_fraction"": 0.2
          },
          ""mode_thresholds"": {
            ""Expert"": {
              ""lateral_green_m"": 0.0010, ""lateral_red_m"": 0.0020,
              ""angular_green_deg"": 3.0, ""angular_red_deg"": 7.0,
              ""depth_green_m"": 0.0015, ""depth_red_m"": 0.0035,
              ""registration_gate_m"": 0.002, ""hysteresis_fraction"": 0.2
            }
          }
        }";

        [Test]
        public void ThresholdsFor_Expert_UsesOverride()
        {
            var cfg = ScenarioLoader.FromJson(Json);
            var t = cfg.ThresholdsFor(SessionMode.Expert);
            Assert.That(t.LateralGreenM, Is.EqualTo(0.0010f).Within(1e-6f));
            Assert.That(t.AngularRedDeg, Is.EqualTo(7.0f).Within(1e-3f));
        }

        [Test]
        public void ThresholdsFor_NoviceMissing_FallsBackToBase()
        {
            var cfg = ScenarioLoader.FromJson(Json);
            var t = cfg.ThresholdsFor(SessionMode.Novice);
            Assert.That(t.LateralGreenM, Is.EqualTo(0.0015f).Within(1e-6f));
        }
    }
}
