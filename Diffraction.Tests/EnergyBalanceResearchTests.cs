using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    public class EnergyBalanceResearchTests
    {
        [TestMethod]
        public void PlateFluxes_AreStableUnderSamplingRefinement()
        {
            Solver solver = CreateSinglePlate(n: 30, skinDepth: 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            Solver.PlateFluxComponents coarse = solver.CalculatePlateFluxComponents(200);
            Solver.PlateFluxComponents fine = solver.CalculatePlateFluxComponents(400);
            double incident = fine.Incident;

            Assert.IsTrue(Math.Abs(coarse.AboveFlux - fine.AboveFlux) / incident < 0.01, "above flux convergence");
            Assert.IsTrue(Math.Abs(coarse.BelowFlux - fine.BelowFlux) / incident < 0.01, "below flux convergence");
            Assert.IsTrue(Math.Abs(coarse.AbsorbedFromFlux - fine.AbsorbedFromFlux) / incident < 0.01, "flux loss convergence");
        }

        [TestMethod]
        public void FarFieldScatteredEnergy_IsFiniteSymmetricAndConvergent()
        {
            Solver solver = CreateSinglePlate(n: 30, skinDepth: 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            Solver.FarFieldScatteredEnergyComponents coarse = solver.CalculateFarFieldScatteredEnergy(180, 160);
            Solver.FarFieldScatteredEnergyComponents fine = solver.CalculateFarFieldScatteredEnergy(360, 240);
            double scale = Math.Max(fine.TotalScattered, 1e-12);

            Assert.IsTrue(coarse.ReflectedScattered >= 0.0, "coarse reflected energy");
            Assert.IsTrue(coarse.TransmittedScattered >= 0.0, "coarse transmitted energy");
            Assert.IsFalse(double.IsNaN(fine.TotalScattered), "far-field total must be finite");
            Assert.IsFalse(double.IsInfinity(fine.TotalScattered), "far-field total must be finite");
            Assert.IsTrue(Math.Abs(fine.ReflectedScattered - fine.TransmittedScattered) / scale < 1e-10,
                "a zero-thickness single-layer sheet radiates symmetrically");
            Assert.IsTrue(Math.Abs(coarse.TotalScattered - fine.TotalScattered) / scale < 0.08,
                "far-field quadrature convergence");
        }

        [TestMethod]
        public void IncreasingSkinDepth_ReducesScatteredFarFieldForSinglePlate()
        {
            Solver thin = CreateSinglePlate(n: 30, skinDepth: 0.01);
            Solver thick = CreateSinglePlate(n: 30, skinDepth: 0.1);
            Assert.AreEqual(1, thin.SolveDifr(), "thin solver failed");
            Assert.AreEqual(1, thick.SolveDifr(), "thick solver failed");

            Solver.FarFieldScatteredEnergyComponents thinFar = thin.CalculateFarFieldScatteredEnergy(180, 200);
            Solver.FarFieldScatteredEnergyComponents thickFar = thick.CalculateFarFieldScatteredEnergy(180, 200);

            Assert.IsTrue(thickFar.ReflectedScattered < thinFar.ReflectedScattered, "reflected scattered field must decrease");
            Assert.IsTrue(thickFar.TransmittedScattered < thinFar.TransmittedScattered, "forward scattered field must decrease");
        }

        [TestMethod]
        public void ClosedContourDiagnostic_IsExplicitAndFinite()
        {
            Solver solver = CreateSinglePlate(n: 25, skinDepth: 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            Solver.EnergyComponents fast = solver.CalculateEnergyComponents();
            Solver.EnergyComponents audited = solver.CalculateEnergyComponents(includeContourDiagnostic: true);

            Assert.IsTrue(double.IsNaN(fast.SignedContourResidual), "fast calculation must skip contour work");
            Assert.IsFalse(double.IsNaN(audited.SignedContourResidual), "audited contour residual");
            Assert.IsFalse(double.IsInfinity(audited.SignedContourResidual), "audited contour residual");
        }

        private static Solver CreateSinglePlate(int n, double skinDepth)
        {
            return new Solver(-1.0, 1.0, 1.0, 45.0 * Math.PI / 180.0, n, skinDepth);
        }
    }
}
