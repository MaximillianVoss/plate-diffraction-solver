using System;
using System.Reflection;
using System.Threading;
using Diffraction.Core;
using MathNet.Numerics.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Diffraction.Core.DiffractionMath;

namespace Diffraction.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class FiniteSkinBasisTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(5)]
        [DataRow(29)]
        [DataRow(54)]
        [DataRow(79)]
        [DataRow(159)]
        public void AnalyticLegendreLogMomentsAgreeWithDirectTransformedIntegration(int degree)
        {
            double maximumError = 0;
            foreach (double target in new[]
            {
                -1.0, -1.0 + 1e-12, -0.91, -0.3, 0.0, 0.27, 0.87,
                1.0 - 1e-12, 1.0 - 1.1102230246251565e-16, 1.0
            })
            {
                double coarse = DirectLogMoment(degree, target, 512);
                double fine = DirectLogMoment(degree, target, 1024);
                Assert.AreEqual(coarse, fine, 2e-12, "The independent quadrature must be resolved first.");
                double actual = LegendreLogMoment(degree, target);
                maximumError = Math.Max(maximumError, Math.Abs(actual - fine));
                Assert.AreEqual(fine, actual, 2e-12,
                    string.Format("Integral P_n(s) log|t-s| ds, n={0}, t={1:R}", degree, target));
            }
            TestContext.WriteLine("Log moment degree={0}: maximum absolute error={1:R}", degree, maximumError);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(30)]
        [DataRow(55)]
        [DataRow(80)]
        [DataRow(240)]
        [DataRow(440)]
        [DataRow(880)]
        [DataRow(1280)]
        public void PublicGaussLegendreQuadratureHasPositiveWeightsAndExactPolynomialMoments(int order)
        {
            GaussLegendreQuadrature(order, out double[] nodes, out double[] weights);
            var moments = new double[2 * order];
            for (int m = 0; m < order; m++)
            {
                Assert.IsTrue(Math.Abs(nodes[m]) < 1 && weights[m] > 0);
                Assert.AreEqual(nodes[m], -nodes[order - 1 - m], 2e-14);
                Assert.AreEqual(weights[m], weights[order - 1 - m], 2e-13);
                double power = 1;
                for (int degree = 0; degree < moments.Length; degree++)
                {
                    moments[degree] += weights[m] * power;
                    power *= nodes[m];
                }
            }

            double maximumError = 0;
            for (int degree = 0; degree < moments.Length; degree++)
            {
                double expected = degree % 2 == 0 ? 2.0 / (degree + 1) : 0;
                maximumError = Math.Max(maximumError, Math.Abs(moments[degree] - expected));
                Assert.AreEqual(expected, moments[degree], 2e-12,
                    string.Format("Q={0}, monomial degree={1}", order, degree));
            }
            TestContext.WriteLine("Gauss-Legendre Q={0}: maximum moment error={1:R}", order, maximumError);
        }

        // These checks concern a conforming discretization of the stated sheet equation,
        // not the physical validity of that equation. The panel reference is separate.
        [DataTestMethod]
        [DataRow(0.01)]
        [DataRow(1e-12)]
        public void PositiveSkinUsesRegularLegendreCurrentIncludingBothEnds(double skin)
        {
            var solver = new DifrOnLenta(-0.5, 0.5, 1, Math.PI / 4, 4, skin);
            Compl[] coefficients =
            {
                new Compl(1.0, 0.3), new Compl(-0.4, 0.2),
                new Compl(0.7, -0.6), new Compl(-0.2, 0.1)
            };
            solver.ApplySolvedCoefficients(coefficients, "test", 0, 0, 0, false);

            Assert.IsFalse(solver.UsesSingularCurrentBasis);
            foreach (double tau in new[] { -1.0, -0.7, 0.0, 0.4, 1.0 })
            {
                Compl expected = coefficients[0] + coefficients[1] * tau +
                    coefficients[2] * ((3 * tau * tau - 1) / 2) +
                    coefficients[3] * ((5 * tau * tau * tau - 3 * tau) / 2);
                Assert.IsTrue(Compl.Abs(solver.CurrentDensity(tau / 2) - expected) < 1e-12,
                    "The stored coefficients must describe J itself, including its finite endpoint values.");
            }
        }

        [TestMethod]
        public void ZeroSkinKeepsTheChebyshevSingularCurrentConvention()
        {
            var solver = new DifrOnLenta(-0.5, 0.5, 1, Math.PI / 4, 3, 0);
            Compl[] coefficients = { new Compl(1, 2), new Compl(-0.5, 0.3), new Compl(0.2, -0.4) };
            solver.ApplySolvedCoefficients(coefficients, "test", 0, 0, 0, false);
            Assert.IsTrue(solver.UsesSingularCurrentBasis);
            foreach (double tau in new[] { -0.9, -0.3, 0.0, 0.7, 0.99 })
            {
                Compl expected = (coefficients[0] + coefficients[1] * tau +
                    coefficients[2] * (2 * tau * tau - 1)) / Math.Sqrt(1 - tau * tau);
                Assert.IsTrue(Compl.Abs(solver.CurrentDensity(tau / 2) - expected) < 1e-12);
            }
        }

        [DataTestMethod]
        [DataRow(30, 0.5)]
        [DataRow(55, 2.0)]
        public void AbsorptionIsExactLegendreMassAndIndependentOfIntegrationOrder(int order, double length)
        {
            var solver = new DifrOnLenta(-length / 2, length / 2, 1, Math.PI / 4, order, 0.01);
            var coefficients = new Compl[order];
            for (int n = 0; n < order; n++)
                coefficients[n] = new Compl(Math.Cos(0.7 * n), Math.Sin(0.4 * n));
            solver.ApplySolvedCoefficients(coefficients, "test", 0, 0, 0, false);

            double exact = AbsorptionFromCoefficients(solver);
            double coarse = IntegrateAbsorption(solver, order);
            double fine = IntegrateAbsorption(solver, 2 * order + 3);
            // |J|^2 has degree 2*(N-1), so N-point Gauss-Legendre is already exact.
            Assert.AreEqual(exact, solver.CalculateAbsorbedEnergy(), exact * 2e-12);
            Assert.AreEqual(exact, coarse, exact * 2e-11);
            Assert.AreEqual(exact, fine, exact * 2e-11);
        }

        [DataTestMethod]
        [DataRow(30, 1.0, 10.0)]
        [DataRow(55, 1.0, 10.0)]
        [DataRow(30, 0.5, 45.0)]
        [DataRow(55, 0.5, 45.0)]
        [DataRow(30, 2.0, 90.0)]
        [DataRow(55, 2.0, 90.0)]
        public void GalerkinHasUnweightedProjectionAndTheSamePhysicalNormalizationAsCollocation(
            int order, double length, double degrees)
        {
            double angle = degrees * Math.PI / 180;
            DifrOnLenta galerkin = GalerkinSolver.SolveSinglePlate(
                -length / 2, length / 2, 1, angle, order, 0.01);
            var collocation = new DifrOnLenta(-length / 2, length / 2, 1, angle, order, 0.01);
            Assert.AreEqual(1, collocation.SolveDifr());

            CheckEndpointContinuity(galerkin);
            CheckEndpointContinuity(collocation);
            double currentDifference = RelativeCurrentDifference(galerkin, collocation);
            double absorptionDifference = RelativeDifference(
                galerkin.CalculateAbsorbedEnergy(), collocation.CalculateAbsorbedEnergy());
            double extinctionDifference = RelativeDifference(
                galerkin.CalculateExtinctionEnergy(160), collocation.CalculateExtinctionEnergy(160));
            double scatteringDifference = RelativeDifference(
                galerkin.CalculateFarFieldScatteredEnergy(128, 160).TotalScattered,
                collocation.CalculateFarFieldScatteredEnergy(128, 160).TotalScattered);
            double weakResidual = UnweightedProjectedResidual(galerkin, 8 * order + 17);
            TestContext.WriteLine(
                "N={0}, L={1}, angle={2}: relative J={3:R}, A={4:R}, E={5:R}, S={6:R}; weak residual={7:R}",
                order, length, degrees, currentDifference, absorptionDifference,
                extinctionDifference, scatteringDifference, weakResidual);

            // Fixed accuracy budgets, not fitted powers: compare the density and equation too.
            Assert.IsTrue(currentDifference < 0.01, "L2 current disagreement exceeds 1%.");
            Assert.IsTrue(absorptionDifference < 0.005, "Absorption disagreement exceeds 0.5%.");
            Assert.IsTrue(extinctionDifference < 0.002, "Extinction disagreement exceeds 0.2%.");
            Assert.IsTrue(scatteringDifference < 0.002, "Scattering disagreement exceeds 0.2%.");
            Assert.IsTrue(weakResidual < 2e-5,
                "The residual must be orthogonal to regular Legendre tests with the unweighted L2 measure.");
        }

        [DataTestMethod]
        [DataRow(30)]
        [DataRow(55)]
        public void FiniteSkinGalerkinConvergesWhenOnlyAssemblyQuadratureIsRefined(int order)
        {
            DifrOnLenta coarse = GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1, Math.PI / 4, order, 0.01);
            DifrOnLenta fine = SolveWithAssemblyQuadrature(order, 16 * order);
            double currentDifference = RelativeCurrentDifference(coarse, fine);
            double absorptionDifference = RelativeDifference(coarse.CalculateAbsorbedEnergy(), fine.CalculateAbsorbedEnergy());
            double extinctionDifference = RelativeDifference(coarse.CalculateExtinctionEnergy(160), fine.CalculateExtinctionEnergy(160));
            TestContext.WriteLine("N={0}, Q={1}->{2}: relative J={3:R}, A={4:R}, E={5:R}",
                order, 8 * order, 16 * order, currentDifference, absorptionDifference, extinctionDifference);

            Assert.IsTrue(currentDifference < 1e-4,
                "Changing quadrature must not reintroduce a logarithmically divergent local mass.");
            Assert.IsTrue(absorptionDifference < 1e-5);
            Assert.IsTrue(extinctionDifference < 1e-5);
        }

        [TestMethod]
        public void NarrowSkinResolvesPowerAndCurrentWithHigherOrderWithoutRelaxingAccuracy()
        {
            DifrOnLenta coarse = GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1, Math.PI / 4, 55, 0.001);
            DifrOnLenta fine = GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1, Math.PI / 4, 80, 0.001);
            DifrOnLenta reference = GalerkinSolver.SolveSinglePlate(-0.5, 0.5, 1, Math.PI / 4, 110, 0.001);
            double coarseCurrentError = RelativeCurrentDifference(coarse, reference);
            double fineCurrentError = RelativeCurrentDifference(fine, reference);
            double absorptionError = RelativeDifference(coarse.CalculateAbsorbedEnergy(), reference.CalculateAbsorbedEnergy());
            double scatteringError = RelativeDifference(
                coarse.CalculateFarFieldScatteredEnergy(128, 240).TotalScattered,
                reference.CalculateFarFieldScatteredEnergy(128, 240).TotalScattered);
            TestContext.WriteLine(
                "delta=0.001, reference N=110: J errors N55={0:R}, N80={1:R}; N55 A={2:R}, S={3:R}",
                coarseCurrentError, fineCurrentError, absorptionError, scatteringError);

            // A small optical-theorem defect does not establish density accuracy at small delta.
            // Compare errors to the same higher-N solution, not successive solution differences.
            Assert.IsTrue(absorptionError < 0.001, "N55 absorption must agree within 0.1%.");
            Assert.IsTrue(scatteringError < 0.001, "N55 scattering must agree within 0.1%.");
            Assert.IsTrue(fineCurrentError < 0.01, "N80 L2 current must agree within 1%.");
            Assert.IsTrue(fineCurrentError < coarseCurrentError);
            CheckEndpointContinuity(fine);
        }

        private static DifrOnLenta SolveWithAssemblyQuadrature(int order, int quadratureOrder)
        {
            // Exercise the production branch at fixed N without adding a public quadrature setting.
            MethodInfo solve = typeof(GalerkinSolver).GetMethod("SolveFiniteSkin", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(solve);
            var solver = new DifrOnLenta(-0.5, 0.5, 1, Math.PI / 4, order, 0.01);
            return (DifrOnLenta)solve.Invoke(null, new object[]
            {
                solver, 2 * Math.PI, 0.5, 0.0, order, quadratureOrder, CancellationToken.None
            });
        }

        private static double DirectLogMoment(int degree, double target, int samples)
        {
            double integral = 0;
            foreach (int sign in new[] { -1, 1 })
            {
                double span = sign < 0 ? target + 1 : 1 - target;
                if (span == 0) continue;
                // Split at the logarithmic singularity, then r=span*u^4. No Q function,
                // analytic log moment, or production quadrature is used in this reference.
                integral += GaussLegendreRule.Integrate(u =>
                {
                    double cube = u * u * u;
                    double source = target + sign * span * cube * u;
                    return 4 * span * cube * Legendre(degree, source) * (Math.Log(span) + 4 * Math.Log(u));
                }, 0, 1, samples);
            }
            return integral;
        }

        private static void CheckEndpointContinuity(DifrOnLenta solver)
        {
            double half = (solver.beta[0] - solver.alpha[0]) / 2;
            double center = (solver.beta[0] + solver.alpha[0]) / 2;
            foreach (int sign in new[] { -1, 1 })
            {
                Compl expected = new Compl(0, 0);
                double derivativeBound = 0;
                for (int n = 0; n < solver.N; n++)
                {
                    expected += solver.y[n] * (sign < 0 && n % 2 != 0 ? -1.0 : 1.0);
                    derivativeBound += Compl.Abs(solver.y[n]) * n * (n + 1.0) / 2;
                }
                Compl atEnd = solver.CurrentDensity(sign < 0 ? solver.alpha[0] : solver.beta[0]);
                const double epsilon = 1e-10;
                Compl nearEnd = solver.CurrentDensity(center + sign * half * (1 - epsilon));
                Assert.IsFalse(double.IsNaN(atEnd.Re) || double.IsNaN(atEnd.Im) ||
                    double.IsInfinity(atEnd.Re) || double.IsInfinity(atEnd.Im));
                Assert.IsTrue(Compl.Abs(atEnd - expected) < 1e-11 * Math.Max(1, Compl.Abs(expected)));
                Assert.IsTrue(Compl.Abs(nearEnd - atEnd) < 1.01 * epsilon * derivativeBound + 1e-10,
                    "Endpoint continuity must obey the polynomial derivative bound, without a sqrt cutoff.");
            }
        }

        private static double AbsorptionFromCoefficients(DifrOnLenta solver)
        {
            double mass = 0;
            for (int n = 0; n < solver.N; n++)
                mass += 2.0 / (2 * n + 1) * AbsSquared(solver.y[n]);
            double half = (solver.beta[0] - solver.alpha[0]) / 2;
            return -0.5 * solver.SheetCoefficient.Im * half * mass;
        }

        private static double IntegrateAbsorption(DifrOnLenta solver, int samples)
        {
            GaussLegendreQuadrature(samples, out double[] tau, out double[] weights);
            double half = (solver.beta[0] - solver.alpha[0]) / 2;
            double center = (solver.beta[0] + solver.alpha[0]) / 2;
            double integral = 0;
            for (int m = 0; m < samples; m++)
                integral += weights[m] * AbsSquared(solver.CurrentDensity(center + half * tau[m]));
            return -0.5 * solver.SheetCoefficient.Im * half * integral;
        }

        private static double UnweightedProjectedResidual(DifrOnLenta solver, int samples)
        {
            GaussLegendreQuadrature(samples, out double[] tau, out double[] weights);
            double half = (solver.beta[0] - solver.alpha[0]) / 2;
            double center = (solver.beta[0] + solver.alpha[0]) / 2;
            var residual = new Compl[samples];
            for (int m = 0; m < samples; m++)
            {
                double x = center + half * tau[m];
                residual[m] = solver.u_on_strip(x) - solver.SheetCoefficient * solver.CurrentDensity(x);
            }

            double largest = 0;
            for (int n = 0; n < solver.N; n++)
            {
                Compl moment = new Compl(0, 0);
                for (int m = 0; m < samples; m++)
                    moment += residual[m] * (weights[m] * Legendre(n, tau[m]));
                // ||u_inc||_L2(-1,1)=sqrt(2); ||P_n||=sqrt(2/(2n+1)).
                largest = Math.Max(largest, Compl.Abs(moment) * Math.Sqrt(2 * n + 1) / 2);
            }
            return largest;
        }

        private static double RelativeCurrentDifference(DifrOnLenta first, DifrOnLenta second)
        {
            double difference = 0;
            double reference = 0;
            for (int n = 0; n < Math.Max(first.N, second.N); n++)
            {
                double mass = 2.0 / (2 * n + 1);
                Compl firstValue = n < first.N ? first.y[n] : new Compl(0, 0);
                Compl secondValue = n < second.N ? second.y[n] : new Compl(0, 0);
                difference += mass * AbsSquared(firstValue - secondValue);
                reference += mass * AbsSquared(secondValue);
            }
            return Math.Sqrt(difference / reference);
        }

        private static double AbsSquared(Compl value)
        {
            return value.Re * value.Re + value.Im * value.Im;
        }

        private static double RelativeDifference(double first, double second)
        {
            return Math.Abs(first - second) / Math.Max(Math.Abs(first), Math.Abs(second));
        }
    }
}
