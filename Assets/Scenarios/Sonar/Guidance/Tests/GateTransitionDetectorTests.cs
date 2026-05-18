using NUnit.Framework;
using Sonar.Guidance;
using Sonar.Scenario;

namespace Sonar.Guidance.Tests
{
    public class GateTransitionDetectorTests
    {
        [Test]
        public void GreenOnly_NeverFires()
        {
            var d = new GateTransitionDetector(redSustainSeconds: 0.3f);
            for (float t = 0; t < 5f; t += 0.1f)
                Assert.That(d.Push(Gate.Green, t), Is.False);
        }

        [Test]
        public void RedSustained_FiresAfterThreshold()
        {
            var d = new GateTransitionDetector(redSustainSeconds: 0.3f);
            Assert.That(d.Push(Gate.Red, 0.0f), Is.False);  // enter red, t0
            Assert.That(d.Push(Gate.Red, 0.1f), Is.False);
            Assert.That(d.Push(Gate.Red, 0.2f), Is.False);
            Assert.That(d.Push(Gate.Red, 0.31f), Is.True);  // crosses 0.3 s
        }

        [Test]
        public void RedFlickerBelowThreshold_DoesNotFire()
        {
            var d = new GateTransitionDetector(redSustainSeconds: 0.3f);
            d.Push(Gate.Red, 0.0f);
            d.Push(Gate.Red, 0.1f);
            d.Push(Gate.Yellow, 0.2f);     // back out of red
            d.Push(Gate.Red, 0.25f);
            Assert.That(d.Push(Gate.Red, 0.5f), Is.False);  // only 0.25 s sustained
        }

        [Test]
        public void Hysteresis_RequiresGreenBetweenFirings()
        {
            var d = new GateTransitionDetector(redSustainSeconds: 0.1f);
            d.Push(Gate.Red, 0.0f);
            Assert.That(d.Push(Gate.Red, 0.2f), Is.True);   // first fire
            Assert.That(d.Push(Gate.Red, 0.4f), Is.False);  // still red, no re-fire
            d.Push(Gate.Yellow, 0.5f);
            d.Push(Gate.Yellow, 0.6f);
            Assert.That(d.Push(Gate.Red, 0.8f), Is.False);  // yellow→red without green: no fire
            d.Push(Gate.Green, 1.0f);
            d.Push(Gate.Red, 1.2f);
            Assert.That(d.Push(Gate.Red, 1.4f), Is.True);   // green→red→sustained: fires
        }
    }
}
