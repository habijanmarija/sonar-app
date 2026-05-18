using NUnit.Framework;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Scenario.Tests
{
    public class ScenarioLoaderTests
    {
        const string ValidJson = @"{
          ""scenario"": ""bone_drilling"",
          ""config_version"": ""test"",
          ""phantom_version"": ""test"",
          ""trajectory"": {
            ""id"": ""t1"", ""vertebra"": ""L2"", ""side"": ""right"",
            ""entry_m"":   [0.0, 0.0, 0.0],
            ""axis_unit"": [0.0, 0.0, 1.0],
            ""planned_depth_m"": 0.035, ""min_depth_m"": 0.030, ""max_depth_m"": 0.040
          },
          ""steps"": [
            {""number"": 1, ""name"": ""a"", ""hold_seconds"": 0, ""errors"": []},
            {""number"": 2, ""name"": ""b"", ""hold_seconds"": 0, ""errors"": [""skipped_step""]}
          ],
          ""thresholds"": {
            ""lateral_green_m"": 0.0015, ""lateral_red_m"": 0.003,
            ""angular_green_deg"": 5.0, ""angular_red_deg"": 10.0,
            ""depth_green_m"": 0.002, ""depth_red_m"": 0.005,
            ""registration_gate_m"": 0.002, ""hysteresis_fraction"": 0.2
          }
        }";

        [Test]
        public void FromJson_Valid_Roundtrips()
        {
            var cfg = ScenarioLoader.FromJson(ValidJson);
            Assert.That(cfg.Scenario, Is.EqualTo("bone_drilling"));
            Assert.That(cfg.TotalSteps, Is.EqualTo(2));
            Assert.That(cfg.Step(2).AllowsError("skipped_step"), Is.True);
            Assert.That(cfg.Step(2).AllowsError("excessive_depth"), Is.False);
            Assert.That(cfg.Trajectory.Axis, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Trajectory_LateralError_OnAxisIsZero()
        {
            var cfg = ScenarioLoader.FromJson(ValidJson);
            var onAxis = cfg.Trajectory.Entry + cfg.Trajectory.Axis * 0.02f;
            Assert.That(cfg.Trajectory.LateralError(onAxis), Is.LessThan(1e-5f));
        }

        [Test]
        public void Trajectory_LateralError_OffAxisMatchesPerpendicular()
        {
            var cfg = ScenarioLoader.FromJson(ValidJson);
            var p = cfg.Trajectory.Entry + cfg.Trajectory.Axis * 0.02f + new Vector3(0.004f, 0f, 0f);
            Assert.That(cfg.Trajectory.LateralError(p), Is.EqualTo(0.004f).Within(1e-5f));
        }

        [Test]
        public void Trajectory_AngularError_AlignedIsZero()
        {
            var cfg = ScenarioLoader.FromJson(ValidJson);
            Assert.That(cfg.Trajectory.AngularErrorDeg(cfg.Trajectory.Axis), Is.LessThan(1e-3f));
        }

        [Test]
        public void Trajectory_DepthAlong_MatchesProjection()
        {
            var cfg = ScenarioLoader.FromJson(ValidJson);
            var p = cfg.Trajectory.Entry + cfg.Trajectory.Axis * 0.025f + new Vector3(0.001f, 0f, 0f);
            Assert.That(cfg.Trajectory.DepthAlong(p), Is.EqualTo(0.025f).Within(1e-5f));
        }

        [Test]
        public void Thresholds_ClassifyLateral_BandsAreInclusiveLow()
        {
            var t = new ThresholdProfile();
            Assert.That(t.ClassifyLateral(0.0010f), Is.EqualTo(Gate.Green));
            Assert.That(t.ClassifyLateral(0.0020f), Is.EqualTo(Gate.Yellow));
            Assert.That(t.ClassifyLateral(0.0050f), Is.EqualTo(Gate.Red));
        }

        [Test]
        public void Validate_RejectsZeroAxis()
        {
            const string bad = @"{
              ""scenario"": ""bone_drilling"",
              ""trajectory"": {
                ""entry_m"":[0,0,0], ""axis_unit"":[0,0,0],
                ""planned_depth_m"": 0.035, ""min_depth_m"": 0.030, ""max_depth_m"": 0.040
              },
              ""steps"": [{""number"":1,""name"":""a"",""hold_seconds"":0,""errors"":[]}],
              ""thresholds"": {}
            }";
            Assert.Throws<ScenarioConfigException>(() => ScenarioLoader.FromJson(bad));
        }

        [Test]
        public void Validate_RejectsNonContiguousSteps()
        {
            const string bad = @"{
              ""scenario"": ""bone_drilling"",
              ""trajectory"": {
                ""entry_m"":[0,0,0], ""axis_unit"":[0,0,1],
                ""planned_depth_m"": 0.035, ""min_depth_m"": 0.030, ""max_depth_m"": 0.040
              },
              ""steps"": [
                {""number"":1,""name"":""a"",""hold_seconds"":0,""errors"":[]},
                {""number"":3,""name"":""c"",""hold_seconds"":0,""errors"":[]}
              ],
              ""thresholds"": {}
            }";
            Assert.Throws<ScenarioConfigException>(() => ScenarioLoader.FromJson(bad));
        }
    }
}
