using System;
using NUnit.Framework;
using Host.Registration;
using UnityEngine;

namespace Host.Registration.Tests
{
    public class PointSetRegistrationTests
    {
        static readonly Vector3[] ReferencePoints =
        {
            new Vector3(0.05f, 0.00f, 0.00f),
            new Vector3(0.00f, 0.04f, 0.00f),
            new Vector3(0.00f, 0.00f, 0.03f),
            new Vector3(-0.02f, 0.01f, 0.02f),
            new Vector3( 0.03f, -0.02f, 0.01f),
        };

        [Test]
        public void Identity_RmseIsZero()
        {
            var result = PointSetRegistration.Solve(ReferencePoints, ReferencePoints);

            Assert.That(result.RmseMeters, Is.LessThan(1e-5f));
            AssertQuaternionsClose(result.Rotation, Quaternion.identity, 1e-4f);
            AssertVector3Close(result.Translation, Vector3.zero, 1e-5f);
        }

        [Test]
        public void PureTranslation_IsRecovered()
        {
            var t = new Vector3(0.13f, -0.27f, 0.41f);
            var transformed = Apply(ReferencePoints, Quaternion.identity, t);

            var result = PointSetRegistration.Solve(ReferencePoints, transformed);

            Assert.That(result.RmseMeters, Is.LessThan(1e-5f));
            AssertVector3Close(result.Translation, t, 1e-5f);
            AssertQuaternionsClose(result.Rotation, Quaternion.identity, 1e-4f);
        }

        [Test]
        public void PureRotation_IsRecovered()
        {
            var r = Quaternion.Euler(15f, 47f, -28f);
            var transformed = Apply(ReferencePoints, r, Vector3.zero);

            var result = PointSetRegistration.Solve(ReferencePoints, transformed);

            Assert.That(result.RmseMeters, Is.LessThan(1e-5f));
            AssertVector3Close(result.Translation, Vector3.zero, 1e-4f);
            AssertQuaternionsClose(result.Rotation, r, 1e-3f);
        }

        [Test]
        public void RigidTransform_IsRecovered()
        {
            var r = Quaternion.Euler(33f, -11f, 70f);
            var t = new Vector3(-0.08f, 0.22f, 0.15f);
            var transformed = Apply(ReferencePoints, r, t);

            var result = PointSetRegistration.Solve(ReferencePoints, transformed);

            Assert.That(result.RmseMeters, Is.LessThan(1e-5f));
            AssertQuaternionsClose(result.Rotation, r, 1e-3f);
            AssertVector3Close(result.Translation, t, 1e-4f);

            for (int i = 0; i < ReferencePoints.Length; i++)
            {
                Vector3 fitted = result.ModelToWorld.MultiplyPoint3x4(ReferencePoints[i]);
                AssertVector3Close(fitted, transformed[i], 1e-4f);
            }
        }

        [Test]
        public void NoisyInput_RmseIsAboutNoiseLevel()
        {
            var r = Quaternion.Euler(20f, 60f, -10f);
            var t = new Vector3(0.10f, -0.05f, 0.25f);
            var clean = Apply(ReferencePoints, r, t);

            const float sigma = 0.001f;
            var noisy = new Vector3[clean.Length];
            var rng = new System.Random(1234);
            for (int i = 0; i < clean.Length; i++)
                noisy[i] = clean[i] + sigma * RandomGaussian3(rng);

            var result = PointSetRegistration.Solve(ReferencePoints, noisy);

            Assert.That(result.RmseMeters, Is.LessThan(3f * sigma));
            Assert.That(result.MeetsRmseGate(0.002f), Is.True,
                $"RMSE={result.RmseMeters} m did not meet 2 mm gate with σ=1 mm noise.");
            Assert.That(result.PerLandmarkResidualsMeters.Count, Is.EqualTo(ReferencePoints.Length));
        }

        [Test]
        public void TooFewPoints_Throws()
        {
            var a = new[] { Vector3.zero, Vector3.right };
            var b = new[] { Vector3.zero, Vector3.right };
            Assert.Throws<ArgumentException>(() => PointSetRegistration.Solve(a, b));
        }

        [Test]
        public void MismatchedLengths_Throws()
        {
            var a = new[] { Vector3.zero, Vector3.right, Vector3.up };
            var b = new[] { Vector3.zero, Vector3.right };
            Assert.Throws<ArgumentException>(() => PointSetRegistration.Solve(a, b));
        }

        static Vector3[] Apply(Vector3[] src, Quaternion r, Vector3 t)
        {
            var dst = new Vector3[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = r * src[i] + t;
            return dst;
        }

        static Vector3 RandomGaussian3(System.Random rng)
        {
            return new Vector3(NextGaussian(rng), NextGaussian(rng), NextGaussian(rng));
        }

        static float NextGaussian(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        static void AssertVector3Close(Vector3 actual, Vector3 expected, float eps)
        {
            Assert.That((actual - expected).magnitude, Is.LessThan(eps),
                $"expected {expected}, got {actual}");
        }

        static void AssertQuaternionsClose(Quaternion actual, Quaternion expected, float eps)
        {
            // Quaternions q and -q represent the same rotation.
            float dot = Mathf.Abs(Quaternion.Dot(actual.normalized, expected.normalized));
            Assert.That(1f - dot, Is.LessThan(eps),
                $"expected ~{expected}, got {actual} (|dot|={dot})");
        }
    }
}
