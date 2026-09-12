using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class CalculationServiceTests
{
    [TestMethod]
    public void SingleCalculationProducesCoreBackedPlotsCoefficientsAndDiagnostics()
    {
        CalculationParameters parameters = CreateFastParameters();

        CalculationOutput output = DiffractionCalculationService.Calculate(
            parameters,
            includeSeries: false,
            CancellationToken.None,
            includeFieldMaps: false);

        Assert.IsTrue(output.Energy.IsAvailable);
        Assert.AreEqual(parameters.SkinDepthMicrometers, output.Energy.SkinDepth, 1e-12);
        Assert.AreEqual(parameters.HarmonicCount, output.Coefficients.Count);
        Assert.IsTrue(output.SlicePlot.HasData);
        Assert.IsTrue(output.MethodPlot.HasData);
        Assert.IsTrue(output.MethodDifferencePlot.HasData);
        Assert.IsTrue(output.CurrentEnergyPlot.HasData);
        Assert.IsFalse(output.SkinEnergyPlot.HasData);
        Assert.IsTrue(output.Diagnostics.Count >= 8);
        Assert.IsTrue(output.DiagnosticsSummary.Contains("Галеркин", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ChangingPhysicalInputsChangesCalculatedResults()
    {
        CalculationParameters firstParameters = CreateFastParameters();
        CalculationParameters secondParameters = CreateFastParameters();
        secondParameters.SkinDepthMicrometers = 0.05;
        secondParameters.IncidenceAngleDegrees = 60.0;

        CalculationOutput first = DiffractionCalculationService.Calculate(
            firstParameters,
            includeSeries: false,
            CancellationToken.None,
            includeFieldMaps: false);
        CalculationOutput second = DiffractionCalculationService.Calculate(
            secondParameters,
            includeSeries: false,
            CancellationToken.None,
            includeFieldMaps: false);

        Assert.IsTrue(Math.Abs(first.Energy.ReflectedScattered - second.Energy.ReflectedScattered) > 1e-4);
        Assert.IsTrue(Math.Abs(first.Energy.Absorbed - second.Energy.Absorbed) > 1e-4);
        Assert.IsTrue(Math.Abs(first.Coefficients[0].CollocationRe - second.Coefficients[0].CollocationRe) > 1e-5);
    }

    [TestMethod]
    public void SeriesCalculationUsesRequestedSkinAndAngleGrids()
    {
        CalculationParameters parameters = CreateFastParameters();
        parameters.SeriesSkinDepthStart = 0.0;
        parameters.SeriesSkinDepthEnd = 0.02;
        parameters.SeriesPointCount = 3;
        parameters.SeriesAngleStartDegrees = 30.0;
        parameters.SeriesAngleEndDegrees = 50.0;
        parameters.SeriesAngleStepDegrees = 20.0;

        CalculationOutput output = DiffractionCalculationService.Calculate(
            parameters,
            includeSeries: true,
            CancellationToken.None,
            includeFieldMaps: false);

        Assert.AreEqual(3, output.SkinEnergyPlot.Series[0].Points.Count);
        Assert.AreEqual(2, output.AngleEnergyPlot.Series[0].Points.Count);
        Assert.AreEqual(3, output.SeriesDiagnosticsPlot.Series[0].Points.Count);
        Assert.AreEqual(3, output.SeriesCheckSummaries.Count);
    }

    [DataTestMethod]
    [DataRow(10)]
    [DataRow(55)]
    [TestCategory("DeepValidation")]
    public void TeacherOrdersProduceFiniteFullSinglePlateOutput(int approximationOrder)
    {
        CalculationParameters parameters = CreateFastParameters();
        parameters.HarmonicCount = approximationOrder;
        parameters.OutputLeft = -2.0;
        parameters.OutputRight = 2.0;
        parameters.OutputBottom = -3.0;
        parameters.OutputTop = 3.0;

        CalculationOutput output = DiffractionCalculationService.Calculate(
            parameters,
            includeSeries: false,
            CancellationToken.None,
            includeFieldMaps: true);

        Assert.IsTrue(double.IsFinite(output.Energy.ReflectedScattered));
        Assert.IsTrue(double.IsFinite(output.Energy.Absorbed));
        Assert.AreEqual(approximationOrder, output.Coefficients.Count);
        Assert.IsNotNull(output.IdealFieldMap);
        Assert.IsNotNull(output.SkinFieldMap);
        Assert.AreEqual(output.IdealFieldMap.ScaleMaximum, output.SkinFieldMap.ScaleMaximum, 0.0);
        Assert.IsTrue(output.MethodDifferencePlot.Series[0].Points.All(point => double.IsFinite(point.Y)));
    }

    private static CalculationParameters CreateFastParameters()
    {
        return new CalculationParameters
        {
            WavelengthMicrometers = 1.0,
            IncidenceAngleDegrees = 45.0,
            PlateStart = -1.5,
            PlateEnd = -0.5,
            HarmonicCount = 6,
            SkinDepthMicrometers = 0.01,
            OutputLeft = -1.8,
            OutputRight = -0.2,
            OutputBottom = -1.0,
            OutputTop = 1.0
        };
    }
}
