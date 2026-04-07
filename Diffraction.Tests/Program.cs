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

        private static DifrOnLenta CreateTwoPlateSolver(int n, double skinDepth)
        {
            double theta = 10.0 * Math.PI / 180.0;
            return new DifrOnLenta(-1.5, -0.5, 0.5, 1.5, 1.0, theta, n, skinDepth);
        }
    }
}
