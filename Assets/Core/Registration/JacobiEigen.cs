using System;

namespace Host.Registration
{
    internal static class JacobiEigen
    {
        // Decompose a real symmetric matrix via cyclic Jacobi rotations.
        // On return, `eigenvalues[i]` is the eigenvalue whose eigenvector is the
        // i-th column of `eigenvectors` (i.e. eigenvectors[r, i] for row r).
        // The input is read-only — internal copy is taken.
        public static void Decompose(
            double[,] symmetric,
            int n,
            double[] eigenvalues,
            double[,] eigenvectors,
            int maxSweeps = 50,
            double convergenceEps = 1e-12)
        {
            if (symmetric == null) throw new ArgumentNullException(nameof(symmetric));
            if (eigenvalues == null || eigenvalues.Length != n)
                throw new ArgumentException("eigenvalues length must equal n", nameof(eigenvalues));
            if (eigenvectors == null || eigenvectors.GetLength(0) != n || eigenvectors.GetLength(1) != n)
                throw new ArgumentException("eigenvectors must be n x n", nameof(eigenvectors));

            var d = (double[,])symmetric.Clone();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    eigenvectors[i, j] = (i == j) ? 1.0 : 0.0;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double off = 0.0;
                for (int p = 0; p < n - 1; p++)
                    for (int q = p + 1; q < n; q++)
                        off += Math.Abs(d[p, q]);
                if (off < convergenceEps) break;

                for (int p = 0; p < n - 1; p++)
                {
                    for (int q = p + 1; q < n; q++)
                    {
                        double apq = d[p, q];
                        if (Math.Abs(apq) < convergenceEps * 1e-3) continue;

                        double app = d[p, p];
                        double aqq = d[q, q];

                        double theta = (aqq - app) / (2.0 * apq);
                        double t;
                        if (Math.Abs(theta) > 1e15)
                            t = 1.0 / (2.0 * theta);
                        else
                        {
                            double sign = theta >= 0 ? 1.0 : -1.0;
                            t = sign / (Math.Abs(theta) + Math.Sqrt(1.0 + theta * theta));
                        }
                        double c = 1.0 / Math.Sqrt(1.0 + t * t);
                        double s = t * c;

                        d[p, p] = app - t * apq;
                        d[q, q] = aqq + t * apq;
                        d[p, q] = 0.0;
                        d[q, p] = 0.0;

                        for (int r = 0; r < n; r++)
                        {
                            if (r == p || r == q) continue;
                            double drp = d[r, p];
                            double drq = d[r, q];
                            d[r, p] = c * drp - s * drq;
                            d[p, r] = d[r, p];
                            d[r, q] = s * drp + c * drq;
                            d[q, r] = d[r, q];
                        }

                        for (int r = 0; r < n; r++)
                        {
                            double vrp = eigenvectors[r, p];
                            double vrq = eigenvectors[r, q];
                            eigenvectors[r, p] = c * vrp - s * vrq;
                            eigenvectors[r, q] = s * vrp + c * vrq;
                        }
                    }
                }
            }

            for (int i = 0; i < n; i++) eigenvalues[i] = d[i, i];
        }
    }
}
