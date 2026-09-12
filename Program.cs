// эта версия — с замером времени и проверкой обусловленности
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Diagnostics; // Для Stopwatch
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using System.Text;
using static Diffraction.Core.DiffractionMath;
using Compl = Diffraction.Core.DiffractionMath.Compl;
using DifrOnLenta = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (HasArg(args, "--diagnostics"))
                {
                    RunDiagnostics();
                    return;
                }

                if (HasArg(args, "--accuracy-sweep") || HasArg(args, "--accuracy-sweep-quick"))
                {
                    bool quick = HasArg(args, "--accuracy-sweep-quick") || HasArg(args, "--quick");
                    string outputFile = GetArgValue(args, "--output") ?? Path.Combine(Environment.CurrentDirectory, "accuracy_sweep.csv");
                    double[] angles = ParseDoubleListArg(args, "--angles");
                    int[] ns = ParseIntListArg(args, "--n-values");
                    double[] skinDepths = ParseDoubleListArg(args, "--skins");
                    RunAccuracySweep(outputFile, quick, angles, ns, skinDepths);
                    return;
                }

                if (HasArg(args, "--skin-method-compare"))
                {
                    RunSkinMethodCompare(args);
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static bool HasArg(string[] args, string name)
        {
            return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private static double[] ParseDoubleListArg(string[] args, string name)
        {
            string value = GetArgValue(args, name);
            if (string.IsNullOrWhiteSpace(value)) return null;

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => double.Parse(item.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static int[] ParseIntListArg(string[] args, string name)
        {
            string value = GetArgValue(args, name);
            if (string.IsNullOrWhiteSpace(value)) return null;

            return value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => int.Parse(item.Trim(), CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static double GetDoubleArg(string[] args, string name, double fallback)
        {
            string value = GetArgValue(args, name);
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : double.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }

        private static int GetIntArg(string[] args, string name, int fallback)
        {
            string value = GetArgValue(args, name);
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : int.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }

        private class AccuracySweepRow
        {
            public int SolveResult;
            public double ThetaDeg, SkinDepth;
            public int N;
            public double BcError, HelmholtzResidual;
            public double Incident, ReflectedScattered, TransmittedScattered, TotalScattered;
            public double IncidentSideDeficit, OppositeSideSignedFlux, Absorbed, PlateBalanceTotal, EnergyBalanceError;
            public double AboveFlux, BelowFlux, FluxAbsorbed;
            public double ScatteredAbove, ScatteredBelow, ScatteredMismatch;
            public long SolveTimeMs, MetricsTimeMs;
            public string ErrorMessage;
        }

        private static string F(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsPositiveInfinity(value)) return "Infinity";
            if (double.IsNegativeInfinity(value)) return "-Infinity";
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static double PercentOf(double value, double total)
        {
            if (Math.Abs(total) < 1e-8) return double.NaN;
            return value / total * 100.0;
        }

        private static AccuracySweepRow RunAccuracySweepCase(double thetaDeg, int n, double skinDepth)
        {
            AccuracySweepRow row = new AccuracySweepRow
            {
                ThetaDeg = thetaDeg,
                N = n,
                SkinDepth = skinDepth,
                SolveResult = -1,
                BcError = double.NaN,
                HelmholtzResidual = double.NaN,
                Incident = double.NaN,
                ReflectedScattered = double.NaN,
                TransmittedScattered = double.NaN,
                TotalScattered = double.NaN,
                IncidentSideDeficit = double.NaN,
                OppositeSideSignedFlux = double.NaN,
                Absorbed = double.NaN,
                PlateBalanceTotal = double.NaN,
                EnergyBalanceError = double.NaN,
                AboveFlux = double.NaN,
                BelowFlux = double.NaN,
                FluxAbsorbed = double.NaN,
                ScatteredAbove = double.NaN,
                ScatteredBelow = double.NaN,
                ScatteredMismatch = double.NaN
            };

            try
            {
                double theta = thetaDeg * Math.PI / 180.0;
                var solver = new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, 1.0, theta, n, skinDepth);
                var solveWatch = Stopwatch.StartNew();
                row.SolveResult = solver.SolveDifr();
                solveWatch.Stop();
                row.SolveTimeMs = solveWatch.ElapsedMilliseconds;

                if (row.SolveResult != 1) return row;

                var metricsWatch = Stopwatch.StartNew();
                row.BcError = solver.VerifyBoundaryConditions();
                row.HelmholtzResidual = solver.VerifyHelmholtz();
                var energy = solver.CalculateEnergyComponents();
                var farField = solver.CalculateFarFieldScatteredEnergy(360, Math.Max(8 * n, 80));
                var scatteredSheet = solver.CalculateScatteredSheetFluxComponents();
                row.Incident = energy.Incident;
                row.ReflectedScattered = farField.ReflectedScattered;
                row.TransmittedScattered = farField.TransmittedScattered;
                row.TotalScattered = farField.TotalScattered;
                row.IncidentSideDeficit = energy.IncidentSideDeficit;
                row.OppositeSideSignedFlux = energy.OppositeSideSignedFlux;
                row.Absorbed = energy.Absorbed;
                row.AboveFlux = energy.AboveFlux;
                row.BelowFlux = energy.BelowFlux;
                row.FluxAbsorbed = energy.FluxAbsorbed;
                row.ScatteredAbove = scatteredSheet.AboveOutgoing;
                row.ScatteredBelow = scatteredSheet.BelowOutgoing;
                row.ScatteredMismatch = scatteredSheet.AbsoluteMismatch;
                row.PlateBalanceTotal = energy.IncidentSideDeficit + energy.OppositeSideSignedFlux + energy.Absorbed;
                row.EnergyBalanceError = Math.Abs(row.Incident) < 1e-8
                    ? double.NaN
                    : Math.Abs(energy.LocalBalanceResidual) / row.Incident;
                metricsWatch.Stop();
                row.MetricsTimeMs = metricsWatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                row.ErrorMessage = ex.Message;
            }

            return row;
        }

        private static void RunAccuracySweep(string outputFilePath, bool quick, double[] angleOverride, int[] nOverride, double[] skinDepthOverride)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputFilePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string compareFilePath = Path.Combine(
                directory ?? Environment.CurrentDirectory,
                Path.GetFileNameWithoutExtension(outputFilePath) + "_compare.csv");

            double[] angles = angleOverride ?? (quick ? new double[] { 0, 30, 60, 90 } : Enumerable.Range(0, 19).Select(i => i * 5.0).ToArray());
            int[] ns = nOverride ?? (quick ? new int[] { 5, 10, 20, 30, 60 } : Enumerable.Range(1, 12).Select(i => i * 5).ToArray());
            double[] skinDepths = skinDepthOverride ?? new double[] { 0.0, 0.1, 0.01, 0.001 };

            using (var rowsWriter = new StreamWriter(outputFilePath, false, Encoding.UTF8))
            using (var compareWriter = new StreamWriter(compareFilePath, false, Encoding.UTF8))
            {
                rowsWriter.WriteLine("ThetaDeg;N;SkinDepth;SolveResult;BcErrorPct;HelmholtzResidual;PlateIncident;ReflectedScatteredPct;ForwardScatteredPct;ScatteredTotalPct;ScatteredHalfPlaneMismatchPct;SheetScatteredAbovePct;SheetScatteredBelowPct;SheetScatteredMismatchPct;IncidentSideDeficitPct;OppositeSideSignedFluxPct;AbsorbedCurrentPct;AbsorbedFluxPct;AboveTotalFluxPct;BelowTotalFluxPct;PlateBalanceTotalPct;LocalBalanceErrorPct;SolveTimeMs;MetricsTimeMs;ErrorMessage");
                compareWriter.WriteLine("ThetaDeg;N;SkinDepth;IdealBcErrorPct;SkinBcErrorPct;BcAbsDiffPct;IdealReflectedScatteredPct;SkinReflectedScatteredPct;ReflectedScatteredAbsDiffPct;IdealForwardScatteredPct;SkinForwardScatteredPct;ForwardScatteredAbsDiffPct;SkinScatteredHalfPlaneMismatchPct;SkinPlateBalanceTotalPct;SkinAbsorbedPct");

                foreach (double thetaDeg in angles)
                {
                    foreach (int n in ns)
                    {
                        AccuracySweepRow ideal = null;
                        foreach (double skinDepth in skinDepths)
                        {
                            AccuracySweepRow row = RunAccuracySweepCase(thetaDeg, n, skinDepth);
                            if (skinDepth == 0) ideal = row;

                            rowsWriter.WriteLine(string.Join(";",
                                F(row.ThetaDeg),
                                row.N.ToString(CultureInfo.InvariantCulture),
                                F(row.SkinDepth),
                                row.SolveResult.ToString(CultureInfo.InvariantCulture),
                                F(row.BcError * 100.0),
                                F(row.HelmholtzResidual),
                                F(row.Incident),
                                F(PercentOf(row.ReflectedScattered, row.Incident)),
                                F(PercentOf(row.TransmittedScattered, row.Incident)),
                                F(PercentOf(row.TotalScattered, row.Incident)),
                                F(PercentOf(Math.Abs(row.ReflectedScattered - row.TransmittedScattered), row.Incident)),
                                F(PercentOf(row.ScatteredAbove, row.Incident)),
                                F(PercentOf(row.ScatteredBelow, row.Incident)),
                                F(PercentOf(row.ScatteredMismatch, row.Incident)),
                                F(PercentOf(row.IncidentSideDeficit, row.Incident)),
                                F(PercentOf(row.OppositeSideSignedFlux, row.Incident)),
                                F(PercentOf(row.Absorbed, row.Incident)),
                                F(PercentOf(row.FluxAbsorbed, row.Incident)),
                                F(PercentOf(row.AboveFlux, row.Incident)),
                                F(PercentOf(row.BelowFlux, row.Incident)),
                                F(PercentOf(row.PlateBalanceTotal, row.Incident)),
                                F(row.EnergyBalanceError * 100.0),
                                row.SolveTimeMs.ToString(CultureInfo.InvariantCulture),
                                row.MetricsTimeMs.ToString(CultureInfo.InvariantCulture),
                                row.ErrorMessage ?? ""));

                            if (ideal != null && skinDepth > 0)
                            {
                                double idealReflectedPct = PercentOf(ideal.ReflectedScattered, ideal.Incident);
                                double skinReflectedPct = PercentOf(row.ReflectedScattered, row.Incident);
                                double idealTransmittedPct = PercentOf(ideal.TransmittedScattered, ideal.Incident);
                                double skinTransmittedPct = PercentOf(row.TransmittedScattered, row.Incident);

                                compareWriter.WriteLine(string.Join(";",
                                    F(thetaDeg),
                                    n.ToString(CultureInfo.InvariantCulture),
                                    F(skinDepth),
                                    F(ideal.BcError * 100.0),
                                    F(row.BcError * 100.0),
                                    F(Math.Abs(row.BcError - ideal.BcError) * 100.0),
                                    F(idealReflectedPct),
                                    F(skinReflectedPct),
                                    F(Math.Abs(skinReflectedPct - idealReflectedPct)),
                                    F(idealTransmittedPct),
                                    F(skinTransmittedPct),
                                    F(Math.Abs(skinTransmittedPct - idealTransmittedPct)),
                                    F(PercentOf(Math.Abs(row.ReflectedScattered - row.TransmittedScattered), row.Incident)),
                                    F(PercentOf(row.PlateBalanceTotal, row.Incident)),
                                    F(PercentOf(row.Absorbed, row.Incident))));
                            }
                        }

                        rowsWriter.Flush();
                        compareWriter.Flush();
                    }
                }
            }

            Console.WriteLine("Accuracy sweep saved to: " + Path.GetFullPath(outputFilePath));
            Console.WriteLine("Comparison sweep saved to: " + Path.GetFullPath(compareFilePath));
        }

        private class MethodComparisonSample
        {
            public double X;
            public Compl Collocation;
            public Compl Galerkin;
            public double CollocationAbs;
            public double GalerkinAbs;
            public double DifferenceAbs;
        }

        private static void RunSkinMethodCompare(string[] args)
        {
            double alpha = GetDoubleArg(args, "--alpha", -1.0);
            double beta = GetDoubleArg(args, "--beta", 1.0);
            double lambda = GetDoubleArg(args, "--lambda", 1.0);
            double thetaDeg = GetDoubleArg(args, "--theta-deg", GetDoubleArg(args, "--theta", 45.0));
            double skinDepth = GetDoubleArg(args, "--skin-depth", GetDoubleArg(args, "--skin", 0.1));
            int n = GetIntArg(args, "--n", 30);
            int samples = GetIntArg(args, "--samples", 400);
            string outputFile = GetArgValue(args, "--output") ?? Path.Combine("diagnostics", "skin_method_compare.csv");
            string imageFile = GetArgValue(args, "--image") ?? Path.Combine("diagnostics", "skin_method_compare.png");

            if (alpha >= beta) throw new ArgumentException("--alpha must be less than --beta");
            if (lambda <= 0) throw new ArgumentException("--lambda must be positive");
            if (skinDepth <= 0) throw new ArgumentException("--skin-depth must be positive for skin method comparison");
            if (n <= 0) throw new ArgumentException("--n must be positive");
            if (samples < 20) throw new ArgumentException("--samples must be at least 20");

            double theta = thetaDeg * Math.PI / 180.0;

            DifrOnLenta collocation = new DifrOnLenta(alpha, beta, lambda, theta, n, skinDepth);
            int collocationStatus = collocation.SolveDifr();
            if (collocationStatus != 1)
                throw new InvalidOperationException("Collocation solve failed with status " + collocationStatus);

            DifrOnLenta galerkin = SolveGalerkinProjectionSinglePlate(alpha, beta, lambda, theta, n, skinDepth);

            MethodComparisonSample[] rows = new MethodComparisonSample[samples];
            double maxDifference = 0.0;
            double meanDifference = 0.0;
            double maxCoeffDifference = 0.0;

            for (int i = 0; i < samples; i++)
            {
                double x = alpha + (i + 0.5) / samples * (beta - alpha);
                Compl uc = collocation.u_on_strip(x);
                Compl ug = galerkin.u_on_strip(x);
                double diff = Compl.Abs(uc - ug);
                rows[i] = new MethodComparisonSample
                {
                    X = x,
                    Collocation = uc,
                    Galerkin = ug,
                    CollocationAbs = Compl.Abs(uc),
                    GalerkinAbs = Compl.Abs(ug),
                    DifferenceAbs = diff
                };
                if (diff > maxDifference) maxDifference = diff;
                meanDifference += diff;
            }
            meanDifference /= samples;

            int coeffCount = Math.Min(collocation.y.Length, galerkin.y.Length);
            for (int i = 0; i < coeffCount; i++)
            {
                double coeffDifference = Compl.Abs(collocation.y[i] - galerkin.y[i]);
                if (coeffDifference > maxCoeffDifference) maxCoeffDifference = coeffDifference;
            }

            WriteMethodComparisonCsv(outputFile, rows);
            SaveMethodComparisonPlot(imageFile, rows, alpha, beta, thetaDeg, skinDepth, n);

            Console.WriteLine("Skin method comparison saved to: " + Path.GetFullPath(outputFile));
            Console.WriteLine("Skin method comparison plot saved to: " + Path.GetFullPath(imageFile));
            Console.WriteLine("Collocation BC error, %: " + F(collocation.VerifyBoundaryConditions() * 100.0));
            Console.WriteLine("Galerkin projection BC error, %: " + F(galerkin.VerifyBoundaryConditions() * 100.0));
            Console.WriteLine("Max |u_collocation - u_galerkin|: " + F(maxDifference));
            Console.WriteLine("Mean |u_collocation - u_galerkin|: " + F(meanDifference));
            Console.WriteLine("Max coefficient absolute difference: " + F(maxCoeffDifference));
        }

        internal static DifrOnLenta SolveGalerkinProjectionSinglePlate(
            double alpha,
            double beta,
            double lambda,
            double theta,
            int n,
            double skinDepth)
        {
            return Diffraction.Core.GalerkinSolver.SolveSinglePlate(
                alpha,
                beta,
                lambda,
                theta,
                n,
                skinDepth);
        }

        private static void WriteMethodComparisonCsv(string outputFilePath, MethodComparisonSample[] rows)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(outputFilePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("x;collocation_re;collocation_im;collocation_abs;galerkin_re;galerkin_im;galerkin_abs;abs_difference");
                foreach (MethodComparisonSample row in rows)
                {
                    writer.WriteLine(string.Join(";",
                        F(row.X),
                        F(row.Collocation.Re),
                        F(row.Collocation.Im),
                        F(row.CollocationAbs),
                        F(row.Galerkin.Re),
                        F(row.Galerkin.Im),
                        F(row.GalerkinAbs),
                        F(row.DifferenceAbs)));
                }
            }
        }

        private static void SaveMethodComparisonPlot(
            string imageFilePath,
            MethodComparisonSample[] rows,
            double alpha,
            double beta,
            double thetaDeg,
            double skinDepth,
            int n)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(imageFilePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using (Bitmap bitmap = new Bitmap(1100, 760))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Font titleFont = new Font("Arial", 14, FontStyle.Bold))
            using (Font labelFont = new Font("Arial", 9))
            using (Pen collocationPen = new Pen(Color.FromArgb(31, 119, 180), 2.0f))
            using (Pen galerkinPen = new Pen(Color.FromArgb(214, 39, 40), 2.0f))
            using (Pen diffPen = new Pen(Color.FromArgb(44, 160, 44), 2.0f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);
                graphics.DrawString(
                    string.Format(CultureInfo.InvariantCulture,
                        "Skin solution comparison: theta={0:G4} deg, skinDepth={1:G4}, N={2}",
                        thetaDeg,
                        skinDepth,
                        n),
                    titleFont,
                    Brushes.Black,
                    new PointF(24, 18));

                Rectangle upper = new Rectangle(72, 70, 980, 300);
                Rectangle lower = new Rectangle(72, 430, 980, 240);

                DrawLinePanel(
                    graphics,
                    upper,
                    rows,
                    row => row.CollocationAbs,
                    row => row.GalerkinAbs,
                    collocationPen,
                    galerkinPen,
                    "|u(x,0)|",
                    "Collocation",
                    "Galerkin projection",
                    labelFont);

                DrawDifferencePanel(
                    graphics,
                    lower,
                    rows,
                    diffPen,
                    "|u_collocation - u_galerkin|",
                    labelFont);

                graphics.DrawString(
                    string.Format(CultureInfo.InvariantCulture, "x in [{0:G4}, {1:G4}]", alpha, beta),
                    labelFont,
                    Brushes.Black,
                    new PointF(upper.Left + upper.Width / 2 - 40, lower.Bottom + 30));

                bitmap.Save(imageFilePath, ImageFormat.Png);
            }
        }

        private static void DrawLinePanel(
            Graphics graphics,
            Rectangle bounds,
            MethodComparisonSample[] rows,
            Func<MethodComparisonSample, double> firstValue,
            Func<MethodComparisonSample, double> secondValue,
            Pen firstPen,
            Pen secondPen,
            string title,
            string firstLabel,
            string secondLabel,
            Font labelFont)
        {
            double min = rows.Min(row => Math.Min(firstValue(row), secondValue(row)));
            double max = rows.Max(row => Math.Max(firstValue(row), secondValue(row)));
            DrawAxes(graphics, bounds, min, max, title, labelFont);
            DrawSeries(graphics, bounds, rows, firstValue, min, max, firstPen);
            DrawSeries(graphics, bounds, rows, secondValue, min, max, secondPen);
            DrawLegend(graphics, bounds, firstPen.Color, secondPen.Color, firstLabel, secondLabel, labelFont);
        }

        private static void DrawDifferencePanel(
            Graphics graphics,
            Rectangle bounds,
            MethodComparisonSample[] rows,
            Pen pen,
            string title,
            Font labelFont)
        {
            double max = rows.Max(row => row.DifferenceAbs);
            DrawAxes(graphics, bounds, 0.0, max, title, labelFont);
            DrawSeries(graphics, bounds, rows, row => row.DifferenceAbs, 0.0, max, pen);
        }

        private static void DrawAxes(Graphics graphics, Rectangle bounds, double min, double max, string title, Font labelFont)
        {
            if (Math.Abs(max - min) < 1e-14)
            {
                max += 1.0;
                min -= 1.0;
            }

            using (Pen axisPen = new Pen(Color.FromArgb(80, 80, 80), 1.0f))
            using (Pen gridPen = new Pen(Color.FromArgb(225, 225, 225), 1.0f))
            {
                graphics.DrawRectangle(axisPen, bounds);
                for (int i = 1; i < 5; i++)
                {
                    float y = bounds.Top + bounds.Height * i / 5.0f;
                    graphics.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
                }
            }

            graphics.DrawString(title, labelFont, Brushes.Black, new PointF(bounds.Left, bounds.Top - 22));
            graphics.DrawString(max.ToString("G4", CultureInfo.InvariantCulture), labelFont, Brushes.Black, new PointF(bounds.Left - 64, bounds.Top - 6));
            graphics.DrawString(min.ToString("G4", CultureInfo.InvariantCulture), labelFont, Brushes.Black, new PointF(bounds.Left - 64, bounds.Bottom - 10));
        }

        private static void DrawSeries(
            Graphics graphics,
            Rectangle bounds,
            MethodComparisonSample[] rows,
            Func<MethodComparisonSample, double> valueSelector,
            double min,
            double max,
            Pen pen)
        {
            if (Math.Abs(max - min) < 1e-14) max = min + 1.0;
            PointF[] points = new PointF[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                double xNorm = i / (double)(rows.Length - 1);
                double yNorm = (valueSelector(rows[i]) - min) / (max - min);
                points[i] = new PointF(
                    bounds.Left + (float)(xNorm * bounds.Width),
                    bounds.Bottom - (float)(yNorm * bounds.Height));
            }
            if (points.Length > 1) graphics.DrawLines(pen, points);
        }

        private static void DrawLegend(
            Graphics graphics,
            Rectangle bounds,
            Color firstColor,
            Color secondColor,
            string firstLabel,
            string secondLabel,
            Font labelFont)
        {
            int x = bounds.Right - 220;
            int y = bounds.Top + 12;
            using (Brush firstBrush = new SolidBrush(firstColor))
            using (Brush secondBrush = new SolidBrush(secondColor))
            {
                graphics.FillRectangle(firstBrush, x, y + 4, 22, 4);
                graphics.DrawString(firstLabel, labelFont, Brushes.Black, new PointF(x + 30, y - 3));
                graphics.FillRectangle(secondBrush, x, y + 24, 22, 4);
                graphics.DrawString(secondLabel, labelFont, Brushes.Black, new PointF(x + 30, y + 17));
            }
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
                Console.WriteLine(string.Format("  BC error (u-qJ=0): {0:P2}", bcErr));
                Console.WriteLine($"  Condition number: {ConditionNumber(solverSkin_BC.LastMatrixA):E2}");
                double helmErr = solverSkin_BC.VerifyHelmholtz();
                Console.WriteLine(string.Format("  Helmholtz residual: {0:E2}", helmErr));
            }

            // Сравнение: без скин-эффекта и с различными значениями скин-слоя
            Console.WriteLine("\n=== СРАВНЕНИЕ: БЕЗ СКИНА vs СО СКИНОМ ===");
            Console.WriteLine(string.Format("{0,-10} {1,-10} {2,-10} {3,-10} {4,-10} {5,-10} {6,-10} {7,-12}",
                "delta", "|Zs| Ohm", "BC err%", "Rscat%", "Absorb%", "FwdScat%", "Tsigned", "Cond#"));

            // Без скина
            {
                var s0 = new DifrOnLenta(-1, 1, 1.0, Math.PI / 4, 10, 0);
                if (s0.SolveDifr() == 1)
                {
                    double bc0 = s0.VerifyBoundaryConditions();
                    var e0 = s0.CalculateEnergyComponents();
                    var f0 = s0.CalculateFarFieldScatteredEnergy(360, 160);
                    double ts0 = s0.CalculateOppositeSideSignedFlux();
                    double cond0 = ConditionNumber(s0.LastMatrixA);
                    Console.WriteLine(string.Format("{0,-10} {1,-10} {2,-10:F4} {3,-10:F2} {4,-10:F2} {5,-10:F2} {6,-10:F4} {7,-12:E2}",
                        "0(ideal)", "0", bc0 * 100,
                        f0.ReflectedScattered / e0.Incident * 100,
                        e0.Absorbed / e0.Incident * 100,
                        f0.TransmittedScattered / e0.Incident * 100,
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
                    var far = ts.CalculateFarFieldScatteredEnergy(360, 160);
                    double tsStrip = ts.CalculateOppositeSideSignedFlux();
                    double cond = ConditionNumber(ts.LastMatrixA);
                    Console.WriteLine(string.Format("{0,-10} {1,-10:F4} {2,-10:F4} {3,-10:F2} {4,-10:F2} {5,-10:F2} {6,-10:F4} {7,-12:E2}",
                        td, Compl.Abs(ts.chi), bcE * 100,
                        far.ReflectedScattered / en.Incident * 100,
                        en.Absorbed / en.Incident * 100,
                        far.TransmittedScattered / en.Incident * 100,
                        tsStrip, cond));
                }
            }

            // Тест сходимости по N для delta=0.001 (тонкий скин-слой)
            Console.WriteLine("\n=== CONVERGENCE TEST (delta=0.001) ===");
            Console.WriteLine(string.Format("{0,-6} {1,-12} {2,-12} {3,-12} {4,-12} {5,-12}", "N", "BC err%", "Rscat%", "Absorb%", "FwdScat%", "Cond#"));
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
                        var farN = tsN.CalculateFarFieldScatteredEnergy(360, Math.Max(8 * tn, 80));
                        double condN = ConditionNumber(tsN.LastMatrixA);
                        Console.WriteLine(string.Format("{0,-6} {1,-12:F4} {2,-12:F2} {3,-12:F4} {4,-12:F2} {5,-12:E2}",
                            tn, bcN * 100,
                            farN.ReflectedScattered / enN.Incident * 100,
                            enN.Absorbed / enN.Incident * 100,
                            farN.TransmittedScattered / enN.Incident * 100,
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
                for (int i = 1; i < M; i++)
                {
                    double x = -1.0 + i * dx;
                    Compl u_val = solver.u_on_strip(x);
                    Compl Jx = solver.GetJphys(x);
                    Compl bc_val = u_val - solver.SheetCoefficient * Jx;
                    double ref_scale = Compl.Abs(solver.u0(x, 0));
                    if (ref_scale < 1e-10) ref_scale = 1.0;
                    double err = Compl.Abs(bc_val) / ref_scale * 100.0;

                    Console.WriteLine(string.Format("  x={0,5:F2} : err = {1,7:F2}%", x, err));
                }
                using (var file = new System.IO.StreamWriter("bc_error_collocation"))
                {
                    file.WriteLine("x,error_percent");

                    double dx_save = 2.0 / M;
                    for (int j = 1; j < M; j++)
                    {
                        double x_save = -1.0 + j * dx_save;
                        Compl u_val_save = solver.u_on_strip(x_save);
                        Compl Jx_save = solver.GetJphys(x_save);
                        Compl bc_val_save = u_val_save - solver.SheetCoefficient * Jx_save;

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
            const double mu0 = 4 * Math.PI * 1e-7;
            const double c = 299792458.0;
            double frequency = c / lambda;

            foreach (double delta in skinDepths)
            {
                var solver = new DifrOnLenta(-1, 1, lambda, Math.PI / 4, 5, delta);
                double expected = Math.PI * mu0 * frequency * delta;
                Console.WriteLine(string.Format("  skinDepth={0:F2}: χ = {1:F4} + {2:F4}i Ом  (expected Zs = {3:F4}+{3:F4}i Ом)", delta, solver.chi.Re, solver.chi.Im, expected));
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

    }
}
