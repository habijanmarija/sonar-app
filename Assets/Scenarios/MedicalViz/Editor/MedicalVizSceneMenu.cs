using Host.Tools.AnnotationWidget;
using Host.Tools.DicomVolumeControl;
using Host.Tools.DicomWidget;
using Host.Tools.OpacityControl;
using Host.Tools.OrthoViewer;
using Host.Tools.PanelToolbar;
using Host.Tools.PatientBriefing;
using Host.Tools.ScrewPlanner;
using Host.Tools.PatientSelector;
using Host.Tools.Segmentation;
using Host.Tools.ViewControl;
using Host.Visualization;
using MedicalViz.Module;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MedicalViz.Module.Editor
{
    /// Builds the MedicalViz_Workstation scene in-memory. Mirrors the SONAR demo-scene
    /// menu pattern so the user gets a one-click bootstrap without authoring scene YAML.
    static class MedicalVizSceneMenu
    {
        const string MenuPath = "SONAR/Medical Viz/Open Workstation Scene";
        const string PanelSettingsAssetPath = "Assets/Core/UI/HostPanelSettings.asset";
        const string SelectorUxml = "Assets/Tools/PatientSelector/PatientSelectorPanel.uxml";
        const string BriefingUxml = "Assets/Tools/PatientBriefing/PatientBriefingPanel.uxml";
        const string OpacityUxml = "Assets/Tools/OpacityControl/OpacityControlPanel.uxml";
        const string ViewUxml = "Assets/Tools/ViewControl/ViewControlPanel.uxml";
        const string AnnotationUxml = "Assets/Tools/AnnotationWidget/AnnotationWidgetPanel.uxml";
        const string DicomUxml = "Assets/Tools/DicomWidget/DicomWidgetPanel.uxml";
        const string VolumeUxml = "Assets/Tools/DicomVolumeControl/DicomVolumeControlPanel.uxml";
        const string SegmentationUxml = "Assets/Tools/Segmentation/SegmentationPanel.uxml";
        const string OrthoUxml = "Assets/Tools/OrthoViewer/OrthoViewerPanel.uxml";
        const string ScrewPlannerUxml = "Assets/Tools/ScrewPlanner/ScrewPlannerPanel.uxml";
        const string PanelToolbarUxml = "Assets/Tools/PanelToolbar/PanelToolbarPanel.uxml";

        [MenuItem(MenuPath)]
        static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MedicalViz] Cannot open the workstation scene while Play mode is running. Stop Play first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 0.1f, -0.4f);
                cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
                cam.backgroundColor = Color.white;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // Orbit controls: RMB-drag to rotate around the volume, MMB-drag to pan,
                // scroll to zoom. LMB stays free for brush painting and UI.
                cam.gameObject.AddComponent<OrbitCamera>();
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsAssetPath);
            var selectorAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SelectorUxml);
            var briefingAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BriefingUxml);
            var opacityAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OpacityUxml);
            var viewAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ViewUxml);
            var annotationAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AnnotationUxml);
            var dicomAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DicomUxml);
            var volumeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VolumeUxml);
            var segmentationAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SegmentationUxml);
            var orthoAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(OrthoUxml);
            var screwAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ScrewPlannerUxml);
            var panelToolbarAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelToolbarUxml);

            if (panelSettings == null)
                Debug.LogWarning($"[MedicalViz] PanelSettings not found at {PanelSettingsAssetPath}. UI panels will need it assigned manually.");

            var ws = new GameObject("Workstation");

            var selector = CreateUiPanel(ws, "PatientSelectorPanel", selectorAsset, panelSettings, sortOrder: 0);
            selector.AddComponent<PatientSelectorController>();

            var briefing = CreateUiPanel(ws, "PatientBriefingPanel", briefingAsset, panelSettings, sortOrder: 0);
            briefing.AddComponent<PatientBriefingController>();

            var opacity = CreateUiPanel(ws, "OpacityControlPanel", opacityAsset, panelSettings, sortOrder: 10);
            opacity.AddComponent<OpacityControlController>();

            var view = CreateUiPanel(ws, "ViewControlPanel", viewAsset, panelSettings, sortOrder: 10);
            view.AddComponent<ViewControlController>();

            var annotation = CreateUiPanel(ws, "AnnotationWidgetPanel", annotationAsset, panelSettings, sortOrder: 10);
            annotation.AddComponent<AnnotationWidgetController>();

            // Slice viewer gets a higher sortOrder so its slider receives clicks even
            // if Volume's panel rectangle overlaps it by a few pixels at the bottom.
            var dicom = CreateUiPanel(ws, "DicomWidgetPanel", dicomAsset, panelSettings, sortOrder: 15);
            dicom.AddComponent<DicomWidgetController>();

            var volumeCtrl = CreateUiPanel(ws, "DicomVolumeControlPanel", volumeAsset, panelSettings, sortOrder: 5);
            volumeCtrl.AddComponent<DicomVolumeControlController>();

            // Segmentation sits in the left column at top:310, overlapping Opacity which
            // is empty for image-only patients. Higher sortOrder so its controls win clicks.
            var segmentation = CreateUiPanel(ws, "SegmentationPanel", segmentationAsset, panelSettings, sortOrder: 12);
            segmentation.AddComponent<SegmentationController>();

            // Ortho viewer — three 2D orthogonal slices with palette overlay,
            // floats on the right side. sortOrder mirrors the slice viewer so
            // its sliders accept clicks.
            var ortho = CreateUiPanel(ws, "OrthoViewerPanel", orthoAsset, panelSettings, sortOrder: 14);
            ortho.AddComponent<OrthoViewerController>();

            // Screw planner — SONAR's pedicle-screw placement training loop,
            // scoring trainee placement against the SPIDER per-vertebra masks.
            var screwPlanner = CreateUiPanel(ws, "ScrewPlannerPanel", screwAsset, panelSettings, sortOrder: 16);
            screwPlanner.AddComponent<ScrewPlannerController>();

            // Panel toolbar sits at the top and stays interactive at all times in ViewPatient,
            // so a closed panel can always be reopened. Highest sortOrder so its buttons win clicks.
            var panelToolbar = CreateUiPanel(ws, "PanelToolbar", panelToolbarAsset, panelSettings, sortOrder: 20);
            panelToolbar.AddComponent<PanelToolbarController>();

            var volumeGo = new GameObject("VolumeRenderer");
            volumeGo.transform.SetParent(ws.transform, worldPositionStays: false);
            var volume = volumeGo.AddComponent<VolumeRenderer>();

            // Aim the orbit camera at the volume so RMB-drag rotates around it.
            if (cam != null)
            {
                var orbit = cam.GetComponent<OrbitCamera>();
                if (orbit != null) orbit.SetTarget(volumeGo.transform);
            }

            var placerGo = new GameObject("AnnotationPlacer");
            placerGo.transform.SetParent(ws.transform, worldPositionStays: false);
            var placer = placerGo.AddComponent<AnnotationPlacer>();

            var brushGo = new GameObject("BrushPlacer");
            brushGo.transform.SetParent(ws.transform, worldPositionStays: false);
            var brush = brushGo.AddComponent<BrushPlacer>();

            var content = new GameObject("PatientContent");
            content.transform.SetParent(ws.transform, worldPositionStays: false);

            var workstationGo = new GameObject("MedicalVizWorkstation");
            workstationGo.transform.SetParent(ws.transform, worldPositionStays: false);
            var workstation = workstationGo.AddComponent<MedicalVizWorkstation>();

            var so = new SerializedObject(workstation);
            so.FindProperty("_selector").objectReferenceValue =
                selector.GetComponent<PatientSelectorController>();
            so.FindProperty("_briefing").objectReferenceValue =
                briefing.GetComponent<PatientBriefingController>();
            so.FindProperty("_opacity").objectReferenceValue =
                opacity.GetComponent<OpacityControlController>();
            so.FindProperty("_view").objectReferenceValue =
                view.GetComponent<ViewControlController>();
            so.FindProperty("_annotations").objectReferenceValue =
                annotation.GetComponent<AnnotationWidgetController>();
            so.FindProperty("_placer").objectReferenceValue = placer;
            so.FindProperty("_dicom").objectReferenceValue =
                dicom.GetComponent<DicomWidgetController>();
            so.FindProperty("_volumeControl").objectReferenceValue =
                volumeCtrl.GetComponent<DicomVolumeControlController>();
            so.FindProperty("_volume").objectReferenceValue = volume;
            so.FindProperty("_segmentation").objectReferenceValue =
                segmentation.GetComponent<SegmentationController>();
            so.FindProperty("_brushPlacer").objectReferenceValue = brush;
            so.FindProperty("_ortho").objectReferenceValue =
                ortho.GetComponent<OrthoViewerController>();
            so.FindProperty("_screwPlanner").objectReferenceValue =
                screwPlanner.GetComponent<ScrewPlannerController>();
            so.FindProperty("_toolbar").objectReferenceValue =
                panelToolbar.GetComponent<PanelToolbarController>();
            so.FindProperty("_patientContentRoot").objectReferenceValue = content.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Leave every tool panel active in the saved scene so each controller's
            // OnEnable runs once at Play start and caches its root.Q<>() field
            // references. MedicalVizWorkstation.OnEnable then calls Switch(PickPatient)
            // before the first frame renders — that hides the panels but their cached
            // field references survive, so workstation.Bind() / RebuildList() calls
            // succeed even while a panel is invisible. The selector and toolbar are
            // also managed by Switch() on the same frame.

            Selection.activeGameObject = ws;
            SceneManager.SetActiveScene(scene);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Scenarios/MedicalViz_Workstation.unity", false);

            Debug.Log(
                "[MedicalViz] Workstation scene built at Assets/Scenes/Scenarios/MedicalViz_Workstation.unity. " +
                "Add it to Build Settings (after Sonar_BoneDrilling). Place patient case folders into " +
                "Application.persistentDataPath/patients/ to populate the selector.");
        }

        static GameObject CreateUiPanel(GameObject parent, string name, VisualTreeAsset uxml, PanelSettings panelSettings, float sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;
            doc.sortingOrder = sortOrder;
            return go;
        }
    }
}
