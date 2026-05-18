using System;
using System.Collections.Generic;
using Host.App;
using Host.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.UI.Settings
{
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/UI/Settings Controller")]
    public sealed class SettingsController : MonoBehaviour
    {
        public event Action OnClose;

        UIDocument _doc;
        TextField _backend;
        TextField _rest;
        DropdownField _defaultMode;
        DropdownField _idStrategy;
        Toggle _autoOpenResults;
        Label _status;

        static readonly List<string> Modes = new() { "Novice", "Intermediate", "Expert" };
        static readonly List<string> Strategies = new() { "sequential", "manual" };

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;
            _backend = root.Q<TextField>("backend-url");
            _rest = root.Q<TextField>("rest-url");
            _defaultMode = root.Q<DropdownField>("default-mode");
            _idStrategy = root.Q<DropdownField>("id-strategy");
            _autoOpenResults = root.Q<Toggle>("auto-open-results");
            _status = root.Q<Label>("status");

            _defaultMode.choices = Modes;
            _idStrategy.choices = Strategies;

            var s = AppLifecycle.Instance?.Settings ?? AppSettings.Load();
            _backend.value = s.BackendUrl;
            _rest.value = s.RestUrl;
            _defaultMode.value = s.DefaultMode;
            _idStrategy.value = s.ParticipantIdStrategy;
            _autoOpenResults.value = s.AutoOpenResults;

            root.Q<Button>("save").clicked += HandleSave;
            root.Q<Button>("close").clicked += () => OnClose?.Invoke();
        }

        void HandleSave()
        {
            var s = AppLifecycle.Instance?.Settings ?? AppSettings.Load();
            s.BackendUrl = _backend.value.Trim();
            s.RestUrl = _rest.value.Trim();
            s.DefaultMode = _defaultMode.value;
            s.ParticipantIdStrategy = _idStrategy.value;
            s.AutoOpenResults = _autoOpenResults.value;
            s.Save();
            _status.text = "Saved.";
        }
    }
}
