using System;
using System.Collections.Generic;
using UnityEngine;

namespace Host.Registration
{
    public sealed class RegistrationResult
    {
        public Matrix4x4 ModelToWorld { get; }
        public Quaternion Rotation { get; }
        public Vector3 Translation { get; }
        public float RmseMeters { get; }
        public IReadOnlyList<float> PerLandmarkResidualsMeters { get; }

        public int LandmarkCount => PerLandmarkResidualsMeters.Count;
        public float MaxResidualMeters
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < PerLandmarkResidualsMeters.Count; i++)
                    if (PerLandmarkResidualsMeters[i] > max) max = PerLandmarkResidualsMeters[i];
                return max;
            }
        }

        public RegistrationResult(
            Matrix4x4 modelToWorld,
            Quaternion rotation,
            Vector3 translation,
            float rmseMeters,
            float[] residuals)
        {
            ModelToWorld = modelToWorld;
            Rotation = rotation;
            Translation = translation;
            RmseMeters = rmseMeters;
            PerLandmarkResidualsMeters = Array.AsReadOnly(residuals ?? Array.Empty<float>());
        }

        public bool MeetsRmseGate(float thresholdMeters) => RmseMeters <= thresholdMeters;
    }
}
