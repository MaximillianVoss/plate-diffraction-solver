using System;
using Diffraction.WpfPrototype.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    [TestCategory("DeepValidation")]
    public class WpfEnergyReferenceTests
    {
        [TestMethod]
        public void SelectedWpfEnergySnapshot_MatchesCoreResult()
        {
            EnergyDemoSnapshot expected = ValidatedEnergyDemo.Selected;
            Solver solver = CreateSolver(45.0, expected.SkinDepth);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double incident = solver.CalculatePlateIncidentEnergy();
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(360, 240);
            Solver.ScatteredSheetFluxComponents sheet =
                solver.CalculateScatteredSheetFluxComponents(200);
            Solver.EnergyComponents energy = solver.CalculateEnergyComponents();

            Assert.AreEqual(expected.ReflectedScattered, far.ReflectedScattered / incident, 1e-12);
            Assert.AreEqual(expected.ForwardScattered, far.TransmittedScattered / incident, 1e-12);
            Assert.AreEqual(expected.Absorbed, energy.Absorbed / incident, 1e-12);
            Assert.AreEqual(expected.SheetAbove, sheet.AboveOutgoing / incident, 1e-12);
            Assert.AreEqual(expected.SheetBelow, sheet.BelowOutgoing / incident, 1e-12);
            Assert.AreEqual(expected.FluxAbsorbed, energy.FluxAbsorbed / incident, 1e-12);
            Assert.AreEqual(
                expected.LocalBalanceErrorPercent,
                Math.Abs(energy.LocalBalanceResidual) / incident * 100.0,
                1e-12);
        }

        [TestMethod]
        public void WpfSkinSeries_MatchesCoreAndDecreasesSymmetrically()
        {
            double previous = double.PositiveInfinity;
            foreach (double skinDepth in new[] { 0.0, 0.001, 0.01, 0.05, 0.1 })
            {
                Solver solver = CreateSolver(45.0, skinDepth);
                Assert.AreEqual(1, solver.SolveDifr(), Case(45.0, skinDepth, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(360, 240);
                double reflected = far.ReflectedScattered / incident;
                double forward = far.TransmittedScattered / incident;

                Assert.AreEqual(reflected, forward, 1e-12, Case(45.0, skinDepth, "R/T mismatch"));
                Assert.AreEqual(
                    ValidatedEnergyDemo.ScatteringAtSkinDepth(skinDepth),
                    reflected,
                    1e-12,
                    Case(45.0, skinDepth, "WPF skin curve mismatch"));
                Assert.IsTrue(reflected <= previous + 1e-12,
                    Case(45.0, skinDepth, "scattering increased"));
                previous = reflected;
            }
        }

        [TestMethod]
        public void WpfAngleSeries_MatchesCoreAndKeepsUpperLowerFluxEqual()
        {
            foreach (double angle in new[] { 10.0, 30.0, 60.0, 90.0 })
            {
                Solver solver = CreateSolver(angle, 0.01);
                Assert.AreEqual(1, solver.SolveDifr(), Case(angle, 0.01, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(360, 240);
                double reflected = far.ReflectedScattered / incident;
                double forward = far.TransmittedScattered / incident;

                Assert.AreEqual(reflected, forward, 1e-12, Case(angle, 0.01, "R/T mismatch"));
                Assert.AreEqual(
                    ValidatedEnergyDemo.ScatteringAtAngle(angle),
                    reflected,
                    1e-12,
                    Case(angle, 0.01, "WPF angle curve mismatch"));
            }
        }

        private static Solver CreateSolver(double angleDegrees, double skinDepth)
        {
            return new Solver(
                -1.5,
                -0.5,
                1.0,
                angleDegrees * Math.PI / 180.0,
                30,
                skinDepth);
        }

        private static string Case(double angle, double skinDepth, string detail)
        {
            return string.Format("theta={0:F1}, skinDepth={1:F3}: {2}", angle, skinDepth, detail);
        }
    }
}
