using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Compl = Diffraction.Core.DiffractionMath.Compl;
using DifrOnLenta = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    public class NativeKernelCompatibilityTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void BridgeAcceptsCurrentModelAndFiniteCoefficients()
        {
            CudaSolverBridge.SolveResponse response = ParseResponse(
                "status=ok\nmodel=thin_sheet_v4\nbackend=test\nassembly_ms=1.25\nsolve_ms=2\ntotal_ms=3.25\n"
                + "coeff_1=-2,3\ncoeff_0=1,-0.5\n", 2);

            Assert.IsTrue(response.Success, response.ErrorMessage);
            Assert.AreEqual("test", response.BackendName);
            Assert.AreEqual(2, response.Coefficients.Length);
            Assert.AreEqual(1.0, response.Coefficients[0].Re);
            Assert.AreEqual(-0.5, response.Coefficients[0].Im);
            Assert.AreEqual(-2.0, response.Coefficients[1].Re);
            Assert.AreEqual(3.0, response.Coefficients[1].Im);
            Assert.AreEqual(1.25, response.AssemblyMilliseconds);
            Assert.AreEqual(2.0, response.LinearSolveMilliseconds);
            Assert.AreEqual(3.25, response.TotalMilliseconds);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("thin_sheet_v1")]
        [DataRow("thin_sheet_v2")]
        [DataRow("thin_sheet_v3")]
        [DataRow("thin_sheet_v5")]
        public void BridgeRejectsMissingOrIncompatibleModel(string model)
        {
            string modelLine = model.Length == 0 ? "" : "model=" + model + "\n";
            AssertRejected(ParseResponse("status=ok\n" + modelLine + "coeff_0=1,0\n", 1));
        }

        [DataTestMethod]
        [DataRow("NaN,0")]
        [DataRow("0,NaN")]
        [DataRow("nan,nan")]
        [DataRow("Infinity,0")]
        [DataRow("0,-Infinity")]
        [DataRow("inf,0")]
        [DataRow("1e999,0")]
        [DataRow("1")]
        [DataRow("1,2,3")]
        public void BridgeRejectsNonFiniteOrMalformedCoefficients(string coefficient)
        {
            AssertRejected(ParseResponse("status=ok\nmodel=thin_sheet_v4\ncoeff_0=" + coefficient + "\n", 1));
        }

        [DataTestMethod]
        [DataRow("coeff_1=2,0\n")]
        [DataRow("coeff_-1=2,0\n")]
        [DataRow("coeff_bad=2,0\n")]
        [DataRow("coeff_0=2,0\n")]
        [DataRow("coeff_0=NaN,0\ncoeff_0=2,0\n")]
        public void BridgeRejectsUnexpectedOrDuplicateCoefficients(string extra)
        {
            AssertRejected(ParseResponse("status=ok\nmodel=thin_sheet_v4\ncoeff_0=1,0\n" + extra, 1));
        }

        [TestMethod]
        public void BridgeRejectsMissingCoefficientWithoutExposingPartialSolution()
        {
            AssertRejected(ParseResponse("status=ok\nmodel=thin_sheet_v4\ncoeff_0=1,0\n", 2));
        }

        [DataTestMethod]
        [DataRow("assembly_ms=NaN")]
        [DataRow("solve_ms=Infinity")]
        [DataRow("total_ms=-Infinity")]
        [DataRow("total_ms=1e999")]
        public void BridgeRejectsNonFiniteTimings(string timing)
        {
            AssertRejected(ParseResponse("status=ok\nmodel=thin_sheet_v4\ncoeff_0=1,0\n" + timing + "\n", 1));
        }

        [TestMethod]
        public void BridgeRejectsErrorStatus()
        {
            CudaSolverBridge.SolveResponse response = ParseResponse(
                "status=error\nmodel=thin_sheet_v4\nmessage=native failure\ncoeff_0=1,0\n", 1);
            AssertRejected(response);
            Assert.AreEqual("native failure", response.ErrorMessage);
        }

        [DataTestMethod]
        [TestCategory("NativeCpu")]
        [DataRow(100.0, 10.0, 10, 0.0)]
        [DataRow(10.0, 10.0, 10, 0.0)]
        [DataRow(0.2, 45.0, 24, 0.0)]
        [DataRow(10.0, 10.0, 1, 0.001)]
        [DataRow(10.0, 10.0, 10, 0.001)]
        [DataRow(6.283185307179586, 30.0, 12, 0.001)]
        [DataRow(1.0, 45.0, 20, 0.001)]
        [DataRow(0.2, 45.0, 24, 0.01)]
        [DataRow(0.1, 45.0, 32, 0.01)]
        public void NativeCpuMatchesManagedKernel(double lambda, double angle, int n, double skinDepth)
        {
            AssertMatchesManagedKernel("CPU", lambda, angle, n, skinDepth);
        }

        [DataTestMethod]
        [TestCategory("NativeCuda")]
        [DataRow(100.0, 10.0, 10, 0.0)]
        [DataRow(10.0, 10.0, 10, 0.001)]
        [DataRow(6.283185307179586, 30.0, 12, 0.001)]
        [DataRow(1.0, 45.0, 20, 0.001)]
        [DataRow(0.2, 45.0, 24, 0.01)]
        [DataRow(0.1, 45.0, 32, 0.01)]
        public void NativeCudaMatchesManagedKernel(double lambda, double angle, int n, double skinDepth)
        {
            AssertMatchesManagedKernel("CUDA", lambda, angle, n, skinDepth);
        }

        private void AssertMatchesManagedKernel(string backend, double lambda, double angle, int n, double skinDepth)
        {
            NativeResult native = RunNative(backend,
                "--alpha1", "-1.5", "--beta1", "-0.5", "--alpha2", "0.5", "--beta2", "1.5",
                "--lambda", Format(lambda), "--theta-deg", Format(angle), "--n", n.ToString(CultureInfo.InvariantCulture),
                "--skin-depth", Format(skinDepth));
            if (backend == "CUDA" && native.ExitCode != 0 && IsCudaUnavailable(native.Error))
                Assert.Inconclusive("CUDA device unavailable: " + native.Error);

            Assert.AreEqual(0, native.ExitCode, native.Output + native.Error);
            CudaSolverBridge.SolveResponse response = ParseResponse(native.Output, 2 * n);
            Assert.IsTrue(response.Success, response.ErrorMessage + "\n" + native.Output);

            DifrOnLenta managed = new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, lambda, angle * Math.PI / 180.0, n, skinDepth);
            Assert.AreEqual(1, managed.SolveDifr(), "Managed solver failed");
            double maxAbsoluteError = 0.0;
            double maxScaledError = 0.0;
            for (int i = 0; i < managed.y.Length; ++i)
            {
                Compl expected = managed.y[i];
                Compl actual = response.Coefficients[i];
                AssertFinite(expected.Re);
                AssertFinite(expected.Im);
                AssertFinite(actual.Re);
                AssertFinite(actual.Im);
                double absoluteError = Compl.Abs(actual - expected);
                double scaledError = absoluteError / (1.0 + Compl.Abs(expected));
                maxAbsoluteError = Math.Max(maxAbsoluteError, absoluteError);
                maxScaledError = Math.Max(maxScaledError, scaledError);
                Assert.IsTrue(scaledError < 1e-9,
                    backend + " coefficient " + i + ": scaled error " + Format(scaledError));
            }
            TestContext.WriteLine("{0}: lambda={1:G17}, n={2}, max_abs={3:E6}, max_scaled={4:E6}",
                backend, lambda, n, maxAbsoluteError, maxScaledError);
        }

        [TestMethod]
        [TestCategory("NativeCpu")]
        public void NativeCpuRejectsNonFiniteParameters()
        {
            AssertRejectsNonFiniteParameters("CPU");
        }

        [TestMethod]
        [TestCategory("NativeCuda")]
        public void NativeCudaRejectsNonFiniteParameters()
        {
            AssertRejectsNonFiniteParameters("CUDA");
        }

        private static void AssertRejectsNonFiniteParameters(string backend)
        {
            foreach (string key in new[] { "--alpha1", "--beta1", "--alpha2", "--beta2", "--lambda", "--theta", "--theta-deg", "--skin-depth" })
            {
                foreach (string value in new[] { "nan", "inf", "-inf" })
                {
                    NativeResult native = RunNative(backend, key, value);
                    AssertNativeRejected(native);
                    StringAssert.Contains(native.Error, "finite", backend + " " + key + " " + value);
                }
            }
        }

        [TestMethod]
        [TestCategory("NativeCpu")]
        public void NativeCpuRejectsOverflowingDerivedParameters()
        {
            AssertRejectsOverflowingDerivedParameters("CPU");
        }

        [TestMethod]
        [TestCategory("NativeCuda")]
        public void NativeCudaRejectsOverflowingDerivedParameters()
        {
            AssertRejectsOverflowingDerivedParameters("CUDA");
        }

        private static void AssertRejectsOverflowingDerivedParameters(string backend)
        {
            AssertNativeRejected(RunNative(backend, "--lambda", "1e-300", "--skin-depth", "0.01"));
            AssertNativeRejected(RunNative(backend, "--skin-depth", "1e308"));
            AssertNativeRejected(RunNative(backend, "--alpha1", "-1e308", "--beta1", "-9e307"));
            AssertNativeRejected(RunNative(backend, "--n", "2147483647"));
            AssertNativeRejected(RunNative(backend, "--m-quad", "2147483647"));
        }

        private static CudaSolverBridge.SolveResponse ParseResponse(string output, int count)
        {
            MethodInfo parse = typeof(CudaSolverBridge).GetMethod("ParseSolveOutput", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(parse);
            return (CudaSolverBridge.SolveResponse)parse.Invoke(null, new object[] { output, count });
        }

        private static void AssertRejected(CudaSolverBridge.SolveResponse response)
        {
            Assert.IsFalse(response.Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(response.ErrorMessage));
            Assert.IsNull(response.Coefficients, "Rejected output must not expose a partial solution");
        }

        private static void AssertFinite(double value)
        {
            Assert.IsFalse(double.IsNaN(value) || double.IsInfinity(value), "Non-finite coefficient");
        }

        private static void AssertNativeRejected(NativeResult native)
        {
            Assert.AreNotEqual(0, native.ExitCode, native.Output + native.Error);
            StringAssert.Contains(native.Error, "status=error");
            Assert.IsFalse(native.Output.Contains("status=ok"), native.Output);
            Assert.IsFalse(native.Output.Contains("coeff_"), native.Output);
        }

        private static bool IsCudaUnavailable(string error)
        {
            return error.IndexOf("no CUDA-capable device", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("CUDA driver version is insufficient", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Format(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private sealed class NativeResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }

        private static NativeResult RunNative(string backend, params string[] arguments)
        {
            string project = backend == "CPU" ? "Diffraction.Cpp" : "Diffraction.Cuda";
            string executable = backend == "CPU" ? "DiffractionCpu.exe" : "DiffractionCuda.exe";
            DirectoryInfo root = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, project)))
                root = root.Parent;
            Assert.IsNotNull(root, "Repository root not found");
            string path = Path.Combine(root.FullName, project, "build", executable);
            if (!File.Exists(path))
                Assert.Inconclusive("Build the native backend first: " + path);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = string.Join(" ", arguments),
                WorkingDirectory = Path.GetDirectoryName(path),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(90000))
                {
                    process.Kill();
                    process.WaitForExit();
                    Assert.Fail(backend + " timed out");
                }
                return new NativeResult
                {
                    ExitCode = process.ExitCode,
                    Output = stdout.GetAwaiter().GetResult(),
                    Error = stderr.GetAwaiter().GetResult()
                };
            }
        }
    }
}
