using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Diffraction.Core
{
    public static class DiffractionMath
    {
        // Класс для представления комплексных чисел
        public class Compl
        {
            public double Re;
            public double Im;

            public Compl() { Re = 0; Im = 0; }
            public Compl(double x) { Re = x; Im = 0; }
            public Compl(double x, double y) { Re = x; Im = y; }

            public static Compl operator +(Compl x1, Compl x2) => new Compl(x1.Re + x2.Re, x1.Im + x2.Im);
            public static Compl operator -(Compl x) => new Compl(-x.Re, -x.Im);
            public static Compl operator -(Compl x1, Compl x2) => new Compl(x1.Re - x2.Re, x1.Im - x2.Im);
            public static Compl operator *(Compl x1, Compl x2) => new Compl(x1.Re * x2.Re - x1.Im * x2.Im, x1.Re * x2.Im + x1.Im * x2.Re);

            public static Compl operator /(Compl x1, Compl x2)
            {
                double y = x2.Re * x2.Re + x2.Im * x2.Im;
                if (Math.Abs(y) < 1e-15) throw new DivideByZeroException("Division by zero complex number");
                return new Compl((x1.Re * x2.Re + x1.Im * x2.Im) / y, (x2.Re * x1.Im - x2.Im * x1.Re) / y);
            }

            public static Compl operator *(Compl x1, double x2) => new Compl(x2 * x1.Re, x2 * x1.Im);
            public static Compl operator /(Compl x1, double x2)
            {
                if (Math.Abs(x2) < 1e-15) throw new DivideByZeroException("Division by zero");
                return new Compl(x1.Re / x2, x1.Im / x2);
            }
            public static Compl operator *(double x, Compl y) => new Compl(x * y.Re, x * y.Im);
            public static Compl operator /(double x, Compl y)
            {
                double r = y.Re * y.Re + y.Im * y.Im;
                if (Math.Abs(r) < 1e-15) throw new DivideByZeroException("Division by zero complex number");
                return new Compl(x * y.Re / r, -x * y.Im / r);
            }
            public static Compl operator +(Compl x, double y) => new Compl(x.Re + y, x.Im);
            public static Compl operator +(double x, Compl y) => new Compl(x + y.Re, y.Im);
            public static Compl operator -(double x, Compl y) => new Compl(x - y.Re, -y.Im);
            public static Compl operator -(Compl x, double y) => new Compl(x.Re - y, x.Im);

            public static Compl Exp(Compl x)
            {
                Compl z = new Compl(Math.Exp(x.Re), 0);
                Compl y = new Compl(Math.Cos(x.Im), Math.Sin(x.Im));
                return z * y;
            }

            public static Compl Log(Compl x) => new Compl(Math.Log(Abs(x)), Argum(x));

            public static Compl Pow(Compl x, double n)
            {
                double r = Math.Pow(x.Re * x.Re + x.Im * x.Im, n / 2);
                double a = Math.Atan2(x.Im, x.Re);
                return new Compl(r * Math.Cos(a * n), r * Math.Sin(a * n));
            }

            public static Compl Pow(Compl x, Compl y) => Exp(y * Log(x));

            public static double Abs(Compl x) => Math.Sqrt(x.Re * x.Re + x.Im * x.Im);
            public static double Argum(Compl x) => Math.Atan2(x.Im, x.Re);
        }

        public static readonly Compl ci = new Compl(0, 1);

        public class CVect
        {
            private Compl[] v;
            private int sz;

            public CVect(int size)
            {
                sz = size;
                v = new Compl[sz];
                for (int i = 0; i < sz; i++) v[i] = new Compl(0, 0);
            }
            ~CVect() { v = null; }
            public int Size() => sz;

            public Compl this[int index]
            {
                get
                {
                    if (index < 0 || index >= sz) throw new IndexOutOfRangeException($"CVect index {index} out of range [0, {sz - 1}]");
                    return v[index];
                }
                set
                {
                    if (index < 0 || index >= sz) throw new IndexOutOfRangeException($"CVect index {index} out of range [0, {sz - 1}]");
                    v[index] = value;
                }
            }
        }

        public class CMatr
        {
            private CVect[] v;
            private int sz;

            public CMatr(int size)
            {
                sz = size;
                v = new CVect[sz];
                for (int i = 0; i < sz; i++) v[i] = new CVect(sz);
            }
            ~CMatr() { v = null; }
            public int Size() => sz;

            public CVect this[int index]
            {
                get
                {
                    if (index < 0 || index >= sz) throw new IndexOutOfRangeException($"CMatr index {index} out of range [0, {sz - 1}]");
                    return v[index];
                }
                set
                {
                    if (index < 0 || index >= sz) throw new IndexOutOfRangeException($"CMatr index {index} out of range [0, {sz - 1}]");
                    v[index] = value;
                }
            }
        }

        public static int Gauss(CMatr A, CVect b, CVect x, CancellationToken cancellationToken = default(CancellationToken))
        {
            Compl s, s1;
            double max, ss;
            int maxN;
            int N = b.Size();

            for (int i = 0; i < N - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                max = Compl.Abs(A[i][i]);
                maxN = i;
                for (int k = i + 1; k < N; k++)
                {
                    ss = Compl.Abs(A[k][i]);
                    if (ss > max) { max = ss; maxN = k; }
                }
                if (maxN != i)
                {
                    for (int k = 0; k < N; k++) { s1 = A[i][k]; A[i][k] = A[maxN][k]; A[maxN][k] = s1; }
                    s1 = b[i]; b[i] = b[maxN]; b[maxN] = s1;
                }
                if (Compl.Abs(A[i][i]) < 1e-12) return -1;
                s = 1 / A[i][i];
                for (int j = i + 1; j < N; j++)
                {
                    s1 = A[j][i] * s;
                    for (int k = i + 1; k < N; k++) A[j][k] = A[j][k] - s1 * A[i][k];
                    b[j] = b[j] - b[i] * s1;
                }
            }
            if (Compl.Abs(A[N - 1][N - 1]) < 1e-12) return -1;
            x[N - 1] = b[N - 1] / A[N - 1][N - 1];
            for (int i = N - 2; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                s = b[i];
                for (int j = N - 1; j > i; j--) s = s - A[i][j] * x[j];
                x[i] = s / A[i][i];
            }
            return 1;
        }

        public static double Cheb(int n, double x)
        {
            if (n == 0) return 1.0;
            if (n == 1) return x;
            double T0 = 1.0, T1 = x, T = 0;
            for (int i = 2; i <= n; i++) { T = 2 * x * T1 - T0; T0 = T1; T1 = T; }
            return T;
        }

        public static double J0(double x)
        {
            double x_half_sq = x * x / 4.0, sum = 1.0, term = 1.0;
            for (int k = 1; k <= 100; k++)
            {
                term *= -x_half_sq / ((double)k * k);
                sum += term;
                if (Math.Abs(term) < 1e-15) break;
            }
            return sum;
        }

        public static double _Y0(double x)
        {
            const double gamma = 0.5772156649015329;
            double j0 = J0(x), x_half_sq = x * x / 4.0, sum = 0, H_k = 0, factorial_k_sq = 1.0, x_pow = 1.0;
            for (int k = 1; k <= 100; k++)
            {
                factorial_k_sq *= (double)k * k;
                x_pow *= x_half_sq;
                H_k += 1.0 / k;
                double sign = (k % 2 == 1) ? 1.0 : -1.0;
                double term = sign * x_pow / factorial_k_sq * H_k;
                sum += term;
                if (Math.Abs(term) < 1e-15) break;
            }
            return gamma * j0 + sum;
        }

        public static double N0(double x) => 2.0 / Math.PI * (J0(x) * Math.Log(x / 2) + _Y0(x));

        public static double J1(double x)
        {
            if (Math.Abs(x) < 1e-10) return 0;
            double x_half = x / 2.0, x_half_sq = x_half * x_half, sum = x_half, term = x_half;
            for (int k = 1; k <= 100; k++)
            {
                term *= -x_half_sq / ((double)k * (k + 1));
                sum += term;
                if (Math.Abs(term) < 1e-15) break;
            }
            return sum;
        }

        public static double _Y1(double x)
        {
            double x_half = x / 2.0, x_half_sq = x_half * x_half, Hk = 0.0, xPow = x_half, factK = 1.0, factK1 = 1.0, sum = -1.0 / x;
            for (int k = 0; k <= 100; k++)
            {
                if (k > 0) { factK *= k; factK1 *= (k + 1); xPow *= -x_half_sq; Hk += 1.0 / k; }
                double Hk1 = Hk + 1.0 / (k + 1);
                double term = xPow / (factK * factK1) * (Hk + Hk1);
                sum += term;
                if (k > 0 && Math.Abs(term) < 1e-15) break;
            }
            return sum;
        }

        public static double N1(double x)
        {
            if (Math.Abs(x) < 1e-10) return double.NegativeInfinity;
            return 2.0 / Math.PI * (J1(x) * Math.Log(x / 2.0) + _Y1(x));
        }

        public static Compl H0_1(double x) => N0(x) * ci + J0(x);
        public static Compl H0_2(double x) => J0(x) - N0(x) * ci;

        public static Compl R_H0(double z)
        {
            const double gamma = 0.5772156649015329;
            if (z < 1e-12) return new Compl(1.0, -2.0 * gamma / Math.PI);
            double j0 = J0(z), lnz2 = Math.Log(z / 2.0), y0reg = _Y0(z);
            double re = j0, im = (2.0 / Math.PI) * lnz2 * (1.0 - j0) - (2.0 / Math.PI) * y0reg;
            return new Compl(re, im);
        }

        public static Compl H1_2(double x) => J1(x) - N1(x) * ci;

        // ===== Методы для расчёта числа обусловленности =====
        public static CMatr Inverse(CMatr A)
        {
            int N = A.Size();
            CMatr inv = new CMatr(N);
            for (int i = 0; i < N; i++)
            {
                CMatr tempA = new CMatr(N);
                for (int r = 0; r < N; r++)
                    for (int c = 0; c < N; c++)
                        tempA[r][c] = A[r][c];
                CVect e = new CVect(N);
                e[i] = new Compl(1, 0);
                CVect x = new CVect(N);
                if (Gauss(tempA, e, x) == -1) return null;
                for (int r = 0; r < N; r++) inv[r][i] = x[r];
            }
            return inv;
        }

        public static double MatrixNormInf(CMatr A)
        {
            int N = A.Size();
            double maxNorm = 0;
            for (int i = 0; i < N; i++)
            {
                double sum = 0;
                for (int j = 0; j < N; j++) sum += Compl.Abs(A[i][j]);
                if (sum > maxNorm) maxNorm = sum;
            }
            return maxNorm;
        }

        public static double ConditionNumber(CMatr A)
        {
            if (A == null) return double.PositiveInfinity;
            CMatr inv = Inverse(A);
            if (inv == null) return double.PositiveInfinity;
            return MatrixNormInf(A) * MatrixNormInf(inv);
        }
        // ===== Конец методов обусловленности =====

        public class SolvePerformance
        {
            public string BackendName;
            public double AssemblyMilliseconds;
            public double LinearSolveMilliseconds;
            public double TotalMilliseconds;
            public bool UsedCuda;
        }

        public class DifrOnLenta
        {
            public double a, b;
            public double lambda;
            public int N;
            public double teta;
            public Compl[] y;
            public double skinDepth;
            public Compl chi;
            public CMatr LastMatrixA; // Сохранение матрицы для расчёта обусловленности
            public int PlateCount { get; private set; }
            public double[] alpha;
            public double[] beta;
            public SolvePerformance LastSolvePerformance { get; private set; }
            public bool LastSolveCancelled { get; private set; }
            public int TotalUnknowns => N * PlateCount;

            public DifrOnLenta(double _a, double _b, double _lambda, double _teta, int _N, double _skinDepth = 0)
            {
                Initialize(new double[] { _a }, new double[] { _b }, _lambda, _teta, _N, _skinDepth);
            }

            public DifrOnLenta(double _alpha1, double _beta1, double _alpha2, double _beta2, double _lambda, double _teta, int _N, double _skinDepth = 0)
            {
                Initialize(new double[] { _alpha1, _alpha2 }, new double[] { _beta1, _beta2 }, _lambda, _teta, _N, _skinDepth);
            }
            ~DifrOnLenta() { y = null; }

            private void Initialize(double[] _alpha, double[] _beta, double _lambda, double _teta, int _N, double _skinDepth)
            {
                if (_alpha == null || _beta == null || _alpha.Length == 0 || _alpha.Length != _beta.Length)
                    throw new ArgumentException("Некорректный набор пластин");
                if (_N <= 0) throw new ArgumentException("Параметр N должен быть положительным");
                if (_lambda <= 0) throw new ArgumentException("Длина волны должна быть положительной");
                if (_skinDepth < 0) throw new ArgumentException("Толщина скин-слоя не может быть отрицательной");

                PlateCount = _alpha.Length;
                alpha = new double[PlateCount];
                beta = new double[PlateCount];

                for (int p = 0; p < PlateCount; p++)
                {
                    if (_alpha[p] >= _beta[p])
                        throw new ArgumentException(string.Format("Для пластины {0} должно выполняться alpha < beta", p + 1));
                    alpha[p] = _alpha[p];
                    beta[p] = _beta[p];
                }

                for (int p = 0; p < PlateCount; p++)
                {
                    for (int q = p + 1; q < PlateCount; q++)
                    {
                        if (Math.Max(alpha[p], alpha[q]) < Math.Min(beta[p], beta[q]))
                            throw new ArgumentException("Пластины не должны накладываться друг на друга");
                    }
                }

                a = alpha[0];
                b = beta[0];
                for (int p = 1; p < PlateCount; p++)
                {
                    if (alpha[p] < a) a = alpha[p];
                    if (beta[p] > b) b = beta[p];
                }

                N = _N; lambda = _lambda; teta = _teta;
                y = new Compl[N * PlateCount];
                for (int i = 0; i < y.Length; i++) y[i] = new Compl(0, 0);
                skinDepth = _skinDepth;
                ResetPreparedState();
                chi = CalculateChi();
                LastSolvePerformance = null;
                LastSolveCancelled = false;
            }

            private Compl CalculateChi()
            {
                if (skinDepth <= 0) return new Compl(0, 0);
                double k = 2 * Math.PI / lambda;
                return new Compl(k * skinDepth, k * skinDepth);
            }

            public double ChebAB(int n, double x)
            {
                int plateIndex = GetPlateIndex(x);
                if (plateIndex < 0) plateIndex = 0;
                return ChebOnPlate(plateIndex, n, x);
            }

            public double CalculateConductivity(double skinDepth, double wavelength)
            {
                if (skinDepth <= 0) throw new ArgumentException("Толщина скин-слоя должна быть положительной");
                const double mu0 = 4 * Math.PI * 1e-7, c = 299792458;
                double frequency = c / wavelength;
                return 1.0 / (Math.PI * mu0 * frequency * skinDepth * skinDepth);
            }

            public Compl dr_dn(double t, double x)
            {
                double k = 2 * Math.PI / lambda;
                double dist = Math.Abs(t - x);
                if (dist < 1e-10) return new Compl(0, 0);
                Compl H1 = H1_2(k * dist);
                return ci / 4.0 * k * H1;
            }

            public Compl r(double t, double x)
            {
                double k = 2 * Math.PI / lambda;
                double diff = Math.Abs(t - x);
                Compl g;
                if (diff < 1e-12)
                    g = -(Math.PI * ci / 2.0 + Math.Log(k / 2.0) + 0.57721566);
                else
                {
                    double kd = k * diff;
                    g = -(Math.PI * ci / 2.0 * J0(kd) + (J0(kd) - 1.0) * Math.Log(kd / 2.0) + Math.Log(k / 2.0) + _Y0(kd));
                }
                Compl dg = dr_dn(t, x);
                return g + chi * dg;
            }

            public Compl u0(double x, double z)
            {
                double k = 2 * Math.PI / lambda;
                return Compl.Exp(k * Math.Cos(teta) * ci * x + k * Math.Sin(teta) * ci * z);
            }

            public Compl u(double x, double z)
            {
                if (Math.Abs(z) < 1e-12 && IsOnAnyPlate(x)) return u_on_strip(x);
                double k_wave = 2 * Math.PI / lambda;
                Compl s = new Compl(0, 0);
                for (int p = 0; p < PlateCount; p++)
                {
                    for (int m = 0; m < M_quad; m++)
                    {
                        Compl phi = PhiAtQuadrature(p, m);
                        double distance = Math.Sqrt(z * z + (t_q[p][m] - x) * (t_q[p][m] - x));
                        if (distance < 1e-14) distance = 1e-14;
                        Compl H = H0_2(k_wave * distance);
                        s += phi * H * w_q[p][m];
                    }
                }
                return s * ci / 4.0 + u0(x, z);
            }

            public Compl u_on_strip(double x)
            {
                int targetPlate = GetPlateIndex(x);
                if (targetPlate < 0) return u(x, lambda * 1e-10);

                double halfL = HalfLength(targetPlate), k_wave = 2 * Math.PI / lambda;
                Compl sum_reg = new Compl(0, 0);
                for (int m = 0; m < M_quad; m++)
                {
                    Compl phi = PhiAtQuadrature(targetPlate, m);
                    double kd = k_wave * Math.Abs(t_q[targetPlate][m] - x);
                    Compl R = R_H0(kd);
                    sum_reg += phi * R * w_q[targetPlate][m];
                }
                double xi = XToTau(targetPlate, x);
                double ln_const = Math.Log(k_wave * halfL / 2.0);
                Compl sum_log = new Compl(0, 0);
                for (int j = 0; j < N; j++)
                {
                    double I_ortho = (j == 0) ? Math.PI : 0.0;
                    double I_log = (j == 0) ? (-Math.PI * Math.Log(2.0)) : (-(Math.PI / j) * Cheb(j, xi));
                    double S = ln_const * I_ortho + I_log;
                    sum_log += y[CoeffIndex(targetPlate, j)] * (-2.0 / Math.PI) * halfL * S;
                }

                Compl sum_cross = new Compl(0, 0);
                for (int sourcePlate = 0; sourcePlate < PlateCount; sourcePlate++)
                {
                    if (sourcePlate == targetPlate) continue;
                    for (int m = 0; m < M_quad; m++)
                    {
                        Compl phi = PhiAtQuadrature(sourcePlate, m);
                        double kd = k_wave * Math.Abs(t_q[sourcePlate][m] - x);
                        sum_cross += phi * H0_2(kd) * w_q[sourcePlate][m];
                    }
                }

                return (sum_reg + ci * sum_log + sum_cross) * ci / 4.0 + u0(x, 0);
            }

            public Compl f(double x) => -2 * Math.PI * u0(x, 0);

            private double[][] tau_q, t_q, w_q;
            private int M_quad;
            private double[][] tau_c, x_c;
            private bool useSingularWeight;

            private void ResetPreparedState()
            {
                M_quad = 0;
                tau_q = null;
                t_q = null;
                w_q = null;
                tau_c = null;
                x_c = null;
                useSingularWeight = true;
            }

            private void EnsurePreparedState()
            {
                if (M_quad > 0 &&
                    tau_q != null &&
                    t_q != null &&
                    w_q != null &&
                    tau_c != null &&
                    x_c != null)
                    return;

                useSingularWeight = true;
                M_quad = Math.Max(8 * N, 80);

                tau_q = new double[PlateCount][];
                t_q = new double[PlateCount][];
                w_q = new double[PlateCount][];
                tau_c = new double[PlateCount][];
                x_c = new double[PlateCount][];

                for (int p = 0; p < PlateCount; p++)
                {
                    double halfL = HalfLength(p);
                    tau_q[p] = new double[M_quad];
                    t_q[p] = new double[M_quad];
                    w_q[p] = new double[M_quad];
                    for (int m = 0; m < M_quad; m++)
                    {
                        tau_q[p][m] = Math.Cos((2.0 * m + 1.0) / (2.0 * M_quad) * Math.PI);
                        t_q[p][m] = TauToX(p, tau_q[p][m]);
                        w_q[p][m] = Math.PI / M_quad * halfL;
                    }

                    tau_c[p] = new double[N];
                    x_c[p] = new double[N];
                    for (int ik = 0; ik < N; ik++)
                    {
                        tau_c[p][ik] = Math.Cos((ik + 0.5) / N * Math.PI);
                        x_c[p][ik] = TauToX(p, tau_c[p][ik]);
                    }
                }
            }

            public void ApplySolvedCoefficients(
                Compl[] coefficients,
                string backendName,
                double assemblyMilliseconds,
                double linearSolveMilliseconds,
                double totalMilliseconds,
                bool usedCuda)
            {
                if (coefficients == null)
                    throw new ArgumentNullException(nameof(coefficients));
                if (coefficients.Length != TotalUnknowns)
                    throw new ArgumentException("Некорректная длина массива коэффициентов", nameof(coefficients));

                EnsurePreparedState();

                for (int i = 0; i < coefficients.Length; i++)
                    y[i] = new Compl(coefficients[i].Re, coefficients[i].Im);

                LastMatrixA = null;
                LastSolvePerformance = new SolvePerformance
                {
                    BackendName = backendName,
                    AssemblyMilliseconds = assemblyMilliseconds,
                    LinearSolveMilliseconds = linearSolveMilliseconds,
                    TotalMilliseconds = totalMilliseconds,
                    UsedCuda = usedCuda
                };
                LastSolveCancelled = false;
            }

            private int CoeffIndex(int plateIndex, int localIndex) => plateIndex * N + localIndex;
            private double HalfLength(int plateIndex) => (beta[plateIndex] - alpha[plateIndex]) / 2.0;
            private double Midpoint(int plateIndex) => (beta[plateIndex] + alpha[plateIndex]) / 2.0;
            private double XToTau(int plateIndex, double x) => (x - Midpoint(plateIndex)) / HalfLength(plateIndex);
            private double TauToX(int plateIndex, double tau) => HalfLength(plateIndex) * tau + Midpoint(plateIndex);
            private double ChebOnPlate(int plateIndex, int n, double x) => Cheb(n, XToTau(plateIndex, x));

            private bool IsOnAnyPlate(double x) => GetPlateIndex(x) >= 0;

            private int GetPlateIndex(double x)
            {
                const double eps = 1e-12;
                for (int p = 0; p < PlateCount; p++)
                    if (x >= alpha[p] - eps && x <= beta[p] + eps)
                        return p;
                return -1;
            }

            private Compl PhiAtQuadrature(int plateIndex, int quadIndex)
            {
                Compl phi = new Compl(0, 0);
                for (int j = 0; j < N; j++)
                    phi += y[CoeffIndex(plateIndex, j)] * Cheb(j, tau_q[plateIndex][quadIndex]);
                return phi;
            }

            private static void GaussLegendre(int n, out double[] nodes, out double[] weights)
            {
                nodes = new double[n]; weights = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), z1, pp;
                    do
                    {
                        double p1 = 1, p2 = 0;
                        for (int j = 0; j < n; j++) { double p3 = p2; p2 = p1; p1 = ((2.0 * j + 1) * z * p2 - j * p3) / (j + 1); }
                        pp = n * (z * p1 - p2) / (z * z - 1); z1 = z; z = z1 - p1 / pp;
                    } while (Math.Abs(z - z1) > 1e-14);
                    nodes[i] = z; weights[i] = 2.0 / ((1 - z * z) * pp * pp);
                }
            }

            public int SolveDifr(CancellationToken cancellationToken = default(CancellationToken))
            {
                try
                {
                    LastSolveCancelled = false;
                    Stopwatch totalWatch = Stopwatch.StartNew();
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsurePreparedState();

                    double k_wave = 2 * Math.PI / lambda;
                    int totalUnknowns = TotalUnknowns;
                    CMatr A_mat = new CMatr(totalUnknowns);
                    CVect B_vec = new CVect(totalUnknowns);
                    Stopwatch assemblyWatch = Stopwatch.StartNew();

                    Parallel.For(0, totalUnknowns, new ParallelOptions { CancellationToken = cancellationToken }, row =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int targetPlate = row / N;
                        int ik = row % N;
                        double targetHalfL = HalfLength(targetPlate);
                        double xk = x_c[targetPlate][ik], tau_k = tau_c[targetPlate][ik];

                        for (int sourcePlate = 0; sourcePlate < PlateCount; sourcePlate++)
                        {
                            for (int j = 0; j < N; j++)
                            {
                                int col = CoeffIndex(sourcePlate, j);

                                if (sourcePlate == targetPlate)
                                {
                                    Compl sum_reg = new Compl(0, 0);
                                    for (int m = 0; m < M_quad; m++)
                                    {
                                        double kd = k_wave * targetHalfL * Math.Abs(tau_k - tau_q[targetPlate][m]);
                                        Compl R = R_H0(kd);
                                        double Tj = Cheb(j, tau_q[targetPlate][m]);
                                        sum_reg += R * Tj * w_q[targetPlate][m];
                                    }
                                    double ln_const = Math.Log(k_wave * targetHalfL / 2.0);
                                    double I_ortho = (j == 0) ? Math.PI : 0.0;
                                    double I_log = (j == 0) ? (-Math.PI * Math.Log(2.0)) : (-(Math.PI / j) * Cheb(j, tau_k));
                                    Compl S_log = ci * (-2.0 / Math.PI) * targetHalfL * (ln_const * I_ortho + I_log);
                                    A_mat[row][col] = ci / 4.0 * (sum_reg + S_log);

                                    if (skinDepth > 0)
                                    {
                                        double Tj_k = Cheb(j, tau_c[targetPlate][ik]);
                                        double sqrt_w = Math.Sqrt(1.0 - tau_c[targetPlate][ik] * tau_c[targetPlate][ik]);
                                        A_mat[row][col] = A_mat[row][col] - chi / (2.0 * targetHalfL) * Tj_k / sqrt_w;
                                    }
                                }
                                else
                                {
                                    Compl sum_cross = new Compl(0, 0);
                                    for (int m = 0; m < M_quad; m++)
                                    {
                                        double distance = Math.Abs(t_q[sourcePlate][m] - xk);
                                        if (distance < 1e-14) distance = 1e-14;
                                        double Tj = Cheb(j, tau_q[sourcePlate][m]);
                                        sum_cross += H0_2(k_wave * distance) * Tj * w_q[sourcePlate][m];
                                    }
                                    A_mat[row][col] = ci / 4.0 * sum_cross;
                                }
                            }
                        }

                        if (skinDepth > 0)
                        {
                            Compl du0_dz = ci * k_wave * Math.Sin(teta) * u0(xk, 0);
                            B_vec[row] = -1.0 * u0(xk, 0) - chi * du0_dz;
                        }
                        else { B_vec[row] = -1.0 * u0(xk, 0); }
                    });
                    assemblyWatch.Stop();

                    CVect w = new CVect(totalUnknowns);
                    Stopwatch solveWatch = Stopwatch.StartNew();
                    cancellationToken.ThrowIfCancellationRequested();
                    int output = Gauss(A_mat, B_vec, w, cancellationToken);
                    solveWatch.Stop();
                    for (int ik = 0; ik < totalUnknowns; ik++) y[ik] = w[ik];

                    // Сохраняем матрицу для расчёта обусловленности
                    LastMatrixA = new CMatr(totalUnknowns);
                    for (int r = 0; r < totalUnknowns; r++)
                        for (int c = 0; c < totalUnknowns; c++)
                            LastMatrixA[r][c] = A_mat[r][c];

                    totalWatch.Stop();
                    LastSolvePerformance = new SolvePerformance
                    {
                        BackendName = "CPU (C#)",
                        AssemblyMilliseconds = assemblyWatch.Elapsed.TotalMilliseconds,
                        LinearSolveMilliseconds = solveWatch.Elapsed.TotalMilliseconds,
                        TotalMilliseconds = totalWatch.Elapsed.TotalMilliseconds,
                        UsedCuda = false
                    };

                    return output;
                }
                catch (OperationCanceledException)
                {
                    LastSolveCancelled = true;
                    LastSolvePerformance = null;
                    LastMatrixA = null;
                    return 0;
                }
            }

            public static void RunParameterSweep_Collocation(
                double a, double b, double lambdaMin, double lambdaMax,
                double angleMinDeg, double angleMaxDeg, int nMin, int nMax,
                double[] skinDepths, string outputFilePath)
            {
                File.WriteAllText(outputFilePath, "Method;Lambda;Theta_deg;N;skinDepth;BC_error;CondNumber;Time_ms\n");
                int lambdaSteps = 10, angleSteps = (int)((angleMaxDeg - angleMinDeg) / 5) + 1, nSteps = (nMax - nMin) / 5 + 1;

                for (int l = 0; l < lambdaSteps; l++)
                {
                    double lambda = lambdaMin + (lambdaMax - lambdaMin) * l / (lambdaSteps - 1);
                    for (int a_idx = 0; a_idx < angleSteps; a_idx++)
                    {
                        double theta_deg = angleMinDeg + 5 * a_idx;
                        double theta = theta_deg * Math.PI / 180.0;
                        for (int n_idx = 0; n_idx < nSteps; n_idx++)
                        {
                            int N = nMin + 5 * n_idx;
                            foreach (double skinDepth in skinDepths)
                            {
                                var sw = Stopwatch.StartNew();
                                var solver = new DifrOnLenta(a, b, lambda, theta, N, skinDepth);
                                int solveResult = solver.SolveDifr();
                                sw.Stop();
                                double bcError = double.NaN, condNum = double.NaN;
                                if (solveResult == 1)
                                {
                                    bcError = solver.VerifyBoundaryConditions();
                                    condNum = ConditionNumber(solver.LastMatrixA);
                                }
                                string line = $"Collocation;{lambda:F6};{theta_deg:F2};{N};{skinDepth:F6};{bcError:E6};{condNum:E6};{sw.ElapsedMilliseconds}";
                                File.AppendAllText(outputFilePath, line + "\n");
                                Console.WriteLine(line);
                            }
                        }
                    }
                }
            }

            public double CalculateIncidentEnergy()
            {
                return CalculateReferenceIncidentEnergy();
            }

            public double CalculateReflectedEnergy()
            {
                return CalculateControlContourFlux().Reflected;
            }

            public class EnergyComponents
            {
                public double Incident, Reflected, Transmitted, Absorbed;
                public bool WasRenormalized;
            }

            public EnergyComponents CalculateEnergyComponents()
            {
                ControlContourFlux flux = CalculateControlContourFlux();
                EnergyComponents energy = new EnergyComponents();
                energy.Incident = CalculateReferenceIncidentEnergy();
                energy.Reflected = flux.Reflected;
                energy.Absorbed = CalculateAbsorbedEnergy();
                energy.Transmitted = energy.Incident - energy.Reflected - energy.Absorbed;
                if (energy.Transmitted < 0) energy.Transmitted = 0;
                energy.WasRenormalized = false;

                double total = energy.Reflected + energy.Transmitted + energy.Absorbed;
                double tolerance = Math.Max(energy.Incident, 1.0) * 0.02;
                if (Math.Abs(total - energy.Incident) > tolerance)
                {
                    double balancedTransmitted = energy.Incident - energy.Reflected - energy.Absorbed;
                    if (balancedTransmitted >= 0)
                    {
                        energy.Transmitted = balancedTransmitted;
                    }
                    else
                    {
                        energy.Absorbed = Math.Max(0, energy.Incident - energy.Reflected);
                        energy.Transmitted = 0;
                    }
                    energy.WasRenormalized = true;
                }
                else
                {
                    double balancedTransmitted = energy.Incident - energy.Reflected - energy.Absorbed;
                    if (balancedTransmitted >= 0) energy.Transmitted = balancedTransmitted;
                }

                return energy;
            }

            public double CalculateTransmittedEnergyIndependent()
            {
                EnergyComponents energy = CalculateEnergyComponents();
                return energy.Transmitted;
            }

            private class ControlContourFlux
            {
                public double Incident;
                public double Reflected;
                public double Transmitted;
            }

            private double EnergyFlux(Compl value, Compl normalDerivative)
            {
                return -0.5 * (value.Re * normalDerivative.Im - value.Im * normalDerivative.Re);
            }

            private double CalculateReferenceIncidentEnergy()
            {
                double k = 2 * Math.PI / lambda;
                double span = Math.Max(b - a, lambda);
                double margin = 3.0 * span;
                double width = (b - a) + 2.0 * margin;
                double height = 2.0 * margin;
                return 0.5 * k * (Math.Abs(Math.Cos(teta)) * height + Math.Abs(Math.Sin(teta)) * width);
            }

            private ControlContourFlux CalculateControlContourFlux()
            {
                double k = 2 * Math.PI / lambda;
                double span = Math.Max(b - a, lambda);
                double margin = Math.Max(lambda, 0.25 * span);
                double xMin = a - margin;
                double xMax = b + margin;
                double zMin = -margin;
                double zMax = margin;
                double h = lambda / 200.0;
                const int pointsPerSide = 60;
                const double sideEps = 1e-9;

                ControlContourFlux flux = new ControlContourFlux();
                AccumulateHorizontalFlux(flux, xMin, xMax, zMax, 1.0, pointsPerSide, h, k, sideEps);
                AccumulateHorizontalFlux(flux, xMin, xMax, zMin, -1.0, pointsPerSide, h, k, sideEps);
                AccumulateVerticalFlux(flux, xMin, zMin, zMax, -1.0, pointsPerSide, h, k, sideEps);
                AccumulateVerticalFlux(flux, xMax, zMin, zMax, 1.0, pointsPerSide, h, k, sideEps);
                return flux;
            }

            private void AccumulateHorizontalFlux(ControlContourFlux flux, double xMin, double xMax, double z, double normalZ, int points, double h, double k, double sideEps)
            {
                double dx = (xMax - xMin) / points;
                for (int i = 0; i < points; i++)
                {
                    double x = xMin + (i + 0.5) * dx;
                    Compl uTotal = u(x, z);
                    Compl uIncident = u0(x, z);
                    Compl duTotalDn = normalZ * (u(x, z + h) - u(x, z - h)) / (2.0 * h);
                    Compl duIncidentDn = normalZ * ci * k * Math.Sin(teta) * uIncident;
                    AccumulateFluxSample(flux, uTotal, uIncident, duTotalDn, duIncidentDn, dx, sideEps);
                }
            }

            private void AccumulateVerticalFlux(ControlContourFlux flux, double x, double zMin, double zMax, double normalX, int points, double h, double k, double sideEps)
            {
                double dz = (zMax - zMin) / points;
                for (int i = 0; i < points; i++)
                {
                    double z = zMin + (i + 0.5) * dz;
                    Compl uTotal = u(x, z);
                    Compl uIncident = u0(x, z);
                    Compl duTotalDn = normalX * (u(x + h, z) - u(x - h, z)) / (2.0 * h);
                    Compl duIncidentDn = normalX * ci * k * Math.Cos(teta) * uIncident;
                    AccumulateFluxSample(flux, uTotal, uIncident, duTotalDn, duIncidentDn, dz, sideEps);
                }
            }

            private void AccumulateFluxSample(ControlContourFlux flux, Compl uTotal, Compl uIncident, Compl duTotalDn, Compl duIncidentDn, double ds, double sideEps)
            {
                double incidentFlux = EnergyFlux(uIncident, duIncidentDn);
                double totalFlux = EnergyFlux(uTotal, duTotalDn);
                Compl uScattered = uTotal - uIncident;
                Compl duScatteredDn = duTotalDn - duIncidentDn;
                double scatteredFlux = EnergyFlux(uScattered, duScatteredDn);

                if (incidentFlux < -sideEps)
                {
                    flux.Incident += -incidentFlux * ds;
                }
                else if (incidentFlux > sideEps)
                {
                    if (totalFlux > 0) flux.Transmitted += totalFlux * ds;
                }
                else
                {
                    if (totalFlux > 0) flux.Transmitted += totalFlux * ds;
                }

                if (!double.IsNaN(scatteredFlux) && !double.IsInfinity(scatteredFlux))
                    flux.Reflected += Math.Abs(scatteredFlux) * ds;
            }

            public Compl CurrentDensity(double x)
            {
                int plateIndex = GetPlateIndex(x);
                if (plateIndex < 0) return new Compl(0, 0);
                double halfL = HalfLength(plateIndex), tau_x = XToTau(plateIndex, x);
                if (tau_x < -1) tau_x = -1; if (tau_x > 1) tau_x = 1;
                Compl phi = new Compl(0, 0);
                for (int j = 0; j < N; j++) phi += y[CoeffIndex(plateIndex, j)] * Cheb(j, tau_x);
                if (useSingularWeight)
                {
                    double w = Math.Sqrt(Math.Max(1.0 - tau_x * tau_x, 1e-10));
                    return phi / (halfL * w);
                }
                return phi;
            }

            public double CalculateTransmittedThroughStrip()
            {
                double k = 2 * Math.PI / lambda;
                const int M = 200;
                double sum = 0;
                for (int plateIndex = 0; plateIndex < PlateCount; plateIndex++)
                {
                    double dx = (beta[plateIndex] - alpha[plateIndex]) / M;
                    for (int m = 1; m < M; m++)
                    {
                        double x = alpha[plateIndex] + m * dx;
                        Compl u_val = u_on_strip(x), Jx = GetJphys(x);
                        Compl du_dz_below = ci * k * Math.Sin(teta) * u0(x, 0) + Jx / 2.0;
                        Compl du_conj = new Compl(du_dz_below.Re, -du_dz_below.Im);
                        sum += -0.5 * (u_val * du_conj).Re * dx;
                    }
                }
                return sum * k / (2.0 * Math.PI);
            }

            public Compl GetJphys(double x)
            {
                int plateIndex = GetPlateIndex(x);
                if (plateIndex < 0) return new Compl(0, 0);
                double halfL = HalfLength(plateIndex), xi = XToTau(plateIndex, x);
                double w2 = 1.0 - xi * xi; if (w2 < 1e-10) w2 = 1e-10;
                Compl phi = new Compl(0, 0);
                for (int j = 0; j < N; j++) phi += y[CoeffIndex(plateIndex, j)] * new Compl(ChebOnPlate(plateIndex, j, x));
                return phi / (halfL * Math.Sqrt(w2));
            }

            public double VerifyBoundaryConditions()
            {
                int M = 40;
                double sumErr = 0, k_wave = 2 * Math.PI / lambda;
                int count = 0;
                for (int plateIndex = 0; plateIndex < PlateCount; plateIndex++)
                {
                    double dx = (beta[plateIndex] - alpha[plateIndex]) / M;
                    for (int i = 1; i < M; i++)
                    {
                        double x = alpha[plateIndex] + i * dx;
                        Compl u_val = u(x, 0), du0_dz = ci * k_wave * Math.Sin(teta) * u0(x, 0), J_val = CurrentDensity(x);
                        Compl du_total = du0_dz - J_val / 2.0, bc_val = u_val + chi * du_total;
                        double scale = Compl.Abs(u0(x, 0)); if (scale < 0.01) scale = 0.01;
                        sumErr += Compl.Abs(bc_val) / scale; count++;
                    }
                }
                return sumErr / count;
            }

            public double VerifyHelmholtz()
            {
                double x = b + lambda, z = lambda, k = 2 * Math.PI / lambda, h = lambda / 100.0;
                Compl u_0 = u(x, z), u_x1 = u(x + h, z), u_x2 = u(x - h, z), u_z1 = u(x, z + h), u_z2 = u(x, z - h);
                Compl laplacian = (u_x1 + u_x2 + u_z1 + u_z2 - 4 * u_0) / (h * h);
                Compl helmholtz = laplacian + k * k * u_0;
                return Compl.Abs(helmholtz) / (k * k * Compl.Abs(u_0) + 1e-10);
            }

            public double CalculateAbsorbedEnergy()
            {
                if (skinDepth <= 0) return 0;
                double k = 2 * Math.PI / lambda, sum = 0;
                const int M = 200;
                for (int plateIndex = 0; plateIndex < PlateCount; plateIndex++)
                {
                    double dx = (beta[plateIndex] - alpha[plateIndex]) / M;
                    for (int m = 1; m < M; m++)
                    {
                        double x = alpha[plateIndex] + m * dx;
                        Compl Jx = CurrentDensity(x), du_dn = ci * k * Math.Sin(teta) * u0(x, 0) - Jx / 2.0;
                        double du_dn_abs2 = du_dn.Re * du_dn.Re + du_dn.Im * du_dn.Im;
                        sum += 0.5 * chi.Re * du_dn_abs2 * dx;
                    }
                }
                return sum * k / (2.0 * Math.PI);
            }

            public void VerifyEnergyConservation()
            {
                EnergyComponents energy = CalculateEnergyComponents();
                double total = energy.Reflected + energy.Transmitted + energy.Absorbed;
                Console.WriteLine("Energy Balance Check:");
                if (energy.WasRenormalized) Console.WriteLine("  ⚠ Note: energies were renormalized due to numerical errors");
                Console.WriteLine(string.Format("  Incident:    {0:F6} (100%)", energy.Incident));
                Console.WriteLine(string.Format("  Reflected:   {0:F6} ({1:P2})", energy.Reflected, energy.Reflected / energy.Incident));
                Console.WriteLine(string.Format("  Transmitted: {0:F6} ({1:P2})", energy.Transmitted, energy.Transmitted / energy.Incident));
                Console.WriteLine(string.Format("  Absorbed:    {0:F6} ({1:P2})", energy.Absorbed, energy.Absorbed / energy.Incident));
                Console.WriteLine(string.Format("  Total:       {0:F6}", total));
                double error = Math.Abs(energy.Incident - total), relError = error / energy.Incident;
                Console.WriteLine(string.Format("  Error:       {0:E6} ({1:P2})", error, relError));
                if (relError < 0.05) Console.WriteLine("  ✓ Energy conservation verified!");
                else Console.WriteLine("  ⚠ Warning: significant energy imbalance");
            }

            public void TestConvergence()
            {
                Console.WriteLine("Convergence test for different M values:");
                int[] M_values = { 10, 20, 40, 80 };
                foreach (int testM in M_values)
                {
                    var testSolver = new DifrOnLenta(a, b, lambda, teta, N, skinDepth);
                    if (testSolver.SolveDifr() == 1)
                    {
                        double energy = testSolver.CalculateReflectedEnergy();
                        Console.WriteLine(string.Format("  M={0,3}: Reflected Energy = {1:E6}", testM, energy));
                    }
                }
            }
        }
    }
}
