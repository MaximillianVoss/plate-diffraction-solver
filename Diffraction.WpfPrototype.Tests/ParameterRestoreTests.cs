using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class ParameterRestoreTests
{
    [TestMethod]
    public void OpenSeriesRunRestoresCompleteParameterSnapshot()
    {
        var viewModel = new MainViewModel();
        CalculationRun run = viewModel.Runs.Single(item => item.RunNumber == 22);

        viewModel.OpenRun(run);

        Assert.AreSame(run, viewModel.SelectedRun);
        Assert.AreEqual("Series", viewModel.CurrentSection);
        Assert.IsTrue(viewModel.IsSeriesMode);
        Assert.AreEqual("CPU", viewModel.SelectedBackend);
        AssertParameters(
            viewModel.Parameters,
            wavelength: 0.800,
            angle: 30.0,
            plateStart: -2.000,
            plateEnd: -0.750,
            harmonicCount: 30,
            skinDepth: 0.020,
            outputLeft: -3.0,
            outputRight: 3.0,
            outputBottom: -4.0,
            outputTop: 4.0,
            seriesStart: 0.005,
            seriesEnd: 0.080,
            seriesPoints: 16,
            angleStart: 15.0,
            angleEnd: 75.0,
            angleStep: 5.0);
    }

    [TestMethod]
    public void ReopeningSelectedRunDiscardsDraftWithoutChangingHistory()
    {
        var viewModel = new MainViewModel();
        Assert.IsNotNull(viewModel.SelectedRun);
        CalculationRun run = viewModel.SelectedRun;
        double storedWavelength = run.Parameters.WavelengthMicrometers;
        double storedSkinDepth = run.Parameters.SkinDepthMicrometers;

        viewModel.Parameters.WavelengthMicrometers = 9.5;
        viewModel.Parameters.SkinDepthMicrometers = 0.075;

        Assert.AreEqual(storedWavelength, run.Parameters.WavelengthMicrometers, 1e-12);
        Assert.AreEqual(storedSkinDepth, run.Parameters.SkinDepthMicrometers, 1e-12);

        viewModel.OpenRun(run);

        Assert.AreEqual(storedWavelength, viewModel.Parameters.WavelengthMicrometers, 1e-12);
        Assert.AreEqual(storedSkinDepth, viewModel.Parameters.SkinDepthMicrometers, 1e-12);
        Assert.IsFalse(viewModel.ContextTitle.Contains("изменены", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OpeningSingleRunRestoresModeBackendAndInputs()
    {
        var viewModel = new MainViewModel();
        CalculationRun seriesRun = viewModel.Runs.Single(item => item.RunNumber == 22);
        CalculationRun singleRun = viewModel.Runs.Single(item => item.RunNumber == 23);

        viewModel.OpenRun(seriesRun);
        viewModel.OpenRun(singleRun);

        Assert.AreEqual("Calculations", viewModel.CurrentSection);
        Assert.IsTrue(viewModel.IsSingleMode);
        Assert.AreEqual("CPU", viewModel.SelectedBackend);
        Assert.AreEqual(1.200, viewModel.Parameters.WavelengthMicrometers, 1e-12);
        Assert.AreEqual(30.0, viewModel.Parameters.IncidenceAngleDegrees, 1e-12);
        Assert.AreEqual(20, viewModel.Parameters.HarmonicCount);
        Assert.AreEqual(0.010, viewModel.Parameters.SkinDepthMicrometers, 1e-12);
        StringAssert.Contains(viewModel.ContextSummary, "λ 1,200");
    }

    private static void AssertParameters(
        CalculationParameters actual,
        double wavelength,
        double angle,
        double plateStart,
        double plateEnd,
        int harmonicCount,
        double skinDepth,
        double outputLeft,
        double outputRight,
        double outputBottom,
        double outputTop,
        double seriesStart,
        double seriesEnd,
        int seriesPoints,
        double angleStart,
        double angleEnd,
        double angleStep)
    {
        Assert.AreEqual(wavelength, actual.WavelengthMicrometers, 1e-12);
        Assert.AreEqual(angle, actual.IncidenceAngleDegrees, 1e-12);
        Assert.AreEqual(plateStart, actual.PlateStart, 1e-12);
        Assert.AreEqual(plateEnd, actual.PlateEnd, 1e-12);
        Assert.AreEqual(harmonicCount, actual.HarmonicCount);
        Assert.AreEqual(skinDepth, actual.SkinDepthMicrometers, 1e-12);
        Assert.AreEqual(outputLeft, actual.OutputLeft, 1e-12);
        Assert.AreEqual(outputRight, actual.OutputRight, 1e-12);
        Assert.AreEqual(outputBottom, actual.OutputBottom, 1e-12);
        Assert.AreEqual(outputTop, actual.OutputTop, 1e-12);
        Assert.AreEqual(seriesStart, actual.SeriesSkinDepthStart, 1e-12);
        Assert.AreEqual(seriesEnd, actual.SeriesSkinDepthEnd, 1e-12);
        Assert.AreEqual(seriesPoints, actual.SeriesPointCount);
        Assert.AreEqual(angleStart, actual.SeriesAngleStartDegrees, 1e-12);
        Assert.AreEqual(angleEnd, actual.SeriesAngleEndDegrees, 1e-12);
        Assert.AreEqual(angleStep, actual.SeriesAngleStepDegrees, 1e-12);
    }
}
