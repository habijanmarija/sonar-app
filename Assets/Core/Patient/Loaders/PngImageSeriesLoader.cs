using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Host.Patients.Loaders
{
    /// Loads a series of pre-rendered slice images from a folder. Accepts PNG, JPG,
    /// and JPEG. Slices are sorted by filename (lex order, so use zero-padded names
    /// like slice_000.png ... slice_127.png). Each image is loaded into a Texture2D;
    /// the series wraps them as an ImageSeries.
    ///
    /// Useful for development / synthetic test fixtures, but also handles the JPG
    /// previews that ship alongside many clinical DICOM exports — letting you view
    /// real scans before fo-dicom integration lands.
    public sealed class PngImageSeriesLoader : IImageSeriesLoader
    {
        public string Format => "png_sequence";

        static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

        public ImageSeries Load(string directoryPath, string id, string modality)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError($"[ImageSeries] folder not found: {directoryPath}");
                return null;
            }

            var files = Directory.GetFiles(directoryPath)
                .Where(p => SupportedExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
            {
                Debug.LogWarning($"[ImageSeries] no .png/.jpg/.jpeg files in {directoryPath}");
                return null;
            }

            var series = new ImageSeries(id, modality);
            int loaded = 0;
            foreach (var file in files)
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
                    if (tex.LoadImage(data))
                    {
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        series.AddSlice(tex);
                        loaded++;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(tex);
                        Debug.LogWarning($"[ImageSeries] failed to decode {Path.GetFileName(file)}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ImageSeries] error reading {file}: {e.Message}");
                }
            }
            Debug.Log($"[ImageSeries] loaded {loaded}/{files.Length} slice(s) from {directoryPath}.");
            return loaded > 0 ? series : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => ImageSeriesLoaderRegistry.Register(new PngImageSeriesLoader());
    }
}
