using System;
using System.Collections.Generic;
using Host.App;
using Host.Participant;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.UI.Intake
{
    /// Drives the participant-intake UI Toolkit panel. Wires field state ↔ ParticipantSession
    /// + ParticipantIntake; persists on Continue; raises OnContinue with the saved session.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/UI/Participant Intake Controller")]
    public sealed class ParticipantIntakeController : MonoBehaviour
    {
        public event Action<ParticipantSession> OnContinue;
        public event Action OnCancel;

        UIDocument _doc;
        TextField _pseudonym;
        DropdownField _ageBand;
        DropdownField _handedness;
        DropdownField _priorTraining;
        DropdownField _mode;
        Toggle _visionCorrected;
        Toggle _vrSusceptible;
        Toggle _photosensitive;
        Toggle _consent;
        Label _validation;

        static readonly List<string> AgeBands = new() { "18–25", "26–35", "36–50", "50+" };
        static readonly List<string> Handedness = new() { "right", "left", "ambidextrous" };
        static readonly List<string> PriorTraining = new() { "none", "observer", "trainee", "expert" };
        static readonly List<string> Modes = new() { "Novice", "Intermediate", "Expert" };

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            _pseudonym = root.Q<TextField>("pseudonym");
            _ageBand = root.Q<DropdownField>("age-band");
            _handedness = root.Q<DropdownField>("handedness");
            _priorTraining = root.Q<DropdownField>("prior-training");
            _mode = root.Q<DropdownField>("mode");
            _visionCorrected = root.Q<Toggle>("vision-corrected");
            _vrSusceptible = root.Q<Toggle>("vr-susceptible");
            _photosensitive = root.Q<Toggle>("photosensitive");
            _consent = root.Q<Toggle>("consent");
            _validation = root.Q<Label>("validation");

            _ageBand.choices = AgeBands; _ageBand.index = 0;
            _handedness.choices = Handedness; _handedness.index = 0;
            _priorTraining.choices = PriorTraining; _priorTraining.index = 0;
            _mode.choices = Modes;
            _mode.value = AppLifecycle.Instance?.Settings?.DefaultMode ?? "Novice";

            _pseudonym.value = ParticipantIdGenerator.NextSequential();

            root.Q<Button>("continue").clicked += HandleContinue;
            root.Q<Button>("cancel").clicked += () => OnCancel?.Invoke();
        }

        void HandleContinue()
        {
            _validation.text = "";

            if (string.IsNullOrWhiteSpace(_pseudonym.value))
            {
                _validation.text = "Pseudonym is required.";
                return;
            }
            if (!_consent.value)
            {
                _validation.text = "Consent confirmation is required before continuing.";
                return;
            }
            if (_vrSusceptible.value)
            {
                _validation.text = "Excluded: known severe VR motion sickness.";
                return;
            }
            if (_photosensitive.value)
            {
                _validation.text = "Excluded: photosensitive epilepsy.";
                return;
            }

            var scenarioId = AppLifecycle.Instance?.ActiveScenario?.Id ?? "";

            var session = ParticipantSession.New(_pseudonym.value.Trim(), _mode.value, scenarioId);
            session.Intake = new ParticipantIntake
            {
                AgeBand = _ageBand.value,
                Handedness = _handedness.value,
                PriorTraining = _priorTraining.value,
                VisionCorrected = _visionCorrected.value,
                VrSusceptible = _vrSusceptible.value,
                Photosensitive = _photosensitive.value,
                ConsentGiven = _consent.value,
                ConsentUtc = DateTime.UtcNow,
            };

            if (!ParticipantStore.Save(session))
            {
                _validation.text = "Failed to persist participant record. Check disk permissions.";
                return;
            }

            OnContinue?.Invoke(session);
        }
    }
}
