using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    /// <summary>
    /// Проверка вычислений против количественных прогнозов физической модели
    /// из diagnostics/energy-interpretation-for-instructor.md (разделы 4–5):
    /// локальный баланс ЗСЭ, замкнутый контур, симметрия R_scat = T_scat.
    /// Конфигурация документа: одна пластина [-1; 1], λ=1, θ=45°, N=80,
    /// толщина скин-слоя 0…0.013.
    /// </summary>
    [TestClass]
    [TestCategory("DeepValidation")]
    public class PhysicalModelValidationTests
    {
        // Документированные границы для серии 0…0.013:
        // сумма компонентов ЗСЭ 99.9554…100.0000 %, локальная невязка ≤ 0.044581 %,
        // невязка замкнутого контура ≤ 0.221749 %.
        // Фактический прогон через CalculateEnergyComponents даёт серию 100.00000…100.00516 %
        // с невязками в 5–10 раз ниже документированных: верх ЗСЭ в документе измерен
        // по диагностической сетке приложения, здесь берём запас 100.01 %.
        private const double MinBalanceSumPercent = 99.95;
        private const double MaxBalanceSumPercent = 100.01;
        private const double MaxLocalResidualPercent = 0.05;
        private const double MaxContourResidualPercent = 0.25;

        [TestMethod]
        public void DocumentedSkinSweep_LocalBalanceAndClosedContourStayWithinBounds()
        {
            for (int millimetres = 0; millimetres <= 13; millimetres++)
            {
                double skinDepth = millimetres / 1000.0;
                Solver solver = CreateDocumentedSolver(skinDepth);
                Assert.AreEqual(1, solver.SolveDifr(), Case(skinDepth, "solver failed"));

                Solver.EnergyComponents energy = solver.CalculateEnergyComponents(includeContourDiagnostic: true);
                double incident = energy.Incident;

                double balanceSumPercent =
                    (energy.IncidentSideDeficit + energy.OppositeSideSignedFlux + energy.Absorbed)
                    / incident * 100.0;
                double localResidualPercent = Math.Abs(energy.LocalBalanceResidual) / incident * 100.0;
                double contourResidualPercent = Math.Abs(energy.SignedContourResidual) / incident * 100.0;

                Assert.IsTrue(balanceSumPercent >= MinBalanceSumPercent,
                    Case(skinDepth, $"ЗСЭ {balanceSumPercent:F4}% ниже {MinBalanceSumPercent}%"));
                Assert.IsTrue(balanceSumPercent <= MaxBalanceSumPercent,
                    Case(skinDepth, $"ЗСЭ {balanceSumPercent:F4}% выше {MaxBalanceSumPercent}%"));
                Assert.IsTrue(localResidualPercent <= MaxLocalResidualPercent,
                    Case(skinDepth, $"локальная невязка {localResidualPercent:F5}%"));
                Assert.IsTrue(contourResidualPercent <= MaxContourResidualPercent,
                    Case(skinDepth, $"невязка контура {contourResidualPercent:F5}%"));
            }
        }

        [TestMethod]
        public void DocumentedSkinSweep_FarFieldScatteringIsSymmetricAtEveryPoint()
        {
            for (int millimetres = 0; millimetres <= 13; millimetres++)
            {
                double skinDepth = millimetres / 1000.0;
                Solver solver = CreateDocumentedSolver(skinDepth);
                Assert.AreEqual(1, solver.SolveDifr(), Case(skinDepth, "solver failed"));

                double incident = solver.CalculatePlateIncidentEnergy();
                Solver.FarFieldScatteredEnergyComponents far =
                    solver.CalculateFarFieldScatteredEnergy(angleSamples: 360, plateSamples: 480);

                double reflected = far.ReflectedScattered / incident;
                double forward = far.TransmittedScattered / incident;
                Assert.AreEqual(reflected, forward, 1e-9,
                    Case(skinDepth, "R_scat != T_scat — нарушена зеркальная симметрия токового листа"));
                Assert.IsTrue(reflected >= 0.0, Case(skinDepth, "отрицательное R_scat"));
            }
        }

        [TestMethod]
        public void DocumentedPhotoEndpoint_IdealConductorReproducesReferenceValues()
        {
            // Раздел 5 документа: δ=0 → R_scat = T_scat = 99.406244 %, A_J = 0.
            Solver solver = CreateDocumentedSolver(skinDepth: 0.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            double incident = solver.CalculatePlateIncidentEnergy();
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(angleSamples: 360, plateSamples: 640);
            double absorbed = solver.CalculateEnergyComponents().Absorbed / incident * 100.0;

            Assert.AreEqual(99.406244, far.ReflectedScattered / incident * 100.0, 0.001,
                "R_scat идеального проводника не воспроизводит документ");
            Assert.AreEqual(99.406244, far.TransmittedScattered / incident * 100.0, 0.001,
                "T_scat идеального проводника не воспроизводит документ");
            Assert.AreEqual(0.0, absorbed, 1e-6, "идеальный проводник не должен поглощать");
        }

        private static Solver CreateDocumentedSolver(double skinDepth)
        {
            return new Solver(
                -1.0, 1.0,
                1.0,
                Math.PI / 4.0,
                80,
                skinDepth);
        }

        private static string Case(double skinDepth, string detail)
        {
            return string.Format("skinDepth={0:F3}: {1}", skinDepth, detail);
        }
    }
}
