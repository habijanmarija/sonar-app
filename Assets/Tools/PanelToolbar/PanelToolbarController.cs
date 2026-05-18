using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Host.Tools.PanelToolbar
{
    /// Top-of-screen toolbar with one toggle button per workstation panel. Click a
    /// button to show/hide the corresponding panel. Buttons highlight when their
    /// panel is active. The toolbar itself never hides.
    ///
    /// Wire up: workstation calls Configure(List<Entry>) once with one entry per
    /// panel GameObject. Each entry: (label, target). The button labelled `label`
    /// toggles `target.SetActive(!target.activeSelf)` and updates its highlighted style.
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("SONAR Host/Tools/Panel Toolbar Controller")]
    public sealed class PanelToolbarController : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string Label;
            public GameObject Target;
        }

        UIDocument _doc;
        VisualElement _buttonsContainer;
        readonly List<(Entry Entry, Button Button)> _rows = new();

        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            TryBind();
        }

        void Update()
        {
            // UIDocument can take a frame to construct rootVisualElement after the
            // GameObject is freshly activated. Retry until the buttons container resolves.
            if (_buttonsContainer == null) TryBind();
        }

        void TryBind()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            var buttons = root.Q<VisualElement>("buttons");
            if (buttons == null) return;
            _buttonsContainer = buttons;
            RebuildButtons();
        }

        void LateUpdate()
        {
            // Cheap visual sync — buttons reflect each panel's current active state
            // even if something else toggled it.
            foreach (var (e, b) in _rows)
            {
                if (e.Target == null || b == null) continue;
                bool active = e.Target.activeSelf;
                if (active && !b.ClassListContains("active")) b.AddToClassList("active");
                else if (!active && b.ClassListContains("active")) b.RemoveFromClassList("active");
            }
        }

        public void Configure(IReadOnlyList<Entry> entries)
        {
            _rows.Clear();
            if (_buttonsContainer == null)
            {
                // OnEnable hasn't run yet; cache and rebuild later.
                _pendingEntries = entries;
                return;
            }
            _pendingEntries = entries;
            RebuildButtons();
        }

        IReadOnlyList<Entry> _pendingEntries;

        void RebuildButtons()
        {
            if (_buttonsContainer == null) return;
            _buttonsContainer.Clear();
            _rows.Clear();
            if (_pendingEntries == null) return;

            foreach (var e in _pendingEntries)
            {
                if (e.Target == null) continue;
                var entry = e;
                var btn = new Button { text = entry.Label };
                btn.AddToClassList("toolbar-button");
                btn.clicked += () =>
                {
                    if (entry.Target == null) return;
                    bool next = !entry.Target.activeSelf;
                    entry.Target.SetActive(next);
                };
                _buttonsContainer.Add(btn);
                _rows.Add((entry, btn));
            }
        }
    }
}
