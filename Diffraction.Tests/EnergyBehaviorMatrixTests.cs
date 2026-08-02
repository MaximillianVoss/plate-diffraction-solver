using System;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    [TestCategory("DeepValidation")]
    public class EnergyBehaviorMatrixTests
    {
        private static readonly double[] SkinDepths = { 0.0, 0.001, 0.01, 0.05, 0.1, 0.2 };

        [TestMethod]
        public void TwoPlateMatrix_ScatteringIsFiniteSymmetricNonNegativeAndMonotone()
        {
            foreach (double thetaDeg in new[] { 15.0, 30.0, 45.0, 60.0, 90.0 })
            {
                double previousReflected = double.PositiveInfinity;
                double previousForward = double.PositiveInfinity;

                foreach (double skinDepth in SkinDepths)
                {
                    Solver solver = CreateTwoPlates(thetaDeg, n: 24, skinDepth: skinDepth);
                    Assert.AreEqual(1, solver.SolveDifr(), Case(thetaDeg, skinDepth, "solver failed"));

                    Solver.FarFieldScatteredEnergyComponents far =
                        solver.CalculateFarFieldScatteredEnergy(angleSamples: 96, plateSamples: 120);
                    Solver.ScatteredSheetFluxComponents sheet =
                        solver.CalculateScatteredSheetFluxComponents(samplesPerPlate: 80);
                    double incident = solver.CalculatePlateIncidentEnergy();
                    double absorption = solver.CalculateAbsorbedEnergy();

                    AssertFiniteNonNegative(far.ReflectedScattered, Case(thetaDeg, skinDepth, "R_scat"));
                    AssertFiniteNonNegative(far.TransmittedScattered, Case(thetaDeg, skinDepth, "T_scat"));
                    AssertFiniteNonNegative(sheet.AboveOutgoing, Case(thetaDeg, skinDepth, "sheet above"));
                    AssertFiniteNonNegative(sheet.BelowOutgoing, Case(thetaDeg, skinDepth, "sheet below"));
                    Assert.IsTrue(absorption >= -1e-10, Case(thetaDeg, skinDepth, "negative absorption"));
                    Assert.IsTrue(
                        Math.Abs(far.ReflectedScattered - far.TransmittedScattered) / incident < 1e-10,
                        Case(thetaDeg, skinDepth, "far-field upper/lower mismatch"));
                    Assert.IsTrue(
                        sheet.AbsoluteMismatch / incident < 1e-10,
                        Case(thetaDeg, skinDepth, "sheet upper/lower mismatch"));
                    Assert.IsTrue(
                        far.ReflectedScattered <= previousReflected + 1e-9,
                        Case(thetaDeg, skinDepth, "R_scat increased with skin depth"));
                    Assert.IsTrue(
                        far.TransmittedScattered <= previousForward + 1e-9,
                        Case(thetaDeg, skinDepth, "T_scat increased with skin depth"));

                    previousReflected = far.ReflectedScattered;
                    previousForward = far.TransmittedScattered;
                }
            }
        }

        [TestMethod]
        public void TwoPlateMatrix_LocalFluxBalanceStaysWithinTolerance()
        {
            foreach (double thetaDeg in new[] { 15.0, 45.0, 90.0 })
            {
                foreach (double skinDepth in SkinDepths)
                {
                    Solver solver = CreateTwoPlates(thetaDeg, n: 24, skinDepth: skinDepth);
                    Assert.AreEqual(1, solver.SolveDifr(), Case(thetaDeg, skinDepth, "solver failed"));

                    Solver.PlateFluxComponents flux = solver.CalculatePlateFluxComponents(samplesPerPlate: 200);
                    double absorbed = solver.CalculateAbsorbedEnergy();
                    double residual = flux.AbsorbedFromFlux - absorbed;

                    Assert.IsTrue(
                        Math.Abs(residual) / flux.Incident < EnergyDiagnosticsReportBuilder.LocalBalanceTolerance,
                        Case(thetaDeg, skinDepth, "local flux/absorption balance"));
                }
            }
        }

        [TestMethod]
        public void ReversingIncidentSide_SwapsFluxDirectionWithoutChangingScatteredEnergy()
        {
            Solver fromAbove = CreateTwoPlates(45.0, n: 25, skinDepth: 0.01);
            Solver fromBelow = CreateTwoPlates(-45.0, n: 25, skinDepth: 0.01);
            Assert.AreEqual(1, fromAbove.SolveDifr(), "from-above solver failed");
            Assert.AreEqual(1, fromBelow.SolveDifr(), "from-below solver failed");

            Solver.FarFieldScatteredEnergyComponents aboveFar =
                fromAbove.CalculateFarFieldScatteredEnergy(96, 120);
            Solver.FarFieldScatteredEnergyComponents belowFar =
                fromBelow.CalculateFarFieldScatteredEnergy(96, 120);
            Solver.PlateFluxComponents aboveFlux = fromAbove.CalculatePlateFluxComponents(160);
            Solver.PlateFluxComponents belowFlux = fromBelow.CalculatePlateFluxComponents(160);
            double incident = aboveFlux.Incident;

            Assert.AreEqual(aboveFar.TotalScattered, belowFar.TotalScattered, incident * 1e-10,
                "scattered energy must not depend on which side illuminates a zero-thickness sheet");
            Assert.AreEqual(aboveFlux.AboveFlux, -belowFlux.BelowFlux, incident * 1e-9,
                "upper total flux must map to lower flux with reversed normal");
            Assert.AreEqual(aboveFlux.BelowFlux, -belowFlux.AboveFlux, incident * 1e-9,
                "lower total flux must map to upper flux with reversed normal");
        }

        [TestMethod]
        public void TranslatingBothPlates_DoesNotChangeIntegratedScatteredEnergy()
        {
            Solver centered = CreateTwoPlates(45.0, n: 25, skinDepth: 0.01);
            Solver shifted = new Solver(5.5, 6.5, 7.5, 8.5, 1.0, Math.PI / 4.0, 25, 0.01);
            Assert.AreEqual(1, centered.SolveDifr(), "centered solver failed");
            Assert.AreEqual(1, shifted.SolveDifr(), "shifted solver failed");

            Solver.FarFieldScatteredEnergyComponents centeredFar =
                centered.CalculateFarFieldScatteredEnergy(96, 120);
            Solver.FarFieldScatteredEnergyComponents shiftedFar =
                shifted.CalculateFarFieldScatteredEnergy(96, 120);

            Assert.AreEqual(centeredFar.TotalScattered, shiftedFar.TotalScattered,
                centeredFar.TotalScattered * 1e-9, "translation invariance");
        }

        [TestMethod]
        public void ScatteredEnergy_ConvergesWithApproximationOrder()
        {
            Solver n15 = CreateTwoPlates(45.0, n: 15, skinDepth: 0.01);
            Solver n25 = CreateTwoPlates(45.0, n: 25, skinDepth: 0.01);
            Solver n35 = CreateTwoPlates(45.0, n: 35, skinDepth: 0.01);
            Assert.AreEqual(1, n15.SolveDifr(), "N=15 solver failed");
            Assert.AreEqual(1, n25.SolveDifr(), "N=25 solver failed");
            Assert.AreEqual(1, n35.SolveDifr(), "N=35 solver failed");

            double e15 = n15.CalculateFarFieldScatteredEnergy(96, 120).TotalScattered;
            double e25 = n25.CalculateFarFieldScatteredEnergy(96, 120).TotalScattered;
            double e35 = n35.CalculateFarFieldScatteredEnergy(96, 120).TotalScattered;
            double coarseChange = Math.Abs(e25 - e15) / e35;
            double fineChange = Math.Abs(e35 - e25) / e35;

            Assert.IsTrue(fineChange < coarseChange, "successive N refinement must reduce the energy change");
            Assert.IsTrue(fineChange < 0.001, "N=25 and N=35 scattered energies must agree within 0.1%");
        }

        [TestMethod]
        public void IncidentEnergy_UsesOnlyThePhysicalPlateLengths()
        {
            const double lambda = 2.0;
            const double theta = Math.PI / 6.0;
            Solver solver = new Solver(-3.0, -2.5, 1.0, 2.5, lambda, theta, 10, 0.01);
            double totalPlateLength = 0.5 + 1.5;
            double expected = 0.5 * (2.0 * Math.PI / lambda) * Math.Abs(Math.Sin(theta)) * totalPlateLength;

            Assert.AreEqual(expected, solver.CalculatePlateIncidentEnergy(), 1e-12,
                "the gap and any enclosing contour must not contribute to incident energy");
        }

        private static Solver CreateTwoPlates(double thetaDeg, int n, double skinDepth)
        {
            return new Solver(
                -1.5, -0.5, 0.5, 1.5,
                1.0,
                thetaDeg * Math.PI / 180.0,
                n,
                skinDepth);
        }

        private static string Case(double thetaDeg, double skinDepth, string detail)
        {
            return string.Format("theta={0}, skinDepth={1}: {2}", thetaDeg, skinDepth, detail);
        }

        private static void AssertFiniteNonNegative(double value, string message)
        {
            Assert.IsFalse(double.IsNaN(value), message + " is NaN");
            Assert.IsFalse(double.IsInfinity(value), message + " is infinite");
            Assert.IsTrue(value >= 0.0, message + " is negative");
        }
    }
}
