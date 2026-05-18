using Sonar.Guidance;
using Host.Telemetry;
using Sonar.Tracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sonar.Visualization.Editor
{
    /// Builds the Phase 1 end-to-end demo scene in-memory so the user can run the full
    /// Tracker → Evaluator → Guidance → Telemetry pipeline from Linux Editor flat-mode.
    static class Phase1DemoSceneMenu
    {
        const string MenuPath = "SONAR/Phase 1/Open Demo Scene";

        [MenuItem(MenuPath)]
        static void OpenPhase1DemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            // Camera setup: look at the phantom origin from a sensible flat-mode pose.
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0.06f, 0.06f, -0.10f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.3f, 1f));
                cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            var rig = new GameObject("Phase1Rig");
            var session = rig.AddComponent<SessionRunner>();
            var drill = rig.AddComponent<ScriptedDrillTipSource>();
            var mirror = rig.AddComponent<DrillTipTransformMirror>();
            mirror.SetSource(drill);

            session.SetDrillTipTransform(mirror.transform);

            var guidance = rig.AddComponent<GuidanceController>();
            // Wire by reflection-free public setters where possible; SerializedObject for inspector-only fields.
            var so = new SerializedObject(guidance);
            so.FindProperty("_drillTipSourceBehaviour").objectReferenceValue = drill;
            so.FindProperty("_sessionRunner").objectReferenceValue = session;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Visualisation: trajectory overlay + tip marker + hint label.
            var overlayHost = new GameObject("TrajectoryOverlay");
            overlayHost.transform.SetParent(rig.transform);
            overlayHost.AddComponent<LineRenderer>();
            var overlay = overlayHost.AddComponent<TrajectoryOverlay>();
            new SerializedObject(overlay)
                .Apply("_guidance", guidance);

            var markerHost = new GameObject("TipGateMarker");
            markerHost.transform.SetParent(rig.transform);
            var marker = markerHost.AddComponent<TipGateMarker>();
            new SerializedObject(marker)
                .Apply("_guidance", guidance)
                .Apply("_drillTipSourceBehaviour", drill);

            var hintHost = new GameObject("HintLabel");
            hintHost.transform.SetParent(rig.transform);
            var hint = hintHost.AddComponent<HintLabel>();
            new SerializedObject(hint)
                .Apply("_guidance", guidance);

            var driver = rig.AddComponent<Phase1DemoDriver>();
            new SerializedObject(driver)
                .Apply("_guidance", guidance)
                .Apply("_drill", drill)
                .Apply("_session", session);

            Selection.activeGameObject = rig;
            SceneManager.SetActiveScene(scene);

            Debug.Log(
                "[SONAR] Phase 1 demo scene ready. " +
                "Confirm SessionRunner._serverUrl points at the backend (default ws://192.168.1.50:8765), " +
                "start the Python backend, then press Play. The driver exercises all 8 steps " +
                "and intentionally triggers one trajectory_deviation during insertion.");
        }
    }

    static class SerializedObjectExtensions
    {
        public static SerializedObject Apply(this SerializedObject so, string property, Object value)
        {
            var p = so.FindProperty(property);
            if (p != null)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[Phase1DemoSceneMenu] property '{property}' not found on {so.targetObject.GetType().Name}");
            }
            return so;
        }
    }
}
