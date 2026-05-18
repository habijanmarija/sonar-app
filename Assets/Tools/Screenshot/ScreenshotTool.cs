using System;
using System.IO;
using Host.App;
using Host.Tools.Notifications;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Host.Tools.Screenshot
{
    /// Hotkey-driven screenshot capture. Saves to
    /// Application.persistentDataPath/screenshots/<session_uuid>_<utc>.png
    /// where <session_uuid> falls back to "no_session" if no active session exists.
    [AddComponentMenu("SONAR Host/Tools/Screenshot Tool")]
    public sealed class ScreenshotTool : MonoBehaviour
    {
        [SerializeField] Key _hotkey = Key.F12;
        [SerializeField] string _subDirectory = "screenshots";

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[_hotkey].wasPressedThisFrame)
                Capture();
        }

        public string Capture()
        {
            try
            {
                var session = AppLifecycle.Instance?.ActiveSession;
                var uuid = session?.SessionUuid ?? "no_session";
                var dir = Path.Combine(Application.persistentDataPath, _subDirectory);
                Directory.CreateDirectory(dir);
                var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
                var file = Path.Combine(dir, $"{uuid}_{stamp}.png");
                ScreenCapture.CaptureScreenshot(file);
                NotificationSystem.Show($"Screenshot: {Path.GetFileName(file)}", NotificationSeverity.Info, 2.5f);
                return file;
            }
            catch (Exception e)
            {
                NotificationSystem.Show($"Screenshot failed: {e.Message}", NotificationSeverity.Error);
                return null;
            }
        }
    }
}
