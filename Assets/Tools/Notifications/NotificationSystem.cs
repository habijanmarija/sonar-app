using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.Notifications
{
    public enum NotificationSeverity { Info, Warning, Error }

    /// Cross-scenario toast / banner notification system. One singleton instance lives
    /// in the host's DontDestroyOnLoad GameObject; any code can call
    /// `NotificationSystem.Show("…", NotificationSeverity.Warning)` without taking a
    /// dependency on the host shell directly.
    [AddComponentMenu("SONAR Host/Tools/Notification System")]
    public sealed class NotificationSystem : MonoBehaviour
    {
        public static NotificationSystem Instance { get; private set; }
        public const float DefaultDurationSeconds = 3.5f;

        struct Pending { public string Message; public NotificationSeverity Severity; public float DurationSeconds; }
        readonly Queue<Pending> _queue = new();

        UIDocument _doc;
        VisualElement _root;
        Label _label;
        Coroutine _runner;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _doc = GetComponent<UIDocument>();
            if (_doc != null && _doc.rootVisualElement != null)
                BuildOverlay(_doc.rootVisualElement);
        }

        public static void Show(string message,
                                NotificationSeverity severity = NotificationSeverity.Info,
                                float durationSeconds = DefaultDurationSeconds)
        {
            if (Instance == null)
            {
                Debug.Log($"[Notification] {severity}: {message}");
                return;
            }
            Instance.Enqueue(message, severity, durationSeconds);
        }

        void Enqueue(string message, NotificationSeverity severity, float durationSeconds)
        {
            _queue.Enqueue(new Pending { Message = message, Severity = severity, DurationSeconds = durationSeconds });
            _runner ??= StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (_queue.Count > 0)
            {
                var p = _queue.Dequeue();
                if (_label != null)
                {
                    _label.text = p.Message;
                    _label.RemoveFromClassList("severity-info");
                    _label.RemoveFromClassList("severity-warning");
                    _label.RemoveFromClassList("severity-error");
                    _label.AddToClassList(p.Severity switch
                    {
                        NotificationSeverity.Warning => "severity-warning",
                        NotificationSeverity.Error => "severity-error",
                        _ => "severity-info",
                    });
                    if (_root != null) _root.style.display = DisplayStyle.Flex;
                }
                yield return new WaitForSecondsRealtime(p.DurationSeconds);
                if (_root != null) _root.style.display = DisplayStyle.None;
            }
            _runner = null;
        }

        void BuildOverlay(VisualElement root)
        {
            _root = new VisualElement();
            _root.AddToClassList("notification-root");
            _root.style.position = Position.Absolute;
            _root.style.top = 16;
            _root.style.right = 16;
            _root.style.display = DisplayStyle.None;
            _label = new Label();
            _label.AddToClassList("notification-label");
            _label.style.paddingTop = 8;
            _label.style.paddingBottom = 8;
            _label.style.paddingLeft = 14;
            _label.style.paddingRight = 14;
            _label.style.borderTopLeftRadius = 4;
            _label.style.borderTopRightRadius = 4;
            _label.style.borderBottomLeftRadius = 4;
            _label.style.borderBottomRightRadius = 4;
            _label.style.color = Color.white;
            _label.style.backgroundColor = new Color(0.2f, 0.4f, 0.7f, 0.92f);
            _root.Add(_label);
            root.Add(_root);
        }
    }
}
