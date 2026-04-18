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
        [DataRow(0)]
        [DataRow(30)]
        [DataRow(60)]
        [DataRow(90)]
        public void ThinSkinBoundaryAndEnergyStayCloseToIdealCase(double angleDeg)
        {
            DifrOnLenta ideal = CreateTwoPlateSolver(n: 30, skinDepth: 0.0, angleDeg: angleDeg);
            DifrOnLenta skin = CreateTwoPlateSolver(n: 30, skinDepth: 0.001, angleDeg: angleDeg);

            Assert.AreEqual(1, ideal.SolveDifr(), "ideal solver failed");
            Assert.AreEqual(1, skin.SolveDifr(), "skin solver failed");

            double idealBoundary = ideal.VerifyBoundaryConditions();
            double skinBoundary = skin.VerifyBoundaryConditions();
            var idealEnergy = ideal.CalculateEnergyComponents();
            var skinEnergy = skin.CalculateEnergyComponents();

            Assert.IsTrue(Math.Abs(skinBoundary - idealBoundary) < 0.001, "thin-skin boundary error must stay close to the ideal case");
            AssertEnergyFractionsClose(idealEnergy.Reflected / idealEnergy.Incident, skinEnergy.Reflected / skinEnergy.Incident, 0.02, "reflected energy");
            AssertEnergyFractionsClose(idealEnergy.Transmitted / idealEnergy.Incident, skinEnergy.Transmitted / skinEnergy.Incident, 0.03, "transmitted energy");
            Assert.IsTrue(skinEnergy.Absorbed / skinEnergy.Incident < 0.01, "thin-skin absorbed energy must stay small");
        }

        [TestMethod]
        public void EnergyComponents_StayBalancedForTwoPlatesWithSkin()
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 30, skinDepth: 0.001);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents();
            double total = energy.Reflected + energy.Transmitted + energy.Absorbed;
            double reflectedFraction = energy.Reflected / energy.Incident;
            double transmittedFraction = energy.Transmitted / energy.Incident;
            double absorbedFraction = energy.Absorbed / energy.Incident;

            Assert.AreEqual(1.0, total / energy.Incident, 1e-10, "energy balance");
            Assert.IsTrue(reflectedFraction > 0.0 && reflectedFraction < 0.2, "reflected energy out of expected range");
            Assert.IsTrue(transmittedFraction > 0.7 && transmittedFraction < 1.0, "transmitted energy out of expected range");
            Assert.IsTrue(absorbedFraction > 0.0 && absorbedFraction < 0.02, "absorbed energy out of expected range");
        }

        [DataTestMethod]
        [DataRow(0, 0.0)]
        [DataRow(0, 0.001)]
        [DataRow(0, 0.01)]
        [DataRow(0, 0.1)]
        [DataRow(30, 0.0)]
        [DataRow(30, 0.001)]
        [DataRow(30, 0.01)]
        [DataRow(30, 0.1)]
        [DataRow(60, 0.0)]
        [DataRow(60, 0.001)]
        [DataRow(60, 0.01)]
        [DataRow(60, 0.1)]
        [DataRow(90, 0.0)]
        [DataRow(90, 0.001)]
        [DataRow(90, 0.01)]
        [DataRow(90, 0.1)]
        public void EnergyComponents_AreFinitePositiveAndBalancedAcrossParameterGrid(double angleDeg, double skinDepth)
        {
            DifrOnLenta solver = CreateTwoPlateSolver(n: 20, skinDepth: skinDepth, angleDeg: angleDeg);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            var energy = solver.CalculateEnergyComponents();
            double total = energy.Reflected + energy.Transmitted + energy.Absorbed;
            double reflectedFraction = energy.Reflected / energy.Incident;
            double transmittedFraction = energy.Transmitted / energy.Incident;
            double absorbedFraction = energy.Absorbed / energy.Incident;

            Assert.IsTrue(energy.Incident > 0.0, "incident energy must be positive");
            Assert.AreEqual(1.0, total / energy.Incident, 1e-10, "energy balance");
            Assert.IsTrue(reflectedFraction >= 0.0 && reflectedFraction < 0.5, "reflected energy out of expected range");
            Assert.IsTrue(transmittedFraction > 0.0 && transmittedFraction <= 1.0, "transmitted energy out of expected range");
            Assert.IsTrue(absorbedFraction >= 0.0 && absorbedFraction < 0.1, "absorbed energy out of expected range");
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

        private static void AssertFiniteAndNonNegative(double value, string message)
        {
            Assert.IsFalse(double.IsNaN(value), message + " must not be NaN");
            Assert.IsFalse(double.IsInfinity(value), message + " must not be Infinity");
            Assert.IsTrue(value >= 0.0, message + " must be non-negative");
        }

        private static void AssertEnergyFractionsClose(double expected, double actual, double tolerance, string message)
        {
            Assert.IsTrue(Math.Abs(expected - actual) < tolerance, message + " differs too much");
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
