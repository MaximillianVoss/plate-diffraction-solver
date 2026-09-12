using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    [TestCategory("DeepValidation")]
    public class EnergyReferenceTests
    {
        [TestMethod]
        public void StandardSinglePlateEnergyMatchesValidatedReference()
        {
            Solver solver = CreateSolver(45.0, 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double incident = solver.CalculatePlateIncidentEnergy();
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(360, 240);
            Solver.ScatteredSheetFluxComponents sheet =
                solver.CalculateScatteredSheetFluxComponents(200);
            Solver.EnergyComponents energy = solver.CalculateEnergyComponents();

            Assert.AreEqual(0.8626996341671146, far.ReflectedScattered / incident, 1e-12);
            Assert.AreEqual(0.8626996341671144, far.TransmittedScattered / incident, 1e-12);
            Assert.AreEqual(0.1159575597470231, energy.Absorbed / incident, 1e-12);
            Assert.AreEqual(0.8596514326575219, sheet.AboveOutgoing / incident, 1e-12);
            Assert.AreEqual(0.8596514326575219, sheet.BelowOutgoing / incident, 1e-12);
            Assert.AreEqual(0.1164282883935220, energy.FluxAbsorbed / incident, 1e-12);
            Assert.AreEqual(
                0.0470728646498805,
                Math.Abs(energy.LocalBalanceResidual) / incident * 100.0,
                1e-12);
        }

        [TestMethod]
        public void SkinSeriesMatchesValidatedReferenceAndDecreasesSymmetrically()
        {
            double[] skinDepths = { 0.0, 0.001, 0.01, 0.05, 0.1 };
            double[] expectedScattering =
            {
                0.9962685338758368,
                0.9789497545802383,
                0.8626996341671146,
                0.5571341431178170,
                0.3608204961050888
            };

            double previous = double.PositiveInfinity;
            for (int i = 0; i < skinDepths.Length; i++)
            {
                Solver solver = CreateSolver(45.0, skinDepths[i]);
                Assert.AreEqual(1, solver.SolveDifr(), Case(45.0, skinDepths[i], "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(360, 240);
                double reflected = far.ReflectedScattered / incident;
                double forward = far.TransmittedScattered / incident;

                Assert.AreEqual(reflected, forward, 1e-12, Case(45.0, skinDepths[i], "R/T mismatch"));
                Assert.AreEqual(expectedScattering[i], reflected, 1e-12, Case(45.0, skinDepths[i], "reference mismatch"));
                Assert.IsTrue(reflected <= previous + 1e-12, Case(45.0, skinDepths[i], "scattering increased"));
                previous = reflected;
            }
        }

        [TestMethod]
        public void AngleSeriesMatchesValidatedReference()
        {
            double[] angles = { 10.0, 30.0, 60.0, 90.0 };
            double[] expectedScattering =
            {
                2.2979627925904880,
                0.9822801249475397,
                0.8505468571671618,
                0.8562583829389787
            };

            for (int i = 0; i < angles.Length; i++)
            {
                Solver solver = CreateSolver(angles[i], 0.01);
                Assert.AreEqual(1, solver.SolveDifr(), Case(angles[i], 0.01, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(360, 240);
                double reflected = far.ReflectedScattered / incident;

                Assert.AreEqual(reflected, far.TransmittedScattered / incident, 1e-12, Case(angles[i], 0.01, "R/T mismatch"));
                Assert.AreEqual(expectedScattering[i], reflected, 1e-12, Case(angles[i], 0.01, "reference mismatch"));
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
