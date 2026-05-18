using System;
using UnityEngine;

namespace Host.Registration
{
    public static class PointSetRegistration
    {
        // Closed-form rigid SE(3) fit minimising sum of squared distances between
        // R * modelPoints[i] + t and worldPoints[i]. Implements Horn 1987's
        // quaternion eigenvalue method on the 4x4 N matrix derived from the
        // cross-covariance of centered points — no SVD required.
        //
        // References: Horn, "Closed-form solution of absolute orientation using
        // unit quaternions", JOSA A 4(4):629–642, 1987. Liebmann et al. and Eckert
        // et al. cite this approach for AR-guided pedicle screw landmark
        // registration (see [[ar-training-research]] A1.1 §2.5/2.6).
        public static RegistrationResult Solve(Vector3[] modelPoints, Vector3[] worldPoints)
        {
            if (modelPoints == null) throw new ArgumentNullException(nameof(modelPoints));
            if (worldPoints == null) throw new ArgumentNullException(nameof(worldPoints));
            if (modelPoints.Length != worldPoints.Length)
                throw new ArgumentException("Point arrays must have equal length.");
            if (modelPoints.Length < 3)
                throw new ArgumentException("At least 3 correspondences required for rigid 3D fit.");

            int n = modelPoints.Length;

            Vector3 cp = Vector3.zero, cq = Vector3.zero;
            for (int i = 0; i < n; i++) { cp += modelPoints[i]; cq += worldPoints[i]; }
            cp /= n;
            cq /= n;

            double Mxx = 0, Mxy = 0, Mxz = 0;
            double Myx = 0, Myy = 0, Myz = 0;
            double Mzx = 0, Mzy = 0, Mzz = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = modelPoints[i] - cp;
                Vector3 q = worldPoints[i] - cq;
                Mxx += p.x * q.x; Mxy += p.x * q.y; Mxz += p.x * q.z;
                Myx += p.y * q.x; Myy += p.y * q.y; Myz += p.y * q.z;
                Mzx += p.z * q.x; Mzy += p.z * q.y; Mzz += p.z * q.z;
            }

            var N = new double[4, 4];
            N[0, 0] = Mxx + Myy + Mzz;
            N[0, 1] = Myz - Mzy;
            N[0, 2] = Mzx - Mxz;
            N[0, 3] = Mxy - Myx;
            N[1, 0] = N[0, 1]; N[1, 1] = Mxx - Myy - Mzz; N[1, 2] = Mxy + Myx; N[1, 3] = Mzx + Mxz;
            N[2, 0] = N[0, 2]; N[2, 1] = N[1, 2]; N[2, 2] = -Mxx + Myy - Mzz; N[2, 3] = Myz + Mzy;
            N[3, 0] = N[0, 3]; N[3, 1] = N[1, 3]; N[3, 2] = N[2, 3]; N[3, 3] = -Mxx - Myy + Mzz;

            var eigenvalues = new double[4];
            var eigenvectors = new double[4, 4];
            JacobiEigen.Decompose(N, 4, eigenvalues, eigenvectors);

            int largest = 0;
            for (int i = 1; i < 4; i++)
                if (eigenvalues[i] > eigenvalues[largest]) largest = i;

            double qw = eigenvectors[0, largest];
            double qx = eigenvectors[1, largest];
            double qy = eigenvectors[2, largest];
            double qz = eigenvectors[3, largest];
            double norm = Math.Sqrt(qw * qw + qx * qx + qy * qy + qz * qz);
            if (norm < 1e-12)
            {
                qw = 1.0; qx = qy = qz = 0.0;
            }
            else
            {
                qw /= norm; qx /= norm; qy /= norm; qz /= norm;
            }

            var rotation = new Quaternion((float)qx, (float)qy, (float)qz, (float)qw);
            Vector3 translation = cq - rotation * cp;
            Matrix4x4 transform = Matrix4x4.TRS(translation, rotation, Vector3.one);

            var residuals = new float[n];
            double sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 fitted = transform.MultiplyPoint3x4(modelPoints[i]);
                float sqDist = (worldPoints[i] - fitted).sqrMagnitude;
                residuals[i] = Mathf.Sqrt(sqDist);
                sumSq += sqDist;
            }
            float rmse = (float)Math.Sqrt(sumSq / n);

            return new RegistrationResult(transform, rotation, translation, rmse, residuals);
        }
    }
}
