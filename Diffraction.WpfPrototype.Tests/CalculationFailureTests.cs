using Diffraction.WpfPrototype.Infrastructure;
using Diffraction.WpfPrototype.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class CalculationFailureTests
{
    [TestMethod]
    public void CorrectingOneInputKeepsTheOtherErrorVisible()
    {
        var viewModel = new MainViewModel();
        object wavelengthInput = new();
        object orderInput = new();
        viewModel.SetInputError(wavelengthInput, "WavelengthMicrometers", "Введите длину волны.");
        viewModel.SetInputError(orderInput, "HarmonicCount", "Введите N.");

        viewModel.SetInputError(orderInput, "HarmonicCount", null);

        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "Введите длину волны.");
        viewModel.SetInputError(wavelengthInput, "WavelengthMicrometers", null);
        Assert.IsFalse(viewModel.HasError);
    }

    [DataTestMethod]
    [DataRow("WavelengthMicrometers", 0.0, false, "Длина волны")]
    [DataRow("WavelengthMicrometers", -1.0, false, "Длина волны")]
    [DataRow("WavelengthMicrometers", double.NaN, false, "Длина волны")]
    [DataRow("WavelengthMicrometers", double.PositiveInfinity, false, "Длина волны")]
    [DataRow("IncidenceAngleDegrees", 0.0, false, "Угол")]
    [DataRow("IncidenceAngleDegrees", 180.0, false, "Угол")]
    [DataRow("IncidenceAngleDegrees", double.NaN, false, "Угол")]
    [DataRow("IncidenceAngleDegrees", double.PositiveInfinity, false, "Угол")]
    [DataRow("PlateStart", -0.5, false, "α₁ < β₁")]
    [DataRow("PlateEnd", -2.0, false, "α₁ < β₁")]
    [DataRow("PlateStart", double.NegativeInfinity, false, "α₁ < β₁")]
    [DataRow("HarmonicCount", 1.0, false, "N")]
    [DataRow("HarmonicCount", 201.0, false, "N")]
    [DataRow("SkinDepthMicrometers", -0.01, false, "скин-слоя")]
    [DataRow("SkinDepthMicrometers", double.NaN, false, "скин-слоя")]
    [DataRow("OutputLeft", 2.0, false, "Левая граница")]
    [DataRow("OutputRight", double.PositiveInfinity, false, "Левая граница")]
    [DataRow("OutputBottom", 3.0, false, "Нижняя граница")]
    [DataRow("OutputTop", double.NaN, false, "Нижняя граница")]
    [DataRow("SeriesSkinDepthStart", -0.01, true, "диапазон толщины")]
    [DataRow("SeriesSkinDepthEnd", double.NaN, true, "диапазон толщины")]
    [DataRow("SeriesPointCount", 102.0, true, "Число точек")]
    [DataRow("SeriesAngleStartDegrees", 0.0, true, "нулевым")]
    [DataRow("SeriesAngleStepDegrees", 0.0, true, "диапазон углов")]
    [DataRow("SeriesAngleStepDegrees", 1e-300, true, "181")]
    [DataRow("SeriesAngleEndDegrees", double.MaxValue, true, "181")]
    public async Task InvalidParametersReportReasonAndLeaveCalculationAvailable(
        string propertyName, double value, bool series, string expected)
    {
        var viewModel = new MainViewModel { IsSeriesMode = series };
        var property = viewModel.Parameters.GetType().GetProperty(propertyName)!;
        property.SetValue(viewModel.Parameters, Convert.ChangeType(value, property.PropertyType));

        await ((AsyncRelayCommand)viewModel.RunCommand).ExecuteAsync();

        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, expected);
        StringAssert.Contains(viewModel.StatusDetail, expected);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.RunCommand.CanExecute(null));
        Assert.AreEqual(0, viewModel.Runs.Count);
        Assert.IsFalse(viewModel.Energy.IsAvailable);
    }

    [TestMethod]
    public async Task UnexpectedPreparationErrorIsReportedAndCommandRecovers()
    {
        var viewModel = new MainViewModel();
        bool failOnce = true;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (failOnce && args.PropertyName == nameof(MainViewModel.IsBusy) && viewModel.IsBusy)
            {
                failOnce = false;
                throw new InvalidOperationException("Preparation failed");
            }
        };

        await ((AsyncRelayCommand)viewModel.RunCommand).ExecuteAsync();

        StringAssert.Contains(viewModel.ErrorMessage, "Preparation failed");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.RunCommand.CanExecute(null));
        Assert.AreEqual(0, viewModel.Runs.Count);
        Assert.IsTrue(viewModel.JournalEntries.Any(line => line.Contains("InvalidOperationException")));
    }

    [TestMethod]
    public async Task AsyncCommandReportsFaultAndAllowsNextExecution()
    {
        Exception? observed = null;
        int attempts = 0;
        var command = new AsyncRelayCommand(async () =>
        {
            await Task.Yield();
            if (++attempts == 1)
                throw new ArithmeticException("Computation failed");
        }, exception => observed = exception);

        await command.ExecuteAsync();
        Assert.IsInstanceOfType<ArithmeticException>(observed);
        Assert.IsTrue(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.AreEqual(2, attempts);
        Assert.IsTrue(command.CanExecute(null));
    }
}
