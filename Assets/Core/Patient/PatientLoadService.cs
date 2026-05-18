using System.IO;
using Host.Patients.Loaders;
using UnityEngine;

namespace Host.Patients
{
    /// Orchestrates the full load of a patient case from disk: meshes via the mesh loader
    /// registry, DICOM via DicomLoader. Materialises a Patient and activates it via
    /// PatientRegistry. Idempotent: calling Load twice with the same meta replaces the active.
    public static class PatientLoadService
    {
        public static Patient Load(PatientMeta meta)
        {
            if (meta == null) return null;
            var patient = new Patient(meta);

            if (meta.Meshes != null)
            {
                foreach (var meshRef in meta.Meshes)
                {
                    var loader = MeshLoaderRegistry.For(meshRef.Format);
                    if (loader == null)
                    {
                        Debug.LogWarning($"[Patient] no loader for format '{meshRef.Format}' ({meshRef.Id})");
                        continue;
                    }
                    var fullPath = Path.IsPathRooted(meshRef.Path)
                        ? meshRef.Path
                        : Path.Combine(meta.DataRoot, meshRef.Path);
                    var mesh = loader.Load(fullPath);
                    if (mesh == null) continue;

                    ColorUtility.TryParseHtmlString(meshRef.Color ?? "#CCCCCC", out var col);
                    patient.Meshes[meshRef.Id] = new OrganMesh
                    {
                        Id = meshRef.Id,
                        Name = meshRef.Name ?? meshRef.Id,
                        UnityMesh = mesh,
                        Color = col,
                        Opacity = Mathf.Clamp01(meshRef.Opacity),
                    };
                }
            }

            if (meta.DicomSeries != null)
            {
                foreach (var seriesRef in meta.DicomSeries)
                {
                    var seriesPath = Path.IsPathRooted(seriesRef.Path)
                        ? seriesRef.Path
                        : Path.Combine(meta.DataRoot, seriesRef.Path);

                    // Format-dispatched loader: "png_sequence" works today; "dicom" is
                    // a stub until Phase F brings fo-dicom integration.
                    DicomSeriesHandle handle;
                    var format = string.IsNullOrEmpty(seriesRef.Format) ? "dicom" : seriesRef.Format;
                    var imageLoader = ImageSeriesLoaderRegistry.For(format);
                    if (imageLoader != null)
                    {
                        handle = imageLoader.Load(seriesPath, seriesRef.Id, seriesRef.Modality);
                    }
                    else
                    {
                        Debug.LogWarning($"[Patient] no image-series loader for format '{format}' ({seriesRef.Id}); falling back to DicomLoader stub.");
                        handle = DicomLoader.Load(seriesPath, seriesRef.Id, seriesRef.Modality);
                    }
                    if (handle != null)
                        patient.Series[seriesRef.Id] = handle;
                }
            }

            PatientRegistry.Activate(patient);
            return patient;
        }
    }
}
