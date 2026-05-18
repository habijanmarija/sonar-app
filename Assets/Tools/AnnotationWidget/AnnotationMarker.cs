using UnityEngine;

namespace Host.Tools.AnnotationWidget
{
    /// 3D visual for one annotation: a small sphere at the annotation's position plus
    /// a billboarded TextMesh label above it. Lives under the patient content root so
    /// it follows any future patient-coordinate transformations.
    [AddComponentMenu("SONAR Host/Tools/Annotation Marker")]
    public sealed class AnnotationMarker : MonoBehaviour
    {
        public Annotation Annotation { get; private set; }

        GameObject _sphere;
        TextMesh _label;
        Material _material;
        Camera _cameraCache;

        public static AnnotationMarker Create(Annotation annotation, Transform parent)
        {
            var go = new GameObject($"Annotation_{annotation.Id}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = annotation.Position;

            var marker = go.AddComponent<AnnotationMarker>();
            marker.Bind(annotation);
            return marker;
        }

        void Bind(Annotation annotation)
        {
            Annotation = annotation;

            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "Sphere";
            _sphere.transform.SetParent(transform, worldPositionStays: false);
            _sphere.transform.localScale = Vector3.one * 0.008f;
            DestroyImmediate(_sphere.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { color = annotation.Color };
            _sphere.GetComponent<MeshRenderer>().sharedMaterial = _material;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, worldPositionStays: false);
            labelGo.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            _label = labelGo.AddComponent<TextMesh>();
            _label.text = annotation.Label;
            _label.fontSize = 64;
            _label.characterSize = 0.0015f;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = annotation.Color;
        }

        void LateUpdate()
        {
            if (_label == null) return;
            if (_cameraCache == null) _cameraCache = Camera.main;
            if (_cameraCache == null) return;
            _label.transform.rotation = Quaternion.LookRotation(
                _label.transform.position - _cameraCache.transform.position);
        }

        public void UpdateLabel(string text)
        {
            if (Annotation != null) Annotation.Label = text;
            if (_label != null) _label.text = text;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
