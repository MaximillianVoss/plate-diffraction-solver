using System;
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
    }
}
