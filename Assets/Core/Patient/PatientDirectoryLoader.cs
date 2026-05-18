using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Patients
{
    /// Scans a root directory for patient case folders. Each patient is one subfolder
    /// containing a `manifest.json` matching the PatientMeta JSON schema, plus the
    /// referenced mesh/DICOM files.
    ///
    /// Default search root: `Application.persistentDataPath/patients/`.
    /// Override via Configure() before Scan().
    public static class PatientDirectoryLoader
    {
        public const string ManifestFilename = "manifest.json";

        static string _root;

        public static string Root => _root ??= Path.Combine(Application.persistentDataPath, "patients");

        public static void Configure(string root)
        {
            _root = root;
        }

        /// Scan the root and return a list of PatientMeta (one per valid manifest).
        /// Creates the root directory if missing (helpful for first-run UX).
        /// Invalid manifests are logged and skipped.
        public static IReadOnlyList<PatientMeta> Scan()
        {
            var results = new List<PatientMeta>();

            // Ensure the root exists. Helpful for first-run + sidesteps any edge cases
            // where Directory.Exists reports false right after the user created the path.
            try
            {
                Directory.CreateDirectory(Root);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Patient] cannot create root '{Root}': {e.Message}");
                return results;
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(Root);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Patient] cannot list '{Root}': {e.Message}");
                return results;
            }

            Debug.Log($"[Patient] scanning '{Root}' — found {subDirs.Length} subdirectorie(s).");
            foreach (var dir in subDirs)
            {
                var manifestPath = Path.Combine(dir, ManifestFilename);
                if (!File.Exists(manifestPath))
                {
                    Debug.Log($"[Patient] skipped '{dir}' — no manifest.json.");
                    continue;
                }
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var meta = JsonConvert.DeserializeObject<PatientMeta>(json);
                    if (meta == null)
                    {
                        Debug.LogWarning($"[Patient] '{manifestPath}' parsed to null.");
                        continue;
                    }
                    if (string.IsNullOrEmpty(meta.DataRoot) || !Path.IsPathRooted(meta.DataRoot))
                        meta.DataRoot = dir;
                    results.Add(meta);
                    Debug.Log($"[Patient] loaded meta for '{meta.PatientId}' from '{manifestPath}'.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Patient] failed to parse {manifestPath}: {e.Message}");
                }
            }
            return results.OrderBy(m => m.PatientId, StringComparer.Ordinal).ToList();
        }
    }
}
