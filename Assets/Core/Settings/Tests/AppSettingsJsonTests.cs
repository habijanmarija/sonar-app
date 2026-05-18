using Newtonsoft.Json;
using NUnit.Framework;
using Host.Settings;

namespace Host.Settings.Tests
{
    public class AppSettingsJsonTests
    {
        [Test]
        public void Defaults_AreSensible()
        {
            var s = new AppSettings();
            Assert.That(s.BackendUrl, Does.StartWith("ws://"));
            Assert.That(s.RestUrl, Does.StartWith("http://"));
            Assert.That(s.DefaultMode, Is.EqualTo("Novice"));
            Assert.That(s.AutoOpenResults, Is.True);
        }

        [Test]
        public void JsonRoundtrip_PreservesValues()
        {
            var s = new AppSettings
            {
                BackendUrl = "ws://10.0.0.5:8765",
                DefaultMode = "Expert",
                AutoOpenResults = false,
                ParticipantIdStrategy = "manual",
            };
            var json = JsonConvert.SerializeObject(s);
            var t = JsonConvert.DeserializeObject<AppSettings>(json);
            Assert.That(t.BackendUrl, Is.EqualTo("ws://10.0.0.5:8765"));
            Assert.That(t.DefaultMode, Is.EqualTo("Expert"));
            Assert.That(t.AutoOpenResults, Is.False);
            Assert.That(t.ParticipantIdStrategy, Is.EqualTo("manual"));
        }
    }
}
