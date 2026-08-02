using System;
using System.Linq;
using Diffraction.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Compl = Diffraction.Core.DiffractionMath.Compl;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    [TestClass]
    public class EnergyDiagnosticsReportTests
    {
        [TestMethod]
        public void StandardTwoPlateReport_PresentsScatteredEnergyAsPrimaryNonNegativeResult()
        {
            Solver solver = CreateTwoPlates(n: 30, skinDepth: 0.01, thetaDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.01, "СО СКИН-СЛОЕМ");

            Assert.IsFalse(report.HasWarning, report.Message);
            Assert.IsTrue(report.ReflectedScattered >= 0.0, "reflected scattered energy");
            Assert.IsTrue(report.ForwardScattered >= 0.0, "forward scattered energy");
            Assert.AreEqual(report.ReflectedScattered, report.ForwardScattered, 1e-10,
                "upper and lower scattered half-planes");
            StringAssert.Contains(report.Message, "Энергии рассеянного поля (основной результат)");
            StringAssert.Contains(report.Message, "Отражённая R_scat");
            StringAssert.Contains(report.Message, "Рассеянная вперёд T_scat");
            StringAssert.Contains(report.Message, "неотрицательны и совпадают");
            Assert.IsTrue(
                report.Message.IndexOf("3. Энергии рассеянного поля", StringComparison.Ordinal) <
                report.Message.IndexOf("4. ЗСЭ по полным знаковым потокам", StringComparison.Ordinal),
                "primary scattered energies must precede local signed diagnostics");
        }

        [TestMethod]
        public void NegativeLocalSignedFlux_IsNotReportedAsNegativeForwardScatteredEnergy()
        {
            Solver solver = CreateTwoPlates(n: 30, skinDepth: 0.01, thetaDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.01, "СО СКИН-СЛОЕМ");

            Assert.IsTrue(report.OppositeSideSignedFlux < 0.0,
                "this regression case must retain the negative local signed diagnostic");
            Assert.IsTrue(report.ForwardScattered > 0.0,
                "forward scattered energy must remain nonnegative");
            StringAssert.Contains(report.Message, "Знаковый поток с другой стороны");
            StringAssert.Contains(report.Message,
                "Отрицательный знаковый поток здесь допустим и не является отрицательной T_scat.");
            Assert.IsFalse(report.Message.Contains("Прошедшая энергия:"),
                "the signed local flux must not be labelled as transmitted energy");
        }

        [TestMethod]
        public void IdealConductorReport_UsesZeroAbsorptionAndIdealBoundaryLabel()
        {
            Solver solver = CreateTwoPlates(n: 30, skinDepth: 0.0, thetaDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.0, "БЕЗ СКИН-СЛОЯ");

            Assert.AreEqual("Контроль точности (идеальный проводник)", report.Title);
            StringAssert.Contains(report.Message, "Невязка u = 0");
            StringAssert.Contains(report.Message, "Поглощение A:                   0.000000");
            Assert.IsFalse(report.Message.Contains("A_J по поверхностному току"));
        }

        [TestMethod]
        public void GrazingIncidenceReport_UsesNotApplicableFractionsInsteadOfNaNOrInfinity()
        {
            Solver solver = CreateTwoPlates(n: 20, skinDepth: 0.01, thetaDeg: 0.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.01, "СКОЛЬЗЯЩЕЕ ПАДЕНИЕ");

            Assert.IsFalse(report.ReferenceValid, "zero projected incident energy has no percentage reference");
            StringAssert.Contains(report.Message, "нормированные доли не определены");
            StringAssert.Contains(report.Message, "н/д");
            Assert.IsFalse(report.Message.Contains("NaN"));
            Assert.IsFalse(report.Message.Contains("Infinity"));
        }

        [TestMethod]
        public void FailedBoundaryCheck_SetsWarningAndPrintsTheSameStatus()
        {
            Solver solver = CreateTwoPlates(n: 3, skinDepth: 0.01, thetaDeg: 45.0);
            Compl[] zeroCoefficients = Enumerable.Range(0, 6).Select(_ => new Compl()).ToArray();
            solver.ApplySolvedCoefficients(zeroCoefficients, "test", 0.0, 0.0, 0.0, usedCuda: false);

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.01, "ИСКУССТВЕННО НЕКОРРЕКТНОЕ РЕШЕНИЕ");

            Assert.IsTrue(report.HasWarning, "failed numerical checks must raise the report warning state");
            Assert.IsFalse(report.BoundaryConditionOk, "zero current cannot satisfy the incident-field boundary condition");
            StringAssert.Contains(report.Message, "ВНИМАНИЕ: увеличьте N и проверьте сходимость.");
        }

        [TestMethod]
        public void Compose_PreservesReportOrderAndHandlesMissingReports()
        {
            Assert.AreEqual(
                "Подробная диагностика отсутствует.",
                EnergyDiagnosticsReportBuilder.Compose(null, null));

            Solver idealSolver = CreateTwoPlates(n: 20, skinDepth: 0.0, thetaDeg: 45.0);
            Solver skinSolver = CreateTwoPlates(n: 20, skinDepth: 0.01, thetaDeg: 45.0);
            Assert.AreEqual(1, idealSolver.SolveDifr(), "ideal solver failed");
            Assert.AreEqual(1, skinSolver.SolveDifr(), "skin solver failed");
            EnergyDiagnosticsReport ideal = EnergyDiagnosticsReportBuilder.Build(idealSolver, 0.0, "ИДЕАЛЬНЫЙ");
            EnergyDiagnosticsReport skin = EnergyDiagnosticsReportBuilder.Build(skinSolver, 0.01, "СКИН");

            string combined = EnergyDiagnosticsReportBuilder.Compose(ideal, skin);

            Assert.IsTrue(combined.IndexOf(ideal.Title, StringComparison.Ordinal) <
                combined.IndexOf(skin.Title, StringComparison.Ordinal));
            StringAssert.Contains(combined, "ИДЕАЛЬНЫЙ");
            StringAssert.Contains(combined, "СКИН");
        }

        [TestMethod]
        public void ImpedanceReport_FormatsNegativeImaginaryPartWithoutDoubleSign()
        {
            Solver solver = CreateTwoPlates(n: 20, skinDepth: 0.01, thetaDeg: 45.0);
            Assert.AreEqual(1, solver.SolveDifr(), "solver failed");

            EnergyDiagnosticsReport report = EnergyDiagnosticsReportBuilder.Build(
                solver, 0.01, "СКИН");

            StringAssert.Contains(report.Message, "q = ");
            StringAssert.Contains(report.Message, " - ");
            Assert.IsFalse(report.Message.Contains("+ -"), "complex values must use a single explicit sign");
        }

        private static Solver CreateTwoPlates(int n, double skinDepth, double thetaDeg)
        {
            return new Solver(
                -1.5, -0.5, 0.5, 1.5,
                1.0,
                thetaDeg * Math.PI / 180.0,
                n,
                skinDepth);
        }
    }
}
