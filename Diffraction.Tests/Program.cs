using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Compl = Diffraction.Core.DiffractionMath.Compl;
using DifrOnLenta = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    public class DiffractionSolverTests
    {
        [TestMethod]
        public void ComplexArithmetic_ReturnsExpectedValues()
        {
            Compl value = new Compl(3, 4) - 2;

            Assert.AreEqual(1.0, value.Re, 1e-12, "real part");
            Assert.AreEqual(4.0, value.Im, 1e-12, "imaginary part");
            Assert.AreEqual(5.0, Compl.Abs(new Compl(3, 4)), 1e-12, "absolute value");
        }

        [TestMethod]
        public void Solver_RejectsOverlappingPlates()
        {
            Assert.ThrowsException<ArgumentException>(
                () => new DifrOnLenta(-1.0, 1.0, 0.5, 1.5, 1.0, 0.0, 10, 0.001),
                "overlapping plates must be rejected");
        }

        [TestMethod]
        public void Solver_AllowsTouchingPlates()
        {
            DifrOnLenta solver = new DifrOnLenta(-1.0, 0.0, 0.0, 1.0, 1.0, 0.0, 10, 0.001);

            Assert.AreEqual(2, solver.PlateCount, "touching plates must still be modeled as two plates");
            Assert.AreEqual(-1.0, solver.a, 1e-12, "left bound");
            Assert.AreEqual(1.0, solver.b, 1e-12, "right bound");
        }

        [DataTestMethod]
        [DataRow(1.0, 0.0, 1.0, 10, 0.0)]
        [DataRow(0, 1.0, 1.0, 10, -0.001)]
        [DataRow(-1.0, -1.0, 1.0, 10, 0.0)]
        [DataRow(-1.0, 1.0, 0.0, 10, 0.0)]
        [DataRow(-1.0, 1.0, 1.0, 0, 0.0)]
        public void Solver_RejectsInvalidSinglePlateParameters(double alpha, double beta, double lambda, int n, double skinDepth)
        {
            Assert.ThrowsException<ArgumentException>(
                () => new DifrOnLenta(alpha, beta, lambda, 0.0, n, skinDepth));
        }

        [DataTestMethod]
        [DataRow(0.001)]
        [DataRow(0.01)]
        [DataRow(0.1)]
        public void Conductivity_DecreasesWhenSkinDepthIncreases(double skinDepth)
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 10, skinDepth: skinDepth);

            double conductivity = solver.CalculateConductivity(skinDepth, 1.0);
            double thickerConductivity = solver.CalculateConductivity(skinDepth * 2.0, 1.0);

            Assert.IsTrue(conductivity > 0.0, "conductivity must be positive");
            Assert.IsTrue(thickerConductivity < conductivity, "larger skin depth must reduce conductivity in the current formula");
        }

        [TestMethod]
        public void SinglePlateSkin_StoresSurfaceImpedanceAndPassiveSheetCoefficient()
        {
            const double mu0 = 4 * Math.PI * 1e-7;
            const double c = 299792458.0;
            double lambda = 1.0;
            double skinDepth = 0.001;
            double frequency = c / lambda;
            double expectedSurfaceResistance = Math.PI * mu0 * frequency * skinDepth;
            double expectedSheetPart = skinDepth / 2.0;

            DifrOnLenta solver = new DifrOnLenta(-1.0, 1.0, lambda, 10.0 * Math.PI / 180.0, 20, skinDepth);

            Assert.AreEqual(expectedSurfaceResistance, solver.chi.Re, expectedSurfaceResistance * 1e-12, "chi real part in ohms");
            Assert.AreEqual(expectedSurfaceResistance, solver.chi.Im, expectedSurfaceResistance * 1e-12, "chi imaginary part in ohms");
            Assert.AreEqual(expectedSheetPart, solver.SheetCoefficient.Re, 1e-12, "sheet coefficient real part");
            Assert.AreEqual(-expectedSheetPart, solver.SheetCoefficient.Im, 1e-12, "passive sheet coefficient imaginary part");
        }

        [TestMethod]
        public void SheetCoefficient_DoesNotDependOnPlateGeometry()
        {
            double lambda = 1.0;
            double skinDepth = 0.001;
            DifrOnLenta single = new DifrOnLenta(-1.0, 1.0, lambda, 10.0 * Math.PI / 180.0, 20, skinDepth);
            DifrOnLenta two = new DifrOnLenta(-1.5, -0.5, 0.25, 1.75, lambda, 10.0 * Math.PI / 180.0, 20, skinDepth);

            Assert.AreEqual(single.SheetCoefficient.Re, two.SheetCoefficient.Re, 1e-15, "real part");
            Assert.AreEqual(single.SheetCoefficient.Im, two.SheetCoefficient.Im, 1e-15, "imaginary part");
        }

        [TestMethod]
        public void PlateIncidentEnergy_UsesOnlyProjectedPlateLengths()
        {
            double theta = 30.0 * Math.PI / 180.0;
            DifrOnLenta solver = new DifrOnLenta(-1.5, -0.5, 0.25, 1.75, 1.0, theta, 20, 0.001);
            double expected = 0.5 * (2.0 * Math.PI) * Math.Sin(theta) * (1.0 + 1.5);

            Assert.AreEqual(expected, solver.CalculatePlateIncidentEnergy(), expected * 1e-12, "plate-only incident flux");
        }

        [TestMethod]
        public void SinglePlateSolver_RemainsSupportedAfterLibraryExtraction()
        {
            DifrOnLenta solver = new DifrOnLenta(-1.0, 1.0, 1.0, 10.0 * Math.PI / 180.0, 20, 0.001);

            Assert.AreEqual(1, solver.SolveDifr(), "single plate solver failed");
            AssertFiniteAndNonNegative(solver.VerifyBoundaryConditions(), "boundary error");
            Assert.IsTrue(solver.VerifyBoundaryConditions() < 0.01, "single plate boundary error is unexpectedly high");
        }

        [TestMethod]
        public void ExternalCoefficientInjection_ReproducesCpuSolution()
        {
            DifrOnLenta cpu = CreateTwoPlateSolver(n: 20, skinDepth: 0.001, angleDeg: 30);
            Assert.AreEqual(1, cpu.SolveDifr(), "cpu solver failed");

            Compl[] copied = new Compl[cpu.y.Length];
            for (int i = 0; i < cpu.y.Length; i++)
                copied[i] = new Compl(cpu.y[i].Re, cpu.y[i].Im);

            DifrOnLenta injected = CreateTwoPlateSolver(n: 20, skinDepth: 0.001, angleDeg: 30);
            injected.ApplySolvedCoefficients(copied, "test-backend", 1.0, 2.0, 3.0, usedCuda: true);

            Assert.AreEqual(cpu.VerifyBoundaryConditions(), injected.VerifyBoundaryConditions(), 1e-12, "boundary error mismatch");
            Assert.AreEqual(3.0, injected.LastSolvePerformance.TotalMilliseconds, 1e-12, "timing mismatch");
            Assert.IsTrue(injected.LastSolvePerformance.UsedCuda, "backend flag mismatch");

            Compl cpuField = cpu.u(0.25, 0.1);
            Compl injectedField = injected.u(0.25, 0.1);
            Assert.AreEqual(0.0, Compl.Abs(cpuField - injectedField), 1e-12, "field mismatch");
        }

        [TestMethod]
        public void BoundaryError_ImprovesWhenNIncreases()
        {
            DifrOnLenta coarse = CreateTwoPlateSolver(n: 10, skinDepth: 0.001);
            DifrOnLenta fine = CreateTwoPlateSolver(n: 30, skinDepth: 0.001);

            Assert.AreEqual(1, coarse.SolveDifr(), "coarse solver failed");
            Assert.AreEqual(1, fine.SolveDifr(), "fine solver failed");

            double coarseError = coarse.VerifyBoundaryConditions();
            double fineError = fine.VerifyBoundaryConditions();

            Assert.IsTrue(fineError < coarseError, "boundary error must improve when N increases");
            Assert.IsTrue(fineError < 0.001, "boundary error must stay below 0.1%");
        }


        [DataTestMethod]
        [DataRow(30, 0.001)]
        [DataRow(45, 0.01)]
        [DataRow(90, 0.01)]
        [DataRow(45, 0.1)]
        public void AbsorbedEnergy_CurrentAndBoundaryValueFormsAgree(double angleDeg, double skinDepth)
        {
            DifrOnLenta solver = CreateSinglePlateSolver(n: 30, skinDepth: skinDepth, angleDeg: angleDeg);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double absorbedByDerivative = solver.CalculateAbsorbedEnergy();
            double absorbedByBoundaryValue = solver.CalculateAbsorbedEnergyByBoundaryValue();
            double scale = Math.Max(absorbedByDerivative, 1e-12);

            AssertFiniteAndNonNegative(absorbedByDerivative, "absorbed energy by derivative");
            AssertFiniteAndNonNegative(absorbedByBoundaryValue, "absorbed energy by boundary value");
            double tolerance = skinDepth <= 0.001 ? 0.10 : 0.08;
            Assert.IsTrue(Math.Abs(absorbedByDerivative - absorbedByBoundaryValue) / scale < tolerance,
                "current and boundary-value absorption formulas must agree");
        }
        [DataTestMethod]
        [DataRow(0, 0.0)]
        [DataRow(0, 0.001)]
        [DataRow(0, 0.01)]
        [DataRow(30, 0.0)]
        [DataRow(30, 0.001)]
        [DataRow(30, 0.01)]
        [DataRow(60, 0.0)]
        [DataRow(60, 0.001)]
        [DataRow(60, 0.01)]
        [DataRow(90, 0.0)]
        [DataRow(90, 0.001)]
        [DataRow(90, 0.01)]
        public void BoundaryError_ImprovesAcrossAnglesAndThinSkinCases(double angleDeg, double skinDepth)
        {
            DifrOnLenta coarse = CreateTwoPlateSolver(n: 10, skinDepth: skinDepth, angleDeg: angleDeg);
            DifrOnLenta fine = CreateTwoPlateSolver(n: 30, skinDepth: skinDepth, angleDeg: angleDeg);

            Assert.AreEqual(1, coarse.SolveDifr(), "coarse solver failed");
            Assert.AreEqual(1, fine.SolveDifr(), "fine solver failed");

            double coarseError = coarse.VerifyBoundaryConditions();
            double fineError = fine.VerifyBoundaryConditions();

            Assert.IsTrue(fineError < coarseError, "boundary error must improve when N increases");
            Assert.IsTrue(fineError < 0.001, "fine boundary error must stay below 0.1%");
        }

        [DataTestMethod]
        [DataRow(0, 0.0, 0.001)]
        [DataRow(0, 0.001, 0.05)]
        [DataRow(0, 0.01, 0.1)]
        [DataRow(0, 0.1, 0.2)]
        [DataRow(30, 0.0, 0.001)]
        [DataRow(30, 0.001, 0.05)]
        [DataRow(30, 0.01, 0.1)]
        [DataRow(30, 0.1, 0.2)]
        [DataRow(60, 0.0, 0.001)]
        [DataRow(60, 0.001, 0.05)]
        [DataRow(60, 0.01, 0.1)]
        [DataRow(60, 0.1, 0.2)]
        [DataRow(90, 0.0, 0.001)]
        [DataRow(90, 0.001, 0.05)]
        [DataRow(90, 0.01, 0.1)]
        [DataRow(90, 0.1, 0.2)]
        public void BoundaryError_StaysWithinExpectedRangeAcrossParameterGrid(double angleDeg, double skinDepth, double maxError)
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 30, skinDepth: skinDepth, angleDeg: angleDeg);

            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double error = solver.VerifyBoundaryConditions();
            AssertFiniteAndNonNegative(error, "boundary error");
            Assert.IsTrue(error < maxError, "boundary error is unexpectedly high");
        }

        [DataTestMethod]
        [DataRow(30, 0.0)]
        [DataRow(30, 0.001)]
        [DataRow(45, 0.01)]
        [DataRow(60, 0.1)]
        [DataRow(90, 0.01)]
        public void LocalPlateEnergyBalance_ClosesWithoutRenormalization(double angleDeg, double skinDepth)
        {
            DifrOnLenta solver = CreateSinglePlateSolver(n: 30, skinDepth: skinDepth, angleDeg: angleDeg);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents();
            Assert.IsTrue(energy.Incident > 0.0, "plate incident energy must be positive");
            AssertFiniteAndNonNegative(energy.Absorbed, "absorbed energy");
            Assert.IsFalse(energy.WasRenormalized, "energy components must not be renormalized");
            Assert.IsTrue(Math.Abs(energy.LocalBalanceResidual) / energy.Incident < 0.015,
                "upper/lower flux difference must equal impedance absorption");
            Assert.IsTrue(EnergyBalanceRelativeError(energy) < 0.015, "local plate energy balance");
        }

        [TestMethod]
        public void LocalPlateEnergyBalance_ClosesForTwoPlates()
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 30, skinDepth: 0.01, angleDeg: 30.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents();
            Assert.IsTrue(Math.Abs(energy.LocalBalanceResidual) / energy.Incident < 0.02,
                "two-plate upper/lower flux balance");
        }

        [TestMethod]
        public void IdealSheet_BlocksNormalFluxAcrossPlate()
        {
            DifrOnLenta solver = CreateSinglePlateSolver(n: 30, skinDepth: 0.0, angleDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents();
            Assert.AreEqual(1.0, energy.Reflected / energy.Incident, 1e-4, "net local reflection");
            Assert.AreEqual(0.0, energy.Transmitted / energy.Incident, 1e-4, "normal flux through ideal sheet");
            Assert.AreEqual(0.0, energy.Absorbed, 1e-12, "ideal sheet absorption");
        }

        [TestMethod]
        public void IncreasingSkinDepth_ReducesNetReflectionAndRaisesFullTransmission()
        {
            DifrOnLenta thin = CreateSinglePlateSolver(n: 30, skinDepth: 0.01, angleDeg: 45.0);
            DifrOnLenta thick = CreateSinglePlateSolver(n: 30, skinDepth: 0.1, angleDeg: 45.0);
            Assert.AreEqual(1, thin.SolveDifr(), "thin solver failed");
            Assert.AreEqual(1, thick.SolveDifr(), "thick solver failed");

            var thinEnergy = thin.CalculateEnergyComponents();
            var thickEnergy = thick.CalculateEnergyComponents();
            Assert.IsTrue(thickEnergy.Reflected < thinEnergy.Reflected, "net reflection must decrease");
            Assert.IsTrue(thickEnergy.Transmitted > thinEnergy.Transmitted, "full transmission must increase");
        }

        [TestMethod]
        public void SignedControlContour_ClosesWithImpedanceAbsorption()
        {
            DifrOnLenta solver = CreateSinglePlateSolver(n: 30, skinDepth: 0.01, angleDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents(includeContourDiagnostic: true);
            Assert.IsTrue(Math.Abs(energy.SignedContourResidual) / energy.Incident < 0.02,
                "signed closed-contour flux plus absorption must vanish");
        }

        [TestMethod]
        public void NormalDerivativeJump_EqualsSurfaceCurrent()
        {
            DifrOnLenta solver = CreateSinglePlateSolver(n: 20, skinDepth: 0.01, angleDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            foreach (double x in new[] { -0.75, -0.25, 0.25, 0.75 })
            {
                Compl jump = solver.NormalDerivativeAbove(x) - solver.NormalDerivativeBelow(x);
                Assert.AreEqual(0.0, Compl.Abs(jump - solver.CurrentDensity(x)), 1e-12, "derivative jump");
            }
        }

        [DataTestMethod]
        [DataRow(0, 0.0)]
        [DataRow(30, 0.001)]
        [DataRow(60, 0.01)]
        [DataRow(90, 0.1)]
        public void HelmholtzResidual_StaysSmallAwayFromPlates(double angleDeg, double skinDepth)
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 20, skinDepth: skinDepth, angleDeg: angleDeg);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double residual = solver.VerifyHelmholtz();
            AssertFiniteAndNonNegative(residual, "Helmholtz residual");
            Assert.IsTrue(residual < 0.005, "Helmholtz residual is unexpectedly high");
        }

        [TestMethod]
        public void NativeCpuBackend_MatchesManagedSolver_ForSameParameters()
        {
            DifrOnLenta managed = new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, 10.0, 10.0 * Math.PI / 180.0, 10, 0.001);
            Assert.AreEqual(1, managed.SolveDifr(), "managed solver failed");

            NativeRunResult native = RunNativeCpu(
                "--alpha1", "-1.5",
                "--beta1", "-0.5",
                "--alpha2", "0.5",
                "--beta2", "1.5",
                "--lambda", "10",
                "--theta", (10.0 * Math.PI / 180.0).ToString("R", CultureInfo.InvariantCulture),
                "--n", "10",
                "--skin-depth", "0.001");

            Assert.IsTrue(native.Success, native.Output);
            Assert.AreEqual(managed.y.Length, native.Coefficients.Count, "coefficient count mismatch");

            for (int i = 0; i < managed.y.Length; i++)
            {
                Assert.AreEqual(managed.y[i].Re, native.Coefficients[i].Re, 1e-12, $"real mismatch at coeff_{i}");
                Assert.AreEqual(managed.y[i].Im, native.Coefficients[i].Im, 1e-12, $"imag mismatch at coeff_{i}");
            }
        }

        [TestMethod]
        public void NativeCpuBackend_ThetaDegreesFlagMatchesManagedSolver()
        {
            DifrOnLenta managed = new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, 10.0, 10.0 * Math.PI / 180.0, 10, 0.001);
            Assert.AreEqual(1, managed.SolveDifr(), "managed solver failed");

            NativeRunResult native = RunNativeCpu(
                "--alpha1", "-1.5",
                "--beta1", "-0.5",
                "--alpha2", "0.5",
                "--beta2", "1.5",
                "--lambda", "10",
                "--theta-deg", "10",
                "--n", "10",
                "--skin-depth", "0.001");

            Assert.IsTrue(native.Success, native.Output);
            Assert.IsTrue(native.ThetaDegrees.HasValue && Math.Abs(native.ThetaDegrees.Value - 10.0) < 1e-12, "theta in degrees");
            Assert.IsTrue(native.ThetaRadians.HasValue && Math.Abs(native.ThetaRadians.Value - 10.0 * Math.PI / 180.0) < 1e-12, "theta in radians");

            for (int i = 0; i < managed.y.Length; i++)
            {
                Assert.AreEqual(managed.y[i].Re, native.Coefficients[i].Re, 1e-12, $"real mismatch at coeff_{i}");
                Assert.AreEqual(managed.y[i].Im, native.Coefficients[i].Im, 1e-12, $"imag mismatch at coeff_{i}");
            }
        }

        [TestMethod]
        public void NativeCpuBackend_RejectsSuspiciousDegreeValuePassedAsRadians()
        {
            NativeRunResult native = RunNativeCpu(
                "--alpha1", "-1.5",
                "--beta1", "-0.5",
                "--alpha2", "0.5",
                "--beta2", "1.5",
                "--lambda", "10",
                "--theta", "10",
                "--n", "10",
                "--skin-depth", "0.001");

            Assert.IsFalse(native.Success, "native run must fail for suspicious degree input");
            StringAssert.Contains(native.Output, "--theta-deg", "error must explicitly suggest the degrees flag");
        }

        [TestMethod]
        public void NativeCpuBackend_MQuadFlag_IsReportedInOutput()
        {
            NativeRunResult native = RunNativeCpu(
                "--alpha1", "-1.5",
                "--beta1", "-0.5",
                "--alpha2", "0.5",
                "--beta2", "1.5",
                "--lambda", "10",
                "--theta-deg", "10",
                "--n", "10",
                "--m-quad", "40",
                "--skin-depth", "0.001");

            Assert.IsTrue(native.Success, native.Output);
            Assert.AreEqual(40, native.MQuad, "custom M must be reported back");
        }

        [TestMethod]
        public void NativeCpuBackend_RejectsInvalidMQuad()
        {
            NativeRunResult native = RunNativeCpu(
                "--alpha1", "-1.5",
                "--beta1", "-0.5",
                "--alpha2", "0.5",
                "--beta2", "1.5",
                "--lambda", "10",
                "--theta-deg", "10",
                "--n", "10",
                "--m-quad", "-1",
                "--skin-depth", "0.001");

            Assert.IsFalse(native.Success, "native run must fail for invalid M");
            StringAssert.Contains(native.Output, "M", "error must mention M");
        }

        private static DifrOnLenta CreateTwoPlateSolver(int n, double skinDepth, double angleDeg = 10.0)
        {
            double theta = angleDeg * Math.PI / 180.0;
            return new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, 1.0, theta, n, skinDepth);
        }

        private static DifrOnLenta CreateSinglePlateSolver(int n, double skinDepth, double angleDeg = 10.0)
        {
            double theta = angleDeg * Math.PI / 180.0;
            return new DifrOnLenta(-1.0, 1.0, 1.0, theta, n, skinDepth);
        }

        private static double EnergyBalanceRelativeError(DifrOnLenta.EnergyComponents energy)
        {
            double total = energy.Reflected + energy.Transmitted + energy.Absorbed;
            return Math.Abs(total - energy.Incident) / energy.Incident;
        }

        private static void AssertFiniteAndNonNegative(double value, string message)
        {
            Assert.IsFalse(double.IsNaN(value), message + " must not be NaN");
            Assert.IsFalse(double.IsInfinity(value), message + " must not be Infinity");
            Assert.IsTrue(value >= 0.0, message + " must be non-negative");
        }

        private sealed class NativeRunResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public Dictionary<int, Compl> Coefficients { get; } = new Dictionary<int, Compl>();
            public double? ThetaRadians { get; set; }
            public double? ThetaDegrees { get; set; }
            public int? MQuad { get; set; }
        }

        private static NativeRunResult RunNativeCpu(params string[] arguments)
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string buildScript = Path.Combine(repoRoot, "Diffraction.Cpp", "build_cpu.bat");
            string exePath = Path.Combine(repoRoot, "Diffraction.Cpp", "build", "DiffractionCpu.exe");
            string sourcePath = Path.Combine(repoRoot, "Diffraction.Cpp", "src", "DiffractionCpu.cpp");

            bool rebuildRequired = !File.Exists(exePath) || File.GetLastWriteTimeUtc(exePath) < File.GetLastWriteTimeUtc(sourcePath);
            if (rebuildRequired)
            {
                ProcessStartInfo buildStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{buildScript}\"",
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process build = Process.Start(buildStartInfo))
                {
                    string buildOutput = build.StandardOutput.ReadToEnd() + build.StandardError.ReadToEnd();
                    build.WaitForExit();
                    if (build.ExitCode != 0 || !File.Exists(exePath))
                        Assert.Inconclusive("Native CPU backend is not available: " + buildOutput);
                }
            }

            string joinedArgs = string.Join(" ", Array.ConvertAll(arguments, QuoteArgument));
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = joinedArgs,
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
                NativeRunResult result = new NativeRunResult
                {
                    Success = process.ExitCode == 0 && output.Contains("status=ok"),
                    Output = output
                };

                using (StringReader reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("coeff_", StringComparison.Ordinal))
                        {
                            int equalsIndex = line.IndexOf('=');
                            int commaIndex = line.IndexOf(',', equalsIndex + 1);
                            int index = int.Parse(line.Substring(6, equalsIndex - 6), CultureInfo.InvariantCulture);
                            double re = double.Parse(line.Substring(equalsIndex + 1, commaIndex - equalsIndex - 1), CultureInfo.InvariantCulture);
                            double im = double.Parse(line.Substring(commaIndex + 1), CultureInfo.InvariantCulture);
                            result.Coefficients[index] = new Compl(re, im);
                        }
                        else if (line.StartsWith("theta_rad=", StringComparison.Ordinal))
                        {
                            result.ThetaRadians = double.Parse(line.Substring("theta_rad=".Length), CultureInfo.InvariantCulture);
                        }
                        else if (line.StartsWith("theta_deg=", StringComparison.Ordinal))
                        {
                            result.ThetaDegrees = double.Parse(line.Substring("theta_deg=".Length), CultureInfo.InvariantCulture);
                        }
                        else if (line.StartsWith("m_quad=", StringComparison.Ordinal))
                        {
                            result.MQuad = int.Parse(line.Substring("m_quad=".Length), CultureInfo.InvariantCulture);
                        }
                    }
                }

                return result;
            }
        }

        private static string QuoteArgument(string value)
        {
            return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
        }
    }
}
