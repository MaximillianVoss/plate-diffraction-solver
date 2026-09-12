using System;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    public class GalerkinSolverTests
    {
        [TestMethod]
        public void SinglePlateGalerkinSolverProducesFiniteNontrivialCoefficients()
        {
            Solver solver = GalerkinSolver.SolveSinglePlate(
                -1.5,
                -0.5,
                1.0,
                Math.PI / 4.0,
                8,
                0.01);

            Assert.AreEqual(1, solver.PlateCount);
            Assert.AreEqual(8, solver.y.Length);
            Assert.IsTrue(solver.y[0].Re != 0 || solver.y[0].Im != 0);
            Assert.IsFalse(double.IsNaN(solver.VerifyBoundaryConditions()));
            Assert.IsFalse(double.IsInfinity(solver.VerifyBoundaryConditions()));
        }
    }
}
