using NUnit.Framework;
using Sonar.Guidance;
using Sonar.Scenario;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Guidance.Tests
{
    public class StepRecognizerTests
    {
        static PlannedTrajectory Forward() => new()
        {
            EntryRaw = new[] { 0f, 0f, 0f },
            AxisRaw = new[] { 0f, 0f, 1f },
            PlannedDepthM = 0.03f,
            MinDepthM = 0.025f,
            MaxDepthM = 0.035f,
        };

        [Test]
        public void FarFromEntry_RecognisesCalibrate()
        {
            var r = new StepRecognizer(Forward());
            var tip = new DrillTipPose(new Vector3(0.2f, 0.2f, 0.2f), Vector3.forward, 0f);
            Assert.That(r.Push(tip), Is.EqualTo(1));
        }

        [Test]
        public void AtEntryNotAligned_RecognisesMarkEntry()
        {
            var r = new StepRecognizer(Forward());
            r.Push(new DrillTipPose(Vector3.zero, Quaternion.Euler(30f, 0, 0) * Vector3.forward, 0f));
            var step = r.Push(new DrillTipPose(Vector3.zero, Quaternion.Euler(30f, 0, 0) * Vector3.forward, 0.1f));
            Assert.That(step, Is.EqualTo(3));
        }

        [Test]
        public void AtEntryAligned_RecognisesEstablishTrajectory()
        {
            var r = new StepRecognizer(Forward());
            r.Push(new DrillTipPose(Vector3.zero, Vector3.forward, 0f));
            var step = r.Push(new DrillTipPose(Vector3.zero, Vector3.forward, 0.1f));
            Assert.That(step, Is.EqualTo(4));
        }

        [Test]
        public void AdvancingAlongAxis_RecognisesInsert()
        {
            var r = new StepRecognizer(Forward());
            r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.005f), Vector3.forward, 0.0f));
            var step = r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.012f), Vector3.forward, 0.1f));
            Assert.That(step, Is.EqualTo(5));
        }

        [Test]
        public void AtPlannedDepthStill_RecognisesReachTarget()
        {
            var r = new StepRecognizer(Forward());
            r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.0298f), Vector3.forward, 0.0f));
            var step = r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.030f), Vector3.forward, 0.5f));
            Assert.That(step, Is.EqualTo(6));
        }

        [Test]
        public void RetreatingAlongAxis_RecognisesWithdraw()
        {
            var r = new StepRecognizer(Forward());
            r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.025f), Vector3.forward, 0.0f));
            var step = r.Push(new DrillTipPose(new Vector3(0f, 0f, 0.010f), Vector3.forward, 0.1f));
            Assert.That(step, Is.EqualTo(7));
        }
    }
}
