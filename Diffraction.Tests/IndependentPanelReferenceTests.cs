using System;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    /// <summary>
    /// Independent discretization of the SAME assumed finite-strip equation:
    /// u_inc + (i/4) integral H0^(2)(k|x-t|) J(t) dt = q J(x),
    /// q = skinDepth/2 * (1-i), u_inc = exp(i*k*cos(theta)*x).
    /// Agreement verifies numerics, NOT the choice of physical sheet model.
    /// The reference has piecewise-constant physical J, not Chebyshev phi or
    /// J divided by the mapping half-length. It never uses production assembly,
    /// Bessel wrappers, current reconstruction, or energy postprocessing.
    /// </summary>
    [TestClass]
    [TestCategory("IndependentPhysics")]
    [DoNotParallelize]
    public class IndependentPanelReferenceTests
    {
        private static readonly double[] Angles = { 10.0, 45.0, 90.0 };
        private static readonly int[] PanelCounts = { 60, 120, 240 };
        private static readonly Lazy<Observables[,]> IdealReference =
            new Lazy<Observables[,]>(() => SolveRefinements(0.0));
        private static readonly Lazy<Observables[,]> LossyReference =
            new Lazy<Observables[,]>(() => SolveRefinements(0.01));

        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(10.0, 0.0, 40, false)]
        [DataRow(45.0, 0.0, 40, false)]
        [DataRow(90.0, 0.0, 40, false)]
        [DataRow(10.0, 0.01, 40, false)]
        [DataRow(45.0, 0.01, 40, false)]
        [DataRow(90.0, 0.01, 40, false)]
        [DataRow(90.0, 0.01, 60, true)]
        public void ChebyshevSolver_AgreesWithRefinedConstantPanels(
            double angleDegrees, double skinDepth, int approximationOrder, bool useGalerkin)
        {
            int angleIndex = Array.IndexOf(Angles, angleDegrees);
            Observables[,] levels = (skinDepth == 0.0 ? IdealReference : LossyReference).Value;
            Observables coarse = levels[0, angleIndex];
            Observables medium = levels[1, angleIndex];
            Observables fine = levels[2, angleIndex];
            string context = string.Format("theta={0}, delta={1}, N={2}, Galerkin={3}",
                angleDegrees, skinDepth, approximationOrder, useGalerkin);

            double scatteringStep = AssertConverging(
                coarse.Scattered, medium.Scattered, fine.Scattered, 0.005, context + " scattering");
            double extinctionStep = AssertConverging(
                coarse.Extinction, medium.Extinction, fine.Extinction, 0.005, context + " extinction");
            double absorptionStep = 0.0;
            if (skinDepth > 0.0)
            {
                absorptionStep = AssertConverging(
                    coarse.Absorbed, medium.Absorbed, fine.Absorbed, 0.01, context + " absorption");
            }
            else
            {
                Assert.AreEqual(0.0, coarse.Absorbed, 0.0, context);
                Assert.AreEqual(0.0, medium.Absorbed, 0.0, context);
                Assert.AreEqual(0.0, fine.Absorbed, 0.0, context);
            }

            Assert.IsTrue(IsFinite(fine.Extinction) && fine.Extinction > 0.0 &&
                Math.Abs(fine.Extinction - fine.Scattered - fine.Absorbed) <=
                    2.0 * (scatteringStep + absorptionStep) + 1e-10 * fine.Scattered,
                context + " panel optical theorem: extinction must equal scattering plus absorption");

            // Uniform constant panels resolve the ideal square-root edge only
            // algebraically. For first-order convergence the final doubling
            // estimates the remaining error; 2*step is a conservative allowance.
            // Add 0.5% for finite spectral order/angular integration and 2% for
            // the more edge-sensitive |J|^2 and production's 400-point loss rule.
            // Independent refinement caps the TOTAL budgets at 1.5% and 4%.
            // No production value or golden snapshot is used to set a tolerance.
            double scatteringTolerance = 2.0 * scatteringStep + 0.005 * fine.Scattered;
            double absorptionTolerance = skinDepth > 0.0
                ? 2.0 * absorptionStep + 0.02 * fine.Absorbed
                : 1e-12;
            double extinctionTolerance = 2.0 * extinctionStep + 0.005 * fine.Extinction;

            Solver solver;
            if (useGalerkin)
            {
                solver = Diffraction.Core.GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1.0,
                    angleDegrees * Math.PI / 180.0, approximationOrder, skinDepth);
            }
            else
            {
                solver = new Solver(-0.5, 0.5, 1.0,
                    angleDegrees * Math.PI / 180.0, approximationOrder, skinDepth);
                Assert.AreEqual(1, solver.SolveDifr(), context + " production solve failed");
            }
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(angleSamples: 64, plateSamples: 80);
            if (useGalerkin)
            {
                // At k*length=2*pi the far pattern is smooth. Check the cheaper
                // output quadrature against doubled counts at the largest N.
                Solver.FarFieldScatteredEnergyComponents refinedFar =
                    solver.CalculateFarFieldScatteredEnergy(angleSamples: 128, plateSamples: 160);
                AssertWithin(refinedFar.TotalScattered, far.TotalScattered,
                    1e-8 * refinedFar.TotalScattered, context + " far-field quadrature refinement");
            }
            double absorbed = solver.CalculateAbsorbedEnergy();

            TestContext.WriteLine(
                "{0}: panels 60/120/240 S={1:G12}/{2:G12}/{3:G12}, A={4:G12}/{5:G12}/{6:G12}; " +
                "production S={7:G12}, A={8:G12}; absolute tolerances S={9:G5}, A={10:G5}; " +
                "panel extinction - S - A={11:G6}",
                context, coarse.Scattered, medium.Scattered, fine.Scattered,
                coarse.Absorbed, medium.Absorbed, fine.Absorbed,
                far.TotalScattered, absorbed, scatteringTolerance, absorptionTolerance,
                fine.Extinction - fine.Scattered - fine.Absorbed);

            AssertWithin(fine.Scattered, far.TotalScattered, scatteringTolerance, context + " total scattering");
            AssertWithin(fine.Scattered / 2.0, far.ReflectedScattered,
                scatteringTolerance / 2.0, context + " reflected scattering");
            AssertWithin(fine.Scattered / 2.0, far.TransmittedScattered,
                scatteringTolerance / 2.0, context + " forward scattering (not total transmission)");
            AssertWithin(fine.Absorbed, absorbed, absorptionTolerance, context + " absorption");
            // Quadratic energies cannot detect a global sign error in J.
            AssertWithin(fine.Extinction, solver.CalculateExtinctionEnergy(80),
                extinctionTolerance, context + " signed incident-current extinction");
        }

        [DataTestMethod]
        [DataRow(60)]
        [DataRow(120)]
        [DataRow(240)]
        public void PanelSelfIntegral_MatchesIndependentLogMappedQuadrature(int panelCount)
        {
            double width = 1.0 / panelCount;
            double k = 2.0 * Math.PI;
            Complex integratedSeries = ConstantPanels.SelfIntegral(k, width);

            // r=(h/2)*exp(-s) removes the endpoint singularity without sharing
            // the series or its analytic logarithmic integral. The omitted
            // s>40 tail is below 1e-16 for these widths and k=2*pi.
            Complex mappedQuadrature = Complex.Zero;
            for (int interval = 0; interval < 40; interval++)
            {
                mappedQuadrature += ConstantPanels.Gauss8(s =>
                {
                    double jacobian = width * Math.Exp(-s);
                    return jacobian * ConstantPanels.Kernel(k * jacobian / 2.0);
                }, interval, interval + 1.0);
            }

            Assert.IsTrue(integratedSeries.Real < 0.0, "H2 self term must have negative real part here");
            Assert.IsTrue(integratedSeries.Imaginary > 0.0, "i*H2/4 has +J0/4 imaginary part");
            Assert.IsTrue((integratedSeries - mappedQuadrature).Magnitude < 1e-12 * width,
                string.Format("panels={0}: self series={1}, log-mapped integral={2}",
                    panelCount, integratedSeries, mappedQuadrature));
        }

        private static Observables[,] SolveRefinements(double skinDepth)
        {
            var result = new Observables[PanelCounts.Length, Angles.Length];
            for (int level = 0; level < PanelCounts.Length; level++)
            {
                Observables[] values = ConstantPanels.Solve(PanelCounts[level], skinDepth, Angles);
                for (int angle = 0; angle < Angles.Length; angle++)
                    result[level, angle] = values[angle];
            }
            return result;
        }

        private static double AssertConverging(
            double coarse, double medium, double fine, double maxRelativeStep, string context)
        {
            Assert.IsTrue(IsFinite(coarse) && IsFinite(medium) && IsFinite(fine) &&
                coarse > 0.0 && medium > 0.0 && fine > 0.0, context + " must be finite and positive");
            double earlierStep = Math.Abs(medium - coarse);
            double finalStep = Math.Abs(fine - medium);
            Assert.IsTrue(finalStep <= 0.75 * earlierStep + 1e-12 * fine,
                string.Format("{0}: refinement has not contracted: {1:G6} -> {2:G6}",
                    context, earlierStep, finalStep));
            Assert.IsTrue(finalStep <= maxRelativeStep * fine,
                string.Format("{0}: final panel change {1:P4} exceeds {2:P2}",
                    context, finalStep / fine, maxRelativeStep));
            return finalStep;
        }

        private static void AssertWithin(double expected, double actual, double tolerance, string context)
        {
            Assert.IsTrue(IsFinite(actual) && actual >= 0.0, context + " must be finite and nonnegative");
            Assert.AreEqual(expected, actual, tolerance,
                string.Format("{0}: panel={1:G12}, production={2:G12}, tolerance={3:G6}",
                    context, expected, actual, tolerance));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class Observables
        {
            public double Scattered;
            public double Absorbed;
            public double Extinction;
        }

        private static class ConstantPanels
        {
            private const double EulerGamma = 0.5772156649015328606;
            private static readonly double[] Nodes =
            {
                -0.9602898564975363, -0.7966664774136267, -0.5255324099163290, -0.1834346424956498,
                0.1834346424956498, 0.5255324099163290, 0.7966664774136267, 0.9602898564975363
            };
            private static readonly double[] Weights =
            {
                0.1012285362903763, 0.2223810344533745, 0.3137066458778873, 0.3626837833783620,
                0.3626837833783620, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763
            };

            public static Observables[] Solve(int panelCount, double skinDepth, double[] angles)
            {
                const double length = 1.0;
                double k = 2.0 * Math.PI;
                double h = length / panelCount;
                var q = new Complex(skinDepth / 2.0, -skinDepth / 2.0);
                var integratedKernel = new Complex[panelCount];
                integratedKernel[0] = SelfIntegral(k, h);
                for (int distance = 1; distance < panelCount; distance++)
                {
                    double offset = distance * h;
                    integratedKernel[distance] = Gauss8(
                        t => Kernel(k * (offset + t)), -h / 2.0, h / 2.0);
                }

                // Uniform panels make the operator Toeplitz. Unknowns are J_j,
                // not integrated currents h*J_j. The diagonal is K_self - q.
                var matrix = Matrix<Complex>.Build.Dense(panelCount, panelCount,
                    (row, column) => integratedKernel[Math.Abs(row - column)] -
                        (row == column ? q : Complex.Zero));
                var rhs = Matrix<Complex>.Build.Dense(panelCount, angles.Length, (row, angle) =>
                    -Phase(k * Math.Cos(angles[angle] * Math.PI / 180.0) * (-length / 2.0 + (row + 0.5) * h)));
                Matrix<Complex> currents = matrix.LU().Solve(rhs);
                double residual = (matrix * currents - rhs).FrobeniusNorm() / rhs.FrobeniusNorm();
                Assert.IsTrue(IsFinite(residual) && residual < 1e-11,
                    string.Format("panels={0}, delta={1}: relative linear residual={2:G6}",
                        panelCount, skinDepth, residual));

                var result = new Observables[angles.Length];
                for (int angle = 0; angle < angles.Length; angle++)
                {
                    var values = new Observables();
                    double incidentK = k * Math.Cos(angles[angle] * Math.PI / 180.0);
                    // -Im(q)/2 * integral |J|^2 dt = delta/4 * sum h |J_j|^2.
                    for (int panel = 0; panel < panelCount; panel++)
                    {
                        values.Absorbed += skinDepth / 4.0 * h * AbsSquared(currents[panel, angle]);
                        double center = -length / 2.0 + (panel + 0.5) * h;
                        // Independent optical theorem: E=Im(integral conj(u_inc)*J dt)/2.
                        values.Extinction += 0.5 * h * Sinc(incidentK * h / 2.0) *
                            (Phase(-incidentK * center) * currents[panel, angle]).Imaginary;
                    }

                    // H2 has outgoing exp(-ikr) with exp(+ik*directionX*t).
                    // Integrate each constant panel's phase exactly with sinc.
                    // S = integral_0^(2*pi) |F(phi)|^2/(16*pi) dphi.
                    // Only scattered power is included: no incident interference,
                    // no clipping/renormalization to R+T+A=I for a finite strip.
                    const int angularSamples = 128;
                    double dPhi = 2.0 * Math.PI / angularSamples;
                    for (int sample = 0; sample < angularSamples; sample++)
                    {
                        double longitudinalK = k * Math.Cos((sample + 0.5) * dPhi);
                        double panelWeight = h * Sinc(longitudinalK * h / 2.0);
                        Complex farIntegral = Complex.Zero;
                        for (int panel = 0; panel < panelCount; panel++)
                        {
                            double center = -length / 2.0 + (panel + 0.5) * h;
                            farIntegral += currents[panel, angle] * panelWeight * Phase(longitudinalK * center);
                        }
                        values.Scattered += AbsSquared(farIntegral) * dPhi / (16.0 * Math.PI);
                    }
                    result[angle] = values;
                }
                return result;
            }

            public static Complex SelfIntegral(double k, double width)
            {
                // Integrate the convergent J0/Y0 series term by term on [-h/2,h/2].
                // Y0(z) = (2/pi) sum c_m * (log(z/2)+gamma-H_m),
                // c_m=(-z^2/4)^m/(m!)^2; see https://dlmf.nist.gov/10.8.E2.
                // Integral r^(2m) log(kr/2) dr contributes -1/(2m+1).
                // In particular the leading logarithmic self integral is
                // h/(2*pi)*(log(k*h/4)+gamma-1) + i*h/4, NOT a cutoff H0(0).
                double z = k * width / 4.0;
                double coefficient = 1.0;
                double harmonic = 0.0;
                double jIntegral = 0.0;
                double yIntegral = 0.0;
                for (int m = 0; m < 32; m++)
                {
                    if (m > 0)
                    {
                        coefficient *= -z * z / (m * m);
                        harmonic += 1.0 / m;
                    }
                    double jTerm = coefficient / (2.0 * m + 1.0);
                    double yTerm = jTerm * (Math.Log(z) + EulerGamma - harmonic - 1.0 / (2.0 * m + 1.0));
                    jIntegral += jTerm;
                    yIntegral += yTerm;
                    if (m > 0 && Math.Abs(jTerm) + Math.Abs(yTerm) < 1e-17)
                        return new Complex(width * yIntegral / (2.0 * Math.PI), width * jIntegral / 4.0);
                }
                throw new InvalidOperationException("Panel self-integral series did not converge.");
            }

            public static Complex Kernel(double argument)
            {
                // (i/4)*(J0-i*Y0) = (Y0+i*J0)/4, for positive real arguments.
                return new Complex(SpecialFunctions.BesselY(0.0, argument),
                    SpecialFunctions.BesselJ(0.0, argument)) / 4.0;
            }

            public static Complex Gauss8(Func<double, Complex> function, double lower, double upper)
            {
                double halfWidth = (upper - lower) / 2.0;
                double midpoint = (upper + lower) / 2.0;
                Complex sum = Complex.Zero;
                for (int i = 0; i < Nodes.Length; i++)
                    sum += Weights[i] * function(midpoint + halfWidth * Nodes[i]);
                return halfWidth * sum;
            }

            private static Complex Phase(double argument)
            {
                return new Complex(Math.Cos(argument), Math.Sin(argument));
            }

            private static double Sinc(double value)
            {
                return Math.Abs(value) < 1e-8 ? 1.0 - value * value / 6.0 : Math.Sin(value) / value;
            }

            private static double AbsSquared(Complex value)
            {
                return value.Real * value.Real + value.Imaginary * value.Imaginary;
            }
        }
    }
}
