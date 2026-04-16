using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Diffraction.Core
{
    public static class CudaSolverBridge
    {
        public sealed class SolveResponse
        {
            public bool Success;
            public bool Cancelled;
            public string ErrorMessage;
            public string BackendName;
            public double AssemblyMilliseconds;
            public double LinearSolveMilliseconds;
            public double TotalMilliseconds;
            public DiffractionMath.Compl[] Coefficients;
        }

        public static SolveResponse Solve(DiffractionMath.DifrOnLenta solver, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (solver == null) throw new ArgumentNullException(nameof(solver));
                cancellationToken.ThrowIfCancellationRequested();
                if (solver.PlateCount != 2)
                {
                    return new SolveResponse
                    {
                        Success = false,
                        ErrorMessage = "CUDA backend сейчас поддерживает только две пластины."
                    };
                }

                string executablePath = EnsureCudaExecutable(cancellationToken);
                if (string.IsNullOrEmpty(executablePath))
                {
                    return new SolveResponse
                    {
                        Success = false,
                        ErrorMessage = "Не удалось найти или собрать DiffractionCuda.exe"
                    };
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = BuildArguments(solver),
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    using (cancellationToken.Register(() => TryTerminateProcess(process)))
                    {
                        process.Start();
                        string stdout = process.StandardOutput.ReadToEnd();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        cancellationToken.ThrowIfCancellationRequested();

                        if (process.ExitCode != 0)
                        {
                            return new SolveResponse
                            {
                                Success = false,
                                ErrorMessage = string.IsNullOrWhiteSpace(stderr)
                                    ? "CUDA backend завершился с кодом " + process.ExitCode.ToString(CultureInfo.InvariantCulture)
                                    : stderr.Trim()
                            };
                        }

                        SolveResponse response = ParseSolveOutput(stdout, solver.TotalUnknowns);
                        if (!response.Success && !string.IsNullOrWhiteSpace(stderr))
                            response.ErrorMessage = string.IsNullOrWhiteSpace(response.ErrorMessage) ? stderr.Trim() : response.ErrorMessage;
                        return response;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new SolveResponse
                {
                    Success = false,
                    Cancelled = true,
                    ErrorMessage = "Операция была отменена."
                };
            }
        }

        public static string GetCudaExecutablePath()
        {
            return FindRelativeFile(Path.Combine("Diffraction.Cuda", "build", "DiffractionCuda.exe"));
        }

        private static string EnsureCudaExecutable(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string executablePath = GetCudaExecutablePath();
            if (!string.IsNullOrEmpty(executablePath))
                return executablePath;

            string buildScriptPath = FindRelativeFile(Path.Combine("Diffraction.Cuda", "build_cuda.bat"));
            if (string.IsNullOrEmpty(buildScriptPath))
                return null;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + buildScriptPath + "\"",
                WorkingDirectory = Path.GetDirectoryName(buildScriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                using (cancellationToken.Register(() => TryTerminateProcess(process)))
                {
                    process.Start();
                    process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return GetCudaExecutablePath();
        }

        private static string BuildArguments(DiffractionMath.DifrOnLenta solver)
        {
            return string.Join(" ", new[]
            {
                "--alpha1", Format(solver.alpha[0]),
                "--beta1", Format(solver.beta[0]),
                "--alpha2", Format(solver.alpha[1]),
                "--beta2", Format(solver.beta[1]),
                "--lambda", Format(solver.lambda),
                "--theta", Format(solver.teta),
                "--n", solver.N.ToString(CultureInfo.InvariantCulture),
                "--skin-depth", Format(solver.skinDepth)
            });
        }

        private static SolveResponse ParseSolveOutput(string stdout, int expectedCoefficientCount)
        {
            Dictionary<int, DiffractionMath.Compl> coefficients = new Dictionary<int, DiffractionMath.Compl>();
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (StringReader reader = new StringReader(stdout ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();

                    if (key.StartsWith("coeff_", StringComparison.OrdinalIgnoreCase))
                    {
                        int index;
                        if (!int.TryParse(key.Substring("coeff_".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                            continue;

                        string[] parts = value.Split(',');
                        if (parts.Length != 2) continue;

                        double re, im;
                        if (double.TryParse(parts[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out re) &&
                            double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out im))
                        {
                            coefficients[index] = new DiffractionMath.Compl(re, im);
                        }
                    }
                    else
                    {
                        values[key] = value;
                    }
                }
            }

            SolveResponse response = new SolveResponse();
            string status;
            if (!values.TryGetValue("status", out status) || !string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                response.Success = false;
                response.ErrorMessage = values.ContainsKey("message") ? values["message"] : "CUDA backend вернул некорректный ответ.";
                return response;
            }

            response.Success = true;
            response.BackendName = values.ContainsKey("backend") ? values["backend"] : "CUDA";
            response.AssemblyMilliseconds = ParseDouble(values, "assembly_ms");
            response.LinearSolveMilliseconds = ParseDouble(values, "solve_ms");
            response.TotalMilliseconds = ParseDouble(values, "total_ms");

            response.Coefficients = new DiffractionMath.Compl[expectedCoefficientCount];
            for (int i = 0; i < expectedCoefficientCount; i++)
            {
                DiffractionMath.Compl value;
                if (!coefficients.TryGetValue(i, out value))
                {
                    response.Success = false;
                    response.ErrorMessage = "CUDA backend не вернул коэффициент с индексом " + i.ToString(CultureInfo.InvariantCulture);
                    return response;
                }
                response.Coefficients[i] = value;
            }

            return response;
        }

        private static double ParseDouble(Dictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value))
                return 0.0;

            double parsed;
            return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0.0;
        }

        private static string FindRelativeFile(string relativePath)
        {
            string[] roots =
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (string root in roots)
            {
                string current = root;
                for (int i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
                {
                    string candidate = Path.GetFullPath(Path.Combine(current, relativePath));
                    if (File.Exists(candidate))
                        return candidate;

                    string parent = Directory.GetParent(current)?.FullName;
                    if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = parent;
                }
            }

            return null;
        }

        private static string Format(double value)
        {
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static void TryTerminateProcess(Process process)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
            }
        }
    }
}
