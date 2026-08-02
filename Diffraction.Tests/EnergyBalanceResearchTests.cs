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
            Solver.FarFieldScatteredEnergyComponents oddAngles = solver.CalculateFarFieldScatteredEnergy(181, 200);
            double scale = Math.Max(fine.TotalScattered, 1e-12);

            Assert.IsTrue(coarse.ReflectedScattered >= 0.0, "coarse reflected energy");
            Assert.IsTrue(coarse.TransmittedScattered >= 0.0, "coarse transmitted energy");
            Assert.IsFalse(double.IsNaN(fine.TotalScattered), "far-field total must be finite");
            Assert.IsFalse(double.IsInfinity(fine.TotalScattered), "far-field total must be finite");
            Assert.IsTrue(Math.Abs(fine.ReflectedScattered - fine.TransmittedScattered) / scale < 1e-10,
                "a zero-thickness single-layer sheet radiates symmetrically");
            Assert.IsTrue(Math.Abs(oddAngles.ReflectedScattered - oddAngles.TransmittedScattered) / scale < 1e-10,
                "a sample on the half-plane boundary must be split symmetrically");
            Assert.IsTrue(Math.Abs(coarse.TotalScattered - fine.TotalScattered) / scale < 0.08,
                "far-field quadrature convergence");
        }

        [TestMethod]
        public void SurfaceCurrentBasis_DoesNotAcquireAnExtraHalfLengthFactor()
        {
            Solver shortPlate = new Solver(-0.5, 0.5, 10.0, Math.PI / 4.0, 1, 0.01);
            Solver longPlate = new Solver(-1.0, 1.0, 10.0, Math.PI / 4.0, 1, 0.01);
            var coefficients = new[] { new Diffraction.Core.DiffractionMath.Compl(1.0, 0.0) };

            shortPlate.ApplySolvedCoefficients(coefficients, "test", 0.0, 0.0, 0.0, usedCuda: false);
            longPlate.ApplySolvedCoefficients(coefficients, "test", 0.0, 0.0, 0.0, usedCuda: false);

            Assert.AreEqual(1.0, shortPlate.CurrentDensity(0.0).Re, 1e-12, "short-plate source density");
            Assert.AreEqual(1.0, longPlate.CurrentDensity(0.0).Re, 1e-12, "long-plate source density");
        }

        [TestMethod]
        public void TwoPlateScatteredFlux_IsNonNegativeAndSymmetricAboveAndBelow()
        {
            Solver solver = CreateTwoPlates(n: 30, skinDepth: 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            Solver.FarFieldScatteredEnergyComponents far = solver.CalculateFarFieldScatteredEnergy(360, 240);
            Solver.ScatteredSheetFluxComponents sheet = solver.CalculateScatteredSheetFluxComponents(400);
            double incident = solver.CalculatePlateIncidentEnergy();

            Assert.IsTrue(far.ReflectedScattered >= 0.0, "reflected scattered energy");
            Assert.IsTrue(far.TransmittedScattered >= 0.0, "forward scattered energy");
            Assert.IsTrue(far.ReflectedScattered / incident < 1.2,
                "standard two-plate case must not contain the old fourfold half-length error");
            Assert.IsTrue(Math.Abs(far.ReflectedScattered - far.TransmittedScattered) / incident < 1e-10,
                "far-field upper/lower symmetry");
            Assert.IsTrue(sheet.AboveOutgoing >= 0.0, "upper outgoing sheet flux");
            Assert.IsTrue(sheet.BelowOutgoing >= 0.0, "lower outgoing sheet flux");
            Assert.IsTrue(sheet.AbsoluteMismatch / incident < 1e-10, "sheet upper/lower symmetry");
        }

        [TestMethod]
        public void TwoPlateScatteredEnergy_DecreasesAcrossSkinDepthSweep()
        {
            double previousReflected = double.PositiveInfinity;
            double previousForward = double.PositiveInfinity;

            foreach (double skinDepth in new[] { 0.0, 0.001, 0.01, 0.05, 0.1, 0.2 })
            {
                Solver solver = CreateTwoPlates(n: 25, skinDepth: skinDepth);
                Assert.AreEqual(1, solver.SolveDifr(), "solver failed for skinDepth=" + skinDepth);
                Solver.FarFieldScatteredEnergyComponents far = solver.CalculateFarFieldScatteredEnergy(180, 200);

                Assert.IsTrue(far.ReflectedScattered <= previousReflected + 1e-10,
                    "reflected scattering increased at skinDepth=" + skinDepth);
                Assert.IsTrue(far.TransmittedScattered <= previousForward + 1e-10,
                    "forward scattering increased at skinDepth=" + skinDepth);
                previousReflected = far.ReflectedScattered;
                previousForward = far.TransmittedScattered;
            }
        }

        [TestMethod]
        public void TwoPlateClosedContour_UsesTheSameCurrentAsTheLayerPotential()
        {
            Solver solver = CreateTwoPlates(n: 30, skinDepth: 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            Solver.EnergyComponents energy = solver.CalculateEnergyComponents(includeContourDiagnostic: true);
            Assert.IsTrue(Math.Abs(energy.SignedContourResidual) / energy.Incident < 0.03,
                "two-plate closed-contour energy residual");
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

        private static Solver CreateTwoPlates(int n, double skinDepth)
        {
            return new Solver(-1.5, -0.5, 0.5, 1.5, 1.0, 45.0 * Math.PI / 180.0, n, skinDepth);
        }
    }
}
