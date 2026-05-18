using System.Collections.Generic;
using NUnit.Framework;
using Sonar.Analytics;
using Sonar.Scenario;
using UnityEngine;

namespace Sonar.Analytics.Tests
{
    public class SkillScorerTests
    {
        static PlannedTrajectory ForwardTrajectory() => new()
        {
            EntryRaw = new[] { 0f, 0f, 0f },
            AxisRaw = new[] { 0f, 0f, 1f },
            PlannedDepthM = 0.03f,
            MinDepthM = 0.025f,
            MaxDepthM = 0.035f,
        };

        static List<PoseSample> StraightStream(int n, float speed = 0.01f)
        {
            var list = new List<PoseSample>(n);
            for (int i = 0; i < n; i++)
                list.Add(new PoseSample(new Vector3(0f, 0f, i * speed * 0.05f), Vector3.forward, i * 0.05f));
            return list;
        }

        [Test]
        public void Smoothness_StraightConstantSpeed_IsNearOne()
        {
            var s = StraightStream(40);
            var score = SmoothnessScorer.Score(s);
            Assert.That(score, Is.GreaterThan(0.95f));
        }

        [Test]
        public void Smoothness_JerkyStream_IsMuchLower()
        {
            var jerky = new List<PoseSample>();
            for (int i = 0; i < 40; i++)
            {
                float wobble = (i % 2 == 0) ? 0.005f : -0.005f;
                jerky.Add(new PoseSample(new Vector3(wobble, 0f, i * 0.0005f), Vector3.forward, i * 0.05f));
            }
            var score = SmoothnessScorer.Score(jerky);
            Assert.That(score, Is.LessThan(0.5f));
        }

        [Test]
        public void PathEfficiency_StraightAlongAxis_IsOne()
        {
            var s = StraightStream(20);
            var traj = ForwardTrajectory();
            var score = PathEfficiencyScorer.Score(s, traj);
            Assert.That(score, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void PathEfficiency_WigglyStream_IsLessThanOne()
        {
            var wiggle = new List<PoseSample>();
            for (int i = 0; i < 20; i++)
            {
                float lateral = 0.005f * Mathf.Sin(i * 0.5f);
                wiggle.Add(new PoseSample(new Vector3(lateral, 0f, i * 0.001f), Vector3.forward, i * 0.05f));
            }
            var score = PathEfficiencyScorer.Score(wiggle, ForwardTrajectory());
            Assert.That(score, Is.LessThan(0.7f));
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Composite_PerfectStreamNoErrors_IsHigh()
        {
            var s = StraightStream(40);
            var score = SkillScorer.Score(s, ForwardTrajectory(), errorCount: 0);
            Assert.That(score.Composite, Is.GreaterThan(0.85f));
            Assert.That(score.ErrorPenalty, Is.EqualTo(0f));
        }

        [Test]
        public void Composite_ErrorsClampToZero()
        {
            var s = StraightStream(40);
            var score = SkillScorer.Score(s, ForwardTrajectory(), errorCount: 20);
            Assert.That(score.ErrorPenalty, Is.EqualTo(1f));
            Assert.That(score.Composite, Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
