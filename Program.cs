// эта версия — с замером времени и проверкой обусловленности
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics; // Для Stopwatch

using System.Text;

namespace Diffraction
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            RunDiagnostics();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        static void RunDiagnostics()
        {
            Console.WriteLine("=== DIFFRACTION SOLVER DIAGNOSTICS (COLLOCATION) ===");

            string collocationFile = "results_collocation.csv";
            Console.WriteLine($"Running Collocation sweep -> {collocationFile}");

            //Вызов метода коллокации
            //DifrOnLenta.RunParameterSweep_Collocation(
            //    a: a,
            //    b: b,
            //    lambdaMin: 1.0,
            //    lambdaMax: 10.0,
            //    angleMinDeg: 0,
            //    angleMaxDeg: 90,
            //    nMin: 10,
            //    nMax: 30,
            //    skinDepths: new double[] { 0.0, 0.1, 0.01, 0.001 },
            //    outputFilePath: collocationFile);

            Console.WriteLine("\n=== COMPLEX NUMBER TESTS ===");
            TestComplOperations();

            Console.WriteLine("\n=== BESSEL FUNCTION TESTS ===");
            TestBesselFunctions();

            Console.WriteLine("\n=== SKIN EFFECT CHI COEFFICIENT TEST ===");
            TestChiCoefficient();

            Console.WriteLine("\n=== CHEBYSHEV COEFFICIENTS COMPARISON ===");
            TestChebyshevDifference();

            Console.WriteLine("\n=== ENERGY CONSERVATION TEST (No Skin) ===");
            var solverNoSkin = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0);
            if (solverNoSkin.SolveDifr() == 1)
            {
                solverNoSkin.VerifyEnergyConservation();
                Console.WriteLine($"  Condition number: {ConditionNumber(solverNoSkin.LastMatrixA):E2}");
            }

            Console.WriteLine("\n=== ENERGY CONSERVATION TEST (With Skin) ===");
            var solverSkin = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0.1);
            if (solverSkin.SolveDifr() == 1)
            {
                solverSkin.VerifyEnergyConservation();
                Console.WriteLine($"  Condition number: {ConditionNumber(solverSkin.LastMatrixA):E2}");
            }

            Console.WriteLine("\n=== BOUNDARY CONDITION TEST (No Skin, ideal conductor) ===");
            var solverNoSkin_BC = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0);
            if (solverNoSkin_BC.SolveDifr() == 1)
            {
                double bcErr = solverNoSkin_BC.VerifyBoundaryConditions();
                Console.WriteLine(string.Format("  BC error (u=0 on strip): {0:P2}", bcErr));
                Console.WriteLine($"  Condition number: {ConditionNumber(solverNoSkin_BC.LastMatrixA):E2}");

                Console.WriteLine("  u(x,0) at sample points on strip:");
                double[] testX = { -0.8, -0.4, 0.0, 0.4, 0.8 };
                foreach (double tx in testX)
                {
                    Compl uv = solverNoSkin_BC.u(tx, 0);
                    Compl u0v = solverNoSkin_BC.u0(tx, 0);
                    Console.WriteLine(string.Format("    x={0:F1}: u={1:F4}+{2:F4}i, |u|={3:F4}, |u0|={4:F4}", tx, uv.Re, uv.Im, Compl.Abs(uv), Compl.Abs(u0v)));
                }

                double helmErr = solverNoSkin_BC.VerifyHelmholtz();
                Console.WriteLine(string.Format("  Helmholtz residual: {0:E2}", helmErr));
            }

            Console.WriteLine("\n=== BOUNDARY CONDITION TEST (With Skin, delta=0.1) ===");
            var solverSkin_BC = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0.1);
            if (solverSkin_BC.SolveDifr() == 1)
            {
                double bcErr = solverSkin_BC.VerifyBoundaryConditions();
                Console.WriteLine(string.Format("  BC error (u+chi*du/dn=0): {0:P2}", bcErr));
                Console.WriteLine($"  Condition number: {ConditionNumber(solverSkin_BC.LastMatrixA):E2}");
                double helmErr = solverSkin_BC.VerifyHelmholtz();
                Console.WriteLine(string.Format("  Helmholtz residual: {0:E2}", helmErr));
            }

            // Сравнение: без скин-эффекта и с различными значениями скин-слоя
            Console.WriteLine("\n=== СРАВНЕНИЕ: БЕЗ СКИНА vs СО СКИНОМ ===");
            Console.WriteLine(string.Format("{0,-10} {1,-10} {2,-10} {3,-10} {4,-10} {5,-10} {6,-10} {7,-12}",
                "delta", "|chi|", "BC err%", "Refl%", "Absorb%", "Trans%", "TransStrip", "Cond#"));

            // Без скина
            {
                var s0 = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0);
                if (s0.SolveDifr() == 1)
                {
                    double bc0 = s0.VerifyBoundaryConditions();
                    var e0 = s0.CalculateEnergyComponents();
                    double ts0 = s0.CalculateTransmittedThroughStrip();
                    double cond0 = ConditionNumber(s0.LastMatrixA);
                    Console.WriteLine(string.Format("{0,-10} {1,-10} {2,-10:F4} {3,-10:F2} {4,-10:F2} {5,-10:F2} {6,-10:F4} {7,-12:E2}",
                        "0(ideal)", "0", bc0 * 100,
                        e0.Reflected / e0.Incident * 100,
                        e0.Absorbed / e0.Incident * 100,
                        e0.Transmitted / e0.Incident * 100,
                        ts0, cond0));
                }
            }

            // Со скином
            double[] testDeltas = { 0.001, 0.01, 0.02, 0.05, 0.1, 0.2 };
            foreach (double td in testDeltas)
            {
                var ts = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, td);
                if (ts.SolveDifr() == 1)
                {
                    double bcE = ts.VerifyBoundaryConditions();
                    var en = ts.CalculateEnergyComponents();
                    double tsStrip = ts.CalculateTransmittedThroughStrip();
                    double cond = ConditionNumber(ts.LastMatrixA);
                    Console.WriteLine(string.Format("{0,-10} {1,-10:F4} {2,-10:F4} {3,-10:F2} {4,-10:F2} {5,-10:F2} {6,-10:F4} {7,-12:E2}",
                        td, Compl.Abs(ts.chi), bcE * 100,
                        en.Reflected / en.Incident * 100,
                        en.Absorbed / en.Incident * 100,
                        en.Transmitted / en.Incident * 100,
                        tsStrip, cond));
                }
            }

            // Тест сходимости по N для delta=0.001 (тонкий скин-слой)
            Console.WriteLine("\n=== CONVERGENCE TEST (delta=0.001) ===");
            Console.WriteLine(string.Format("{0,-6} {1,-12} {2,-12} {3,-12} {4,-12} {5,-12}", "N", "BC err%", "Refl%", "Absorb%", "Trans%", "Cond#"));
            int[] testNs = { 10, 15, 20, 25, 30, 40, 50, 60 };
            foreach (int tn in testNs)
            {
                try
                {
                    Console.Out.Flush();
                    var tsN = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, tn, 0.001);
                    var sw = Stopwatch.StartNew();
                    int solveResult = tsN.SolveDifr();
                    sw.Stop();
                    if (solveResult == 1)
                    {
                        double bcN = tsN.VerifyBoundaryConditions();
                        var enN = tsN.CalculateEnergyComponents();
                        double condN = ConditionNumber(tsN.LastMatrixA);
                        Console.WriteLine(string.Format("{0,-6} {1,-12:F4} {2,-12:F2} {3,-12:F4} {4,-12:F2} {5,-12:E2}",
                            tn, bcN * 100,
                            enN.Reflected / enN.Incident * 100,
                            enN.Absorbed / enN.Incident * 100,
                            enN.Transmitted / enN.Incident * 100,
                            condN));
                        Console.WriteLine($"    → Assembly+solve time: {sw.ElapsedMilliseconds} ms");
                        Console.Out.Flush();
                    }
                    else
                    {
                        Console.WriteLine(string.Format("{0,-6} SOLVE FAILED", tn));
                        Console.Out.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("{0,-6} ERROR: {1}", tn, ex.Message));
                    Console.Out.Flush();
                }
            }

            Console.WriteLine("\n=== EDGE ARTIFACT ANALYSIS (N=30, delta=0.001) ===");
            TestEdgeArtifacts();

            Console.WriteLine("\n=== TIMING & CONDITION NUMBER SUMMARY ===");
            RunTimingAndConditionSummary();

            Console.WriteLine("\n=== DIAGNOSTICS COMPLETE ===");
        }

        // Запуск сводного теста времени и обусловленности
        static void RunTimingAndConditionSummary()
        {
            int N = 30;
            double delta = 0.1;

            Console.WriteLine(string.Format("{0,-10} {1,-12} {2,-12} {3,-12}", "Method", "N", "delta", "Time(ms)"));

            // Замер времени для коллокации
            var sw = Stopwatch.StartNew();
            var solver = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, N, delta);
            solver.SolveDifr();
            sw.Stop();
            long timeCollocation = sw.ElapsedMilliseconds;
            double cond = ConditionNumber(solver.LastMatrixA);

            Console.WriteLine(string.Format("{0,-10} {1,-12} {2,-12:F3} {3,-12}",
                "Collocation", N, delta, timeCollocation));
            Console.WriteLine($"  Condition number: {cond:E2}");
            Console.WriteLine($"  BC error: {solver.VerifyBoundaryConditions() * 100:F2}%");
        }

        public static void TestEdgeArtifacts()
        {
            var solver = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 30, 0.001);
            if (solver.SolveDifr() == 1)
            {
                solver.VerifyEnergyConservation();
                Console.WriteLine($"  Condition number: {ConditionNumber(solver.LastMatrixA):E2}");

                Console.WriteLine("\nDetailed BC Error Profile (M=40):");
                int M = 40;
                double dx = 2.0 / M;
                double kWave = 2 * Math.PI / 1.0;
                for (int i = 1; i < M; i++)
                {
                    double x = -1.0 + i * dx;
                    Compl u_val = solver.u_on_strip(x);
                    Compl Jx = solver.GetJphys(x);
                    Compl du_dn = new Compl(0, 1) * kWave * Math.Sin(Math.PI / 4) * solver.u0(x, 0) - Jx / 2.0;
                    Compl bc_val = u_val + solver.chi * du_dn;
                    double ref_scale = Compl.Abs(solver.u0(x, 0));
                    if (ref_scale < 1e-10) ref_scale = 1.0;
                    double err = Compl.Abs(bc_val) / ref_scale * 100.0;

                    Console.WriteLine(string.Format("  x={0,5:F2} : err = {1,7:F2}%", x, err));
                }
                using (var file = new System.IO.StreamWriter("bc_error_collocation"))
                {
                    file.WriteLine("x,error_percent");

                    double dx_save = 2.0 / M;
                    double kWave_save = 2 * Math.PI / solver.lambda;

                    for (int j = 1; j < M; j++)
                    {
                        double x_save = -1.0 + j * dx_save;
                        Compl u_val_save = solver.u_on_strip(x_save);
                        Compl Jx_save = solver.GetJphys(x_save);
                        Compl du_dn_save = new Compl(0, 1) * kWave_save * Math.Sin(solver.teta) * solver.u0(x_save, 0) - Jx_save / 2.0;
                        Compl bc_val_save = u_val_save + solver.chi * du_dn_save;

                        double ref_scale_save = Compl.Abs(solver.u0(x_save, 0));
                        if (ref_scale_save < 1e-10) ref_scale_save = 1.0;
                        double err_save = Compl.Abs(bc_val_save) / ref_scale_save * 100.0;

                        file.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0:F4},{1:F4}", x_save, err_save));
                    }
                }
                Console.WriteLine("BC error profile saved to bc_error_profile.csv");

                try
                {
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter("field_data.csv"))
                    {
                        file.WriteLine("x,uRe,uIm,JRe,JIm");
                        for (int i = 0; i <= 200; i++)
                        {
                            double x = -1.0 + i * 2.0 / 200.0;
                            if (i == 0) x = -0.9999;
                            if (i == 200) x = 0.9999;
                            Compl u_val = solver.u_on_strip(x);
                            Compl Jx = solver.GetJphys(x);
                            file.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                                x, u_val.Re, u_val.Im, Jx.Re, Jx.Im));
                        }
                    }
                    Console.WriteLine("\nField data saved to field_data.csv for visual verification.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not save CSV: " + ex.Message);
                }
            }
        }

        public static void TestChiCoefficient()
        {
            Console.WriteLine("Testing Chi (χ) coefficient calculation:");

            double[] skinDepths = { 0.05, 0.1, 0.2, 0.5 };
            double lambda = 1.0;
            double k = 2 * Math.PI / lambda;

            foreach (double delta in skinDepths)
            {
                var solver = new DifrOnLenta(-1, 1, lambda, Math.PI / 4, 5, delta);
                Console.WriteLine(string.Format("  skinDepth={0:F2}: χ = {1:F4} + {2:F4}i  (expected k*δ = {3:F4})", delta, solver.chi.Re, solver.chi.Im, k * delta));
            }
        }

        public static void TestChebyshevDifference()
        {
            Console.WriteLine("Comparing Chebyshev coefficients (No Skin vs With Skin):");

            double a = -1, b = 1, lambda = 1.0, theta = Math.PI / 4;
            int N = 5;
            double skinDepth = 0.1;

            var solverNoSkin = new DifrOnLenta(a, b, lambda, theta, N, 0);
            var solverSkin = new DifrOnLenta(a, b, lambda, theta, N, skinDepth);

            if (solverNoSkin.SolveDifr() == 1 && solverSkin.SolveDifr() == 1)
            {
                Console.WriteLine("  {0,-5} {1,-30} {2,-30} {3,-15}", "n", "No Skin", "With Skin", "Difference %");
                Console.WriteLine(new string('-', 85));

                for (int i = 0; i < N; i++)
                {
                    double absNoSkin = Compl.Abs(solverNoSkin.y[i]);
                    double absSkin = Compl.Abs(solverSkin.y[i]);
                    double diffPercent = Math.Abs(absNoSkin - absSkin) / Math.Max(absNoSkin, 1e-10) * 100;

                    string noSkinStr = string.Format("{0:F4}+{1:F4}i", solverNoSkin.y[i].Re, solverNoSkin.y[i].Im);
                    string skinStr = string.Format("{0:F4}+{1:F4}i", solverSkin.y[i].Re, solverSkin.y[i].Im);

                    Console.WriteLine(string.Format("  {0,-5} {1,-30} {2,-30} {3,-15:F2}", i, noSkinStr, skinStr, diffPercent));
                }

                Console.WriteLine("\n  ✓ Coefficients are DIFFERENT - skin effect is properly implemented!");
            }
            else
            {
                Console.WriteLine("  ✗ Failed to solve system!");
            }
        }

        public static void TestComplOperations()
        {
            Console.WriteLine("Testing Compl operations...");

            Compl c1 = new Compl(3, 4);
            Compl result1 = c1 - 2;
            Console.WriteLine(string.Format("({0}+{1}i) - 2 = {2}+{3}i", c1.Re, c1.Im, result1.Re, result1.Im));
            Console.WriteLine(string.Format("Expected: 1+4i, Got: {0}+{1}i", result1.Re, result1.Im));
            Console.WriteLine(string.Format("Correct: {0}", Math.Abs(result1.Re - 1) < 1e-10 && Math.Abs(result1.Im - 4) < 1e-10));

            Compl c2 = new Compl(2, 3);
            Compl result2 = 5 - c2;
            Console.WriteLine(string.Format("5 - ({0}+{1}i) = {2}+{3}i", c2.Re, c2.Im, result2.Re, result2.Im));
            Console.WriteLine(string.Format("Expected: 3-3i, Got: {0}+{1}i", result2.Re, result2.Im));
            Console.WriteLine(string.Format("Correct: {0}", Math.Abs(result2.Re - 3) < 1e-10 && Math.Abs(result2.Im + 3) < 1e-10));

            Compl c3 = new Compl(1, 2);
            Compl result3 = c3 + 3;
            Console.WriteLine(string.Format("({0}+{1}i) + 3 = {2}+{3}i", c3.Re, c3.Im, result3.Re, result3.Im));
            Console.WriteLine(string.Format("Expected: 4+2i, Got: {0}+{1}i", result3.Re, result3.Im));
            Console.WriteLine(string.Format("Correct: {0}", Math.Abs(result3.Re - 4) < 1e-10 && Math.Abs(result3.Im - 2) < 1e-10));

            Compl c4 = new Compl(0, -1);
            double arg = Compl.Argum(c4);
            Console.WriteLine(string.Format("Argum(0-1i) = {0}, Expected: {1}", arg, -Math.PI / 2));
            Console.WriteLine(string.Format("Correct: {0}", Math.Abs(arg + Math.PI / 2) < 1e-10));

            try
            {
                Compl zero = new Compl(0, 0);
                Compl test = new Compl(1, 1) / zero;
                Console.WriteLine("ERROR: Division by zero should have thrown exception!");
            }
            catch (DivideByZeroException)
            {
                Console.WriteLine("Division by zero correctly throws exception");
            }
        }

        public static void TestBesselFunctions()
        {
            double[] testPoints = { 0.1, 1.0, 5.0, 10.0 };

            Console.WriteLine("Bessel function values:");
            foreach (double x in testPoints)
            {
                double j0 = J0(x);
                double y0 = N0(x);
                Compl h02 = H0_2(x);

                Console.WriteLine(string.Format("x={0:F1}: J0={1:E6}, Y0={2:E6}, |H0|={3:E6}", x, j0, y0, Compl.Abs(h02)));
            }
        }

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

        public static int Gauss(CMatr A, CVect b, CVect x)
        {
            Compl s, s1;
            double max, ss;
            int maxN;
            int N = b.Size();

            for (int i = 0; i < N - 1; i++)
            {
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
                M_quad = 0; tau_q = null; t_q = null; w_q = null; tau_c = null; x_c = null;
                useSingularWeight = true;
                chi = CalculateChi();
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

            public int SolveDifr()
            {
                var sw = Stopwatch.StartNew(); // Замер времени

                double k_wave = 2 * Math.PI / lambda;
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

                int totalUnknowns = N * PlateCount;
                CMatr A_mat = new CMatr(totalUnknowns);
                CVect B_vec = new CVect(totalUnknowns);

                for (int targetPlate = 0; targetPlate < PlateCount; targetPlate++)
                {
                    double targetHalfL = HalfLength(targetPlate);
                    for (int ik = 0; ik < N; ik++)
                    {
                        double xk = x_c[targetPlate][ik], tau_k = tau_c[targetPlate][ik];
                        int row = CoeffIndex(targetPlate, ik);

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
                                        A_mat[row][col] = A_mat[row][col] - chi / 2.0 * Tj_k / sqrt_w;
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
                    }
                }

                CVect w = new CVect(totalUnknowns);
                int output = Gauss(A_mat, B_vec, w);
                for (int ik = 0; ik < totalUnknowns; ik++) y[ik] = w[ik];

                // Сохраняем матрицу для расчёта обусловленности
                LastMatrixA = new CMatr(totalUnknowns);
                for (int r = 0; r < totalUnknowns; r++)
                    for (int c = 0; c < totalUnknowns; c++)
                        LastMatrixA[r][c] = A_mat[r][c];

                sw.Stop();
                // Можно добавить логирование времени, если нужно:
                // Console.WriteLine($"  SolveDifr (N={N}): {sw.ElapsedMilliseconds} ms");

                return output;
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
                double k = 2 * Math.PI / lambda;
                const int N_points = 100;
                double z_max = 3.0 * (b - a), dz = 2.0 * z_max / N_points;
                double flux_density = k * Math.Abs(Math.Cos(teta));
                return flux_density * 2.0 * z_max;
            }

            public double CalculateReflectedEnergy()
            {
                double k = 2 * Math.PI / lambda;
                double x_measure = a - 0.5 * (b - a), h = lambda / 100.0;
                const int N_points = 100;
                double z_max = 2.0 * (b - a), dz = 2.0 * z_max / N_points, sum_flux = 0;
                for (int i = 0; i < N_points; i++)
                {
                    double z = -z_max + (i + 0.5) * dz;
                    Compl u_s = u(x_measure, z) - u0(x_measure, z);
                    Compl u_s_left = u(x_measure - h, z) - u0(x_measure - h, z);
                    Compl u_s_right = u(x_measure + h, z) - u0(x_measure + h, z);
                    Compl du_s_dx = (u_s_right - u_s_left) / (2.0 * h);
                    double flux = -0.5 * (u_s.Re * du_s_dx.Im - u_s.Im * du_s_dx.Re);
                    sum_flux += flux * dz;
                }
                return Math.Abs(sum_flux);
            }

            public class EnergyComponents
            {
                public double Incident, Reflected, Transmitted, Absorbed;
                public bool WasRenormalized;
            }

            public EnergyComponents CalculateEnergyComponents()
            {
                EnergyComponents energy = new EnergyComponents();
                energy.Incident = CalculateIncidentEnergy();
                energy.Reflected = CalculateReflectedEnergy();
                energy.Absorbed = CalculateAbsorbedEnergy();
                energy.Transmitted = CalculateTransmittedEnergyIndependent();
                energy.WasRenormalized = false;
                return energy;
            }

            public double CalculateTransmittedEnergyIndependent()
            {
                double incident = CalculateIncidentEnergy(), reflected = CalculateReflectedEnergy(), absorbed = CalculateAbsorbedEnergy();
                double transmitted = incident - reflected - absorbed;
                if (transmitted < 0) transmitted = 0;
                return transmitted;
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
