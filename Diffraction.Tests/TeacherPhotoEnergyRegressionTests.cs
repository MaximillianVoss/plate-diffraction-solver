using System;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    [TestCategory("DeepValidation")]
    public class TeacherPhotoEnergyRegressionTests
    {
        [TestMethod]
        public void SinglePlatePhotoSweep_HasSymmetricDecreasingScatteringAndClosesLocalBalance()
        {
            double previousReflected = double.PositiveInfinity;
            double previousForward = double.PositiveInfinity;
            double previousAbsorbed = double.NegativeInfinity;
            double firstReflectedFraction = double.NaN;
            double lastReflectedFraction = double.NaN;
            double lastAbsorbedFraction = double.NaN;

            for (int millimetres = 0; millimetres <= 13; millimetres++)
            {
                double skinDepth = millimetres / 1000.0;
                Solver solver = new Solver(
                    -1.0, 1.0,
                    1.0,
                    Math.PI / 4.0,
                    30,
                    skinDepth);

                Assert.AreEqual(1, solver.SolveDifr(), Case(skinDepth, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(angleSamples: 180, plateSamples: 240);
                Solver.ScatteredSheetFluxComponents sheet =
                    solver.CalculateScatteredSheetFluxComponents(samplesPerPlate: 200);
                Solver.EnergyComponents energy = solver.CalculateEnergyComponents();

                double reflectedFraction = far.ReflectedScattered / incident;
                double forwardFraction = far.TransmittedScattered / incident;
                double absorbedFraction = energy.Absorbed / incident;
                double localErrorFraction = Math.Abs(energy.LocalBalanceResidual) / incident;

                Assert.IsTrue(reflectedFraction >= 0.0, Case(skinDepth, "negative R_scat"));
                Assert.IsTrue(forwardFraction >= 0.0, Case(skinDepth, "negative T_scat"));
                Assert.IsTrue(absorbedFraction >= -1e-12, Case(skinDepth, "negative absorption"));
                Assert.AreEqual(reflectedFraction, forwardFraction, 1e-10,
                    Case(skinDepth, "far-field upper/lower mismatch"));
                Assert.IsTrue(sheet.AbsoluteMismatch / incident < 1e-10,
                    Case(skinDepth, "sheet upper/lower mismatch"));
                Assert.IsTrue(localErrorFraction < 0.001,
                    Case(skinDepth, "local energy-balance error exceeds 0.1%"));
                Assert.IsTrue(far.ReflectedScattered <= previousReflected + 1e-10,
                    Case(skinDepth, "R_scat increased"));
                Assert.IsTrue(far.TransmittedScattered <= previousForward + 1e-10,
                    Case(skinDepth, "T_scat increased"));
                Assert.IsTrue(energy.Absorbed + 1e-10 >= previousAbsorbed,
                    Case(skinDepth, "absorption decreased"));

                if (millimetres == 0)
                    firstReflectedFraction = reflectedFraction;
                if (millimetres == 13)
                {
                    lastReflectedFraction = reflectedFraction;
                    lastAbsorbedFraction = absorbedFraction;
                }

                previousReflected = far.ReflectedScattered;
                previousForward = far.TransmittedScattered;
                previousAbsorbed = energy.Absorbed;
            }

            Assert.AreEqual(0.994062, firstReflectedFraction, 0.0001,
                "the ideal-sheet endpoint must reproduce the photographed sweep");
            Assert.AreEqual(0.857172, lastReflectedFraction, 0.0002,
                "the skinDepth=0.013 endpoint must remain stable");
            Assert.AreEqual(0.120306, lastAbsorbedFraction, 0.001,
                "the skinDepth=0.013 absorption must remain stable at N=30");

            Solver photographedResolution = new Solver(
                -1.0, 1.0,
                1.0,
                Math.PI / 4.0,
                80,
                0.013);
            Assert.AreEqual(1, photographedResolution.SolveDifr(), "N=80 endpoint solver failed");

            double photographedIncident = photographedResolution.CalculatePlateIncidentEnergy();
            Solver.FarFieldScatteredEnergyComponents photographedFar =
                photographedResolution.CalculateFarFieldScatteredEnergy(angleSamples: 360, plateSamples: 640);
            double photographedAbsorbed = photographedResolution.CalculateAbsorbedEnergy();

            Assert.AreEqual(0.85680245, photographedFar.ReflectedScattered / photographedIncident, 0.00001,
                "N=80 R_scat must reproduce the photographed endpoint");
            Assert.AreEqual(0.85680245, photographedFar.TransmittedScattered / photographedIncident, 0.00001,
                "N=80 T_scat must reproduce the photographed endpoint");
            Assert.AreEqual(0.12211937, photographedAbsorbed / photographedIncident, 0.00001,
                "N=80 absorption must reproduce the photographed endpoint");
        }

        private static string Case(double skinDepth, string detail)
        {
            return string.Format("skinDepth={0:F3}: {1}", skinDepth, detail);
        }
    }
}
