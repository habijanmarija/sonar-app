using NUnit.Framework;
using Sonar.Tracking;
using UnityEngine;

namespace Sonar.Tracking.Tests
{
    public class PoseSmootherTests
    {
        [Test]
        public void FirstSample_Initialises()
        {
            var s = new PoseSmoother();
            var ok = s.Push(new Vector3(1, 2, 3), Vector3.right, 0f);
            Assert.That(ok, Is.True);
            Assert.That(s.SmoothedPosition, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(s.SmoothedAxis, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void StationaryStream_ConvergesToInput()
        {
            var s = new PoseSmoother(positionAlpha: 0.5f);
            for (int i = 0; i < 50; i++)
                s.Push(Vector3.up, Vector3.forward, i * 0.01f);
            Assert.That((s.SmoothedPosition - Vector3.up).magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void OutlierJump_IsRejected()
        {
            var s = new PoseSmoother(maxJumpMetresPerSecond: 1f);
            s.Push(Vector3.zero, Vector3.forward, 0f);
            // 10 metres in 10 ms = 1000 m/s; well above 1 m/s rejection threshold.
            var ok = s.Push(new Vector3(10f, 0f, 0f), Vector3.forward, 0.01f);
            Assert.That(ok, Is.False);
            Assert.That(s.SmoothedPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void GradualMotion_IsAccepted()
        {
            var s = new PoseSmoother(maxJumpMetresPerSecond: 2f, positionAlpha: 0.5f);
            s.Push(Vector3.zero, Vector3.forward, 0f);
            // 1 cm in 100 ms = 0.1 m/s; well below threshold.
            var ok = s.Push(new Vector3(0.01f, 0f, 0f), Vector3.forward, 0.1f);
            Assert.That(ok, Is.True);
            Assert.That(s.SmoothedPosition.x, Is.GreaterThan(0f));
        }

        [Test]
        public void Reset_ClearsState()
        {
            var s = new PoseSmoother();
            s.Push(new Vector3(5, 5, 5), Vector3.right, 0f);
            s.Reset();
            s.Push(Vector3.zero, Vector3.up, 0f);
            Assert.That(s.SmoothedPosition, Is.EqualTo(Vector3.zero));
            Assert.That(s.SmoothedAxis, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void ZeroAxis_FallsBackToPrior()
        {
            var s = new PoseSmoother();
            s.Push(Vector3.zero, Vector3.right, 0f);
            s.Push(Vector3.zero, Vector3.zero, 0.01f);
            Assert.That((s.SmoothedAxis - Vector3.right).magnitude, Is.LessThan(1e-4f));
        }
    }
}
