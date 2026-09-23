using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Diffraction.Core;
using Solver = Diffraction.Core.DiffractionMath.DifrOnLenta;

namespace Diffraction.Tests
{
    /// <summary>
    /// Дымовые тесты пути вычислений WinForms-приложения (Form1.cs):
    /// то же самое API Diffraction.Core, что использует форма, без UI.
    /// Закрывают ручную проверку «запустил форму и прокликал расчёт».
    /// </summary>
    [TestClass]
    [TestCategory("WinFormsPath")]
    public class WinFormsPathSmokeTests
    {
        private const double PlateStart = -1.0;
        private const double PlateEnd = 1.0;
        private const double Wavelength = 1.0;
        private const int ApproximationOrder = 10;

        [TestMethod]
        public void CollocationSolveWithCancelledTokenReturnsZeroAndSetsFlag()
        {
            // Контракт Form1: SolveDifr(token) при отмене не бросает исключение,
            // а возвращает 0 и выставляет LastSolveCancelled.
            var solver = CreateSolver(angleDegrees: 30.0, skinDepth: 0.0);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                int result = solver.SolveDifr(cts.Token);
                Assert.AreEqual(0, result);
                Assert.IsTrue(solver.LastSolveCancelled);
            }
        }

        [TestMethod]
        public void GalerkinSolveWithCancelledTokenThrowsOperationCanceled()
        {
            // GalerkinSolver.SolveSinglePlate при отмененном токене бросает
            // OperationCanceledException — вызывающий код (Form1/сервис WPF)
            // обязан перехватывать его сам, как и любую отмену.
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.ThrowsException<OperationCanceledException>(() =>
                    GalerkinSolver.SolveSinglePlate(
                        PlateStart, PlateEnd, Wavelength,
                        DegToRad(30.0), ApproximationOrder, 0.0, cts.Token));
            }
        }

        [TestMethod]
        public void WinFormsEnergyPath_IdealConductorDoesNotAbsorb()
        {
            Solver solver = Solve(30.0, skinDepth: 0.0);
            double absorbed = solver.CalculateEnergyComponents().Absorbed
                / solver.CalculatePlateIncidentEnergy();
            Assert.AreEqual(0.0, absorbed, 1e-9, "идеальный проводник не должен поглощать");
        }

        [TestMethod]
        public void WinFormsEnergyPath_SkinLayerAbsorbsAndReducesScattering()
        {
            Solver ideal = Solve(30.0, skinDepth: 0.0);
            Solver skin = Solve(30.0, skinDepth: 0.001);

            double idealAbsorbed = ideal.CalculateEnergyComponents().Absorbed
                / ideal.CalculatePlateIncidentEnergy();
            double skinAbsorbed = skin.CalculateEnergyComponents().Absorbed
                / skin.CalculatePlateIncidentEnergy();

            Assert.AreEqual(0.0, idealAbsorbed, 1e-9);
            Assert.IsTrue(skinAbsorbed > 1e-6,
                $"скин-слой должен поглощать, получено {skinAbsorbed:E3}");

            double idealScattering = TotalScattering(ideal);
            double skinScattering = TotalScattering(skin);
            Assert.IsTrue(skinScattering < idealScattering,
                $"скин-слой должен уменьшать рассеяние: {idealScattering:F6} -> {skinScattering:F6}");
        }

        [TestMethod]
        public void WinFormsEnergyPath_CollocationMatchesGalerkin()
        {
            // Form1 сравнивает коллокацию и Галеркина на одной задаче —
            // энергетика двух методов должна совпадать.
            const double skinDepth = 0.001;
            Solver collocation = Solve(30.0, skinDepth);
            Solver galerkin = GalerkinSolver.SolveSinglePlate(
                PlateStart, PlateEnd, Wavelength,
                DegToRad(30.0), ApproximationOrder, skinDepth);

            double collocationAbsorbed = collocation.CalculateEnergyComponents().Absorbed
                / collocation.CalculatePlateIncidentEnergy();
            double galerkinAbsorbed = galerkin.CalculateEnergyComponents().Absorbed
                / galerkin.CalculatePlateIncidentEnergy();

            Assert.AreEqual(collocationAbsorbed, galerkinAbsorbed, 1e-3,
                $"коллокация {collocationAbsorbed:F9} и Галеркин {galerkinAbsorbed:F9} расходятся");
        }

        private static Solver Solve(double angleDegrees, double skinDepth)
        {
            Solver solver = CreateSolver(angleDegrees, skinDepth);
            Assert.AreEqual(1, solver.SolveDifr(), $"solver failed: theta={angleDegrees}, skin={skinDepth}");
            return solver;
        }

        private static Solver CreateSolver(double angleDegrees, double skinDepth)
        {
            return new Solver(
                PlateStart,
                PlateEnd,
                Wavelength,
                DegToRad(angleDegrees),
                ApproximationOrder,
                skinDepth);
        }

        private static double TotalScattering(Solver solver)
        {
            Solver.FarFieldScatteredEnergyComponents far =
                solver.CalculateFarFieldScatteredEnergy(360, 240);
            return (far.ReflectedScattered + far.TransmittedScattered)
                / solver.CalculatePlateIncidentEnergy();
        }

        private static double DegToRad(double degrees) => degrees * Math.PI / 180.0;
    }
}
