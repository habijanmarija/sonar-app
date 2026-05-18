using NUnit.Framework;
using Sonar.Guidance;

namespace Sonar.Guidance.Tests
{
    public class AdaptiveHintProviderTests
    {
        const string Json = @"{
          ""3:entry_point_error"": ""hello"",
          ""5:trajectory_deviation"": [""a"", ""b"", ""c""]
        }";

        [Test]
        public void Get_String_ReturnsAsSingleton()
        {
            var p = AdaptiveHintProvider.FromJson(Json);
            Assert.That(p.Get(3, "entry_point_error"), Is.EqualTo("hello"));
            Assert.That(p.Get(3, "entry_point_error"), Is.EqualTo("hello"));
        }

        [Test]
        public void Get_ListCyclesAndStopsAtFinal()
        {
            var p = AdaptiveHintProvider.FromJson(Json);
            Assert.That(p.Get(5, "trajectory_deviation"), Is.EqualTo("a"));
            Assert.That(p.Get(5, "trajectory_deviation"), Is.EqualTo("b"));
            Assert.That(p.Get(5, "trajectory_deviation"), Is.EqualTo("c"));
            Assert.That(p.Get(5, "trajectory_deviation"), Is.EqualTo("c"));
        }

        [Test]
        public void OnStepChanged_ResetsCursors()
        {
            var p = AdaptiveHintProvider.FromJson(Json);
            p.Get(5, "trajectory_deviation");
            p.Get(5, "trajectory_deviation");
            p.OnStepChanged(6);
            p.OnStepChanged(5); // back to step 5 (rare but possible if user re-runs)
            Assert.That(p.Get(5, "trajectory_deviation"), Is.EqualTo("a"));
        }

        [Test]
        public void Get_MissingKey_ReturnsEmpty()
        {
            var p = AdaptiveHintProvider.FromJson(Json);
            Assert.That(p.Get(9, "unknown_code"), Is.Empty);
        }
    }
}
