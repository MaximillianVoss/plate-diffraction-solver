using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class NumericOutputTests
{
    [DataTestMethod]
    [DataRow("SkinDepthMicrometers", double.MaxValue)]
    [DataRow("OutputRight", 1e307)]
    public void NonFiniteComputationFailsInsteadOfPublishingInvalidResults(string propertyName, double value)
    {
        var parameters = CalculationParameters.CreateDefault();
        parameters.HarmonicCount = 2;
        parameters.SkinDepthMicrometers = 0;
        parameters.OutputLeft = 0;
        typeof(CalculationParameters).GetProperty(propertyName)!.SetValue(parameters, value);

        Exception? error = null;
        try
        {
            DiffractionCalculationService.Calculate(parameters, false, CancellationToken.None);
        }
        catch (Exception exception)
        {
            error = exception;
        }

        Assert.IsNotNull(error, "An invalid computation must fail before returning results.");
        IEnumerable<Exception> causes = error is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions
            : new[] { error };
        foreach (Exception cause in causes)
        {
            Assert.IsInstanceOfType<InvalidOperationException>(cause);
            StringAssert.Contains(cause.Message, "числ");
        }
    }

    [TestMethod]
    public void SingleExtremeAngleHasFiniteAxisBoundsAndSpan()
    {
        var parameters = CalculationParameters.CreateDefault();
        parameters.HarmonicCount = 2;
        parameters.SkinDepthMicrometers = 0;
        parameters.SeriesPointCount = 2;
        parameters.SeriesSkinDepthStart = parameters.SeriesSkinDepthEnd = 0;
        parameters.SeriesAngleStartDegrees = parameters.SeriesAngleEndDegrees = double.MaxValue;

        var output = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);

        Assert.IsTrue(double.IsFinite(output.AngleEnergyPlot.XMinimum));
        Assert.IsTrue(double.IsFinite(output.AngleEnergyPlot.XMaximum));
        Assert.IsTrue(double.IsFinite(output.AngleEnergyPlot.XMaximum - output.AngleEnergyPlot.XMinimum));
        Assert.IsTrue(output.AngleEnergyPlot.XMaximum > output.AngleEnergyPlot.XMinimum);
        Assert.AreEqual(double.MaxValue, output.AngleEnergyPlot.Series[0].Points[0].X);
    }

    [DataTestMethod]
    [DataRow(double.NaN, 1.0)]
    [DataRow(0.0, double.PositiveInfinity)]
    [DataRow(-double.MaxValue, double.MaxValue)]
    [DataRow(1.0, 1.0)]
    public void InvalidPlotAxisIsRejectedBeforeRendering(double minimum, double maximum)
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new PlotData("x", "y", Array.Empty<PlotSeriesData>(), minimum, maximum, 0, 1));
    }

    [TestMethod]
    public void NonFinitePlotPointIsRejectedBeforeRendering()
    {
        var series = new[] { new PlotSeriesData("Test", "#000000", new[] { new PlotPointData(0, double.NaN) }) };
        Assert.ThrowsException<ArgumentException>(() => new PlotData("x", "y", series, 0, 1, 0, 1));
    }

    [TestMethod]
    public void OverflowingFieldMapDimensionsAreRejectedBeforeRendering()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new FieldMapData(65536, 65536, Array.Empty<double>(), 0, 1, 0, 1, 0, 1, 0, 1));
    }

    [TestMethod]
    public void NonFiniteFieldMapIsRejectedInsteadOfDisplayedAsZero()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new FieldMapData(2, 2, new[] { 0.0, 1.0, double.NaN, 0.0 }, 0, 1, 0, 1, 0, 1, 0, 1));
    }
}
