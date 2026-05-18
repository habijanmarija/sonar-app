using NUnit.Framework;
using Sonar.Guidance;
using Sonar.Scenario;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Guidance.Tests
{
    public class TrajectoryEvaluatorTests
    {
        static (PlannedTrajectory, ThresholdProfile) Setup()
        {
            var t = new PlannedTrajectory
            {
                EntryRaw = new[] { 0f, 0f, 0f },
                AxisRaw = new[] { 0f, 0f, 1f },
                PlannedDepthM = 0.03f,
                MinDepthM = 0.025f,
                MaxDepthM = 0.035f,
            };
            return (t, new ThresholdProfile());
        }

        [Test]
        public void OnAxisAtPlannedDepth_AllGreen()
        {
            var (t, p) = Setup();
            var tip = new DrillTipPose(new Vector3(0f, 0f, 0.03f), Vector3.forward, 0f);
            var f = TrajectoryEvaluator.Evaluate(in tip, t, p);
            Assert.That(f.LateralGate, Is.EqualTo(Gate.Green));
            Assert.That(f.AngularGate, Is.EqualTo(Gate.Green));
            Assert.That(f.DepthGate, Is.EqualTo(Gate.Green));
            Assert.That(f.Worst, Is.EqualTo(Gate.Green));
        }

        [Test]
        public void LateralOffset_BumpsLateralGate()
        {
            var (t, p) = Setup();
            var tip = new DrillTipPose(new Vector3(0.004f, 0f, 0.03f), Vector3.forward, 0f);
            var f = TrajectoryEvaluator.Evaluate(in tip, t, p);
            Assert.That(f.LateralGate, Is.EqualTo(Gate.Red));
            Assert.That(f.LateralM, Is.EqualTo(0.004f).Within(1e-5f));
        }

        [Test]
        public void AngularOffset_BumpsAngularGate()
        {
            var (t, p) = Setup();
            var tilted = Quaternion.Euler(15f, 0f, 0f) * Vector3.forward;
            var tip = new DrillTipPose(new Vector3(0f, 0f, 0.03f), tilted, 0f);
            var f = TrajectoryEvaluator.Evaluate(in tip, t, p);
            Assert.That(f.AngularGate, Is.EqualTo(Gate.Red));
        }

        [Test]
        public void TooDeep_BumpsDepthGate()
        {
            var (t, p) = Setup();
            var tip = new DrillTipPose(new Vector3(0f, 0f, 0.040f), Vector3.forward, 0f);
            var f = TrajectoryEvaluator.Evaluate(in tip, t, p);
            Assert.That(f.DepthGate, Is.EqualTo(Gate.Red));
            Assert.That(f.DepthErrorM, Is.GreaterThan(0f));
        }

        [Test]
        public void InvalidTip_AllRed()
        {
            var (t, p) = Setup();
            var f = TrajectoryEvaluator.Evaluate(DrillTipPose.Invalid, t, p);
            Assert.That(f.LateralGate, Is.EqualTo(Gate.Red));
            Assert.That(f.AngularGate, Is.EqualTo(Gate.Red));
            Assert.That(f.DepthGate, Is.EqualTo(Gate.Red));
        }
    }
}
