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
        public void StandardSinglePlateEnergyClosesIndependentBalanceAndConverges()
        {
            Solver solver = CreateSolver(45.0, 0.01);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double incident = solver.CalculatePlateIncidentEnergy();
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(360, 240);
            Solver.ScatteredSheetFluxComponents sheet =
                solver.CalculateScatteredSheetFluxComponents(200);
            Solver.EnergyComponents energy = solver.CalculateEnergyComponents();

            // Old constants were snapshots of the singular finite-skin approximation,
            // not external references. Independent panels are checked in their own suite.
            Assert.AreEqual(far.ReflectedScattered, far.TransmittedScattered, incident * 1e-12);
            Assert.AreEqual(sheet.AboveOutgoing, sheet.BelowOutgoing, incident * 1e-12);
            Assert.IsTrue(sheet.AboveOutgoing >= 0);
            Assert.AreEqual(energy.Absorbed, energy.FluxAbsorbed, incident * 0.0001);
            AssertIndependentBalanceAndRefinement(solver);
        }

        [TestMethod]
        public void SkinSeriesClosesIndependentBalanceAndDecreasesSymmetrically()
        {
            double[] skinDepths = { 0.0, 0.001, 0.01, 0.05, 0.1 };

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
                AssertIndependentBalanceAndRefinement(solver);
                Assert.IsTrue(reflected <= previous + 1e-12, Case(45.0, skinDepths[i], "scattering increased"));
                previous = reflected;
            }
        }

        [TestMethod]
        public void AngleSeriesClosesIndependentBalanceAndConverges()
        {
            double[] angles = { 10.0, 30.0, 60.0, 90.0 };

            for (int i = 0; i < angles.Length; i++)
            {
                Solver solver = CreateSolver(angles[i], 0.01);
                Assert.AreEqual(1, solver.SolveDifr(), Case(angles[i], 0.01, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(360, 240);
                double reflected = far.ReflectedScattered / incident;

                Assert.AreEqual(reflected, far.TransmittedScattered / incident, 1e-12, Case(angles[i], 0.01, "R/T mismatch"));
                AssertIndependentBalanceAndRefinement(solver);
            }
        }

        private static void AssertIndependentBalanceAndRefinement(Solver solver)
        {
            double scattered = solver.CalculateFarFieldScatteredEnergy(360, 240).TotalScattered;
            double absorbed = solver.CalculateAbsorbedEnergy();
            double extinction = solver.CalculateExtinctionEnergy(400);
            Assert.IsTrue(extinction > 0);
            Assert.IsTrue(absorbed >= 0);
            Assert.AreEqual(extinction, scattered + absorbed, extinction * 0.001,
                "Independent extinction balance, no fitted component");
            var refined = new Solver(solver.a, solver.b, solver.lambda, solver.teta, 60, solver.skinDepth);
            Assert.AreEqual(1, refined.SolveDifr());
            Assert.AreEqual(refined.CalculateFarFieldScatteredEnergy(360, 480).TotalScattered,
                scattered, extinction * 0.001, "N refinement of scattering");
            Assert.AreEqual(refined.CalculateAbsorbedEnergy(), absorbed,
                extinction * 0.001, "N refinement of absorption");
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
