using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class SeriesGridTests
{
    [DataTestMethod]
    [DataRow(10.0, 19.999999995, 10.0, 1)]
    [DataRow(10.0, 64.2999999999, 0.3, 181)]
    [DataRow(0.1, 0.3, 0.1, 3)]
    [DataRow(10.0, 64.0, 0.3, 181)]
    [DataRow(10.0, 64.3, 0.3, 0)]
    [DataRow(45.0, 45.0, 2.0, 1)]
    public void AngleCountsRespectTheRequestedRange(double start, double end, double step, int expected)
    {
        var parameters = CreateParameters();
        parameters.SeriesAngleStartDegrees = start;
        parameters.SeriesAngleEndDegrees = end;
        parameters.SeriesAngleStepDegrees = step;
        Assert.AreEqual(expected, DiffractionCalculationService.GetAnglePointCount(parameters));
    }

    [TestMethod]
    public void AStepThatCannotChangeTheRepresentedAngleIsRejected()
    {
        var parameters = CreateParameters();
        parameters.SeriesAngleStartDegrees = 45;
        parameters.SeriesAngleEndDegrees = Math.BitIncrement(45);
        parameters.SeriesAngleStepDegrees = 1e-16;
        Assert.AreEqual(0, DiffractionCalculationService.GetAnglePointCount(parameters));
        Assert.IsNotNull(DiffractionCalculationService.GetValidationError(parameters, true));
    }

    [DataTestMethod]
    [DataRow(10.0, 19.999999995, 10.0, 1)]
    [DataRow(0.1, 0.3, 0.1, 3)]
    public void GeneratedAnglesAreDistinctAndNeverExceedTheEnd(double start, double end, double step, int expected)
    {
        var parameters = CreateParameters();
        parameters.SeriesAngleStartDegrees = start;
        parameters.SeriesAngleEndDegrees = end;
        parameters.SeriesAngleStepDegrees = step;
        var output = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);
        var angles = output.AngleEnergyPlot.Series[0].Points.Select(point => point.X).ToArray();
        Assert.AreEqual(expected, angles.Length);
        Assert.AreEqual(angles.Length, angles.Distinct().Count());
        Assert.IsTrue(angles.All(angle => angle >= start && angle <= end));
    }

    [TestMethod]
    public void CloseSkinDepthsUseTheirOwnSolutionsIncludingTheZeroSkinCase()
    {
        var parameters = CreateParameters();
        parameters.HarmonicCount = 4;
        parameters.SkinDepthMicrometers = 5e-13;
        parameters.SeriesSkinDepthEnd = 1e-12;
        parameters.SeriesPointCount = 3;
        var series = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);
        parameters.SkinDepthMicrometers = parameters.SeriesSkinDepthEnd;
        var endpoint = DiffractionCalculationService.Calculate(parameters, false, CancellationToken.None, false);
        var absorbed = series.SkinEnergyPlot.Series[2].Points;
        Assert.AreEqual(0.0, absorbed[0].Y, 0.0, "Zero skin must not reuse the nearby nonzero skin solution.");
        Assert.IsTrue(absorbed[1].Y > 0);
        Assert.IsTrue(absorbed[2].Y > absorbed[1].Y);
        Assert.AreEqual(endpoint.Energy.Absorbed, absorbed[2].Y, Math.Abs(endpoint.Energy.Absorbed) * 1e-10);
    }

    [TestMethod]
    public void SeriesBuildsSeparateAngleStudiesForIdealAndSkinConductors()
    {
        var parameters = CreateParameters();
        parameters.HarmonicCount = 4;
        parameters.SkinDepthMicrometers = 0.001;
        parameters.SeriesSkinDepthStart = 0;
        parameters.SeriesSkinDepthEnd = 0.001;
        parameters.SeriesPointCount = 2;
        parameters.SeriesAngleStartDegrees = 30;
        parameters.SeriesAngleEndDegrees = 40;
        parameters.SeriesAngleStepDegrees = 5;

        var output = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);

        Assert.AreEqual(2, output.SkinDepthStudyRows.Count);
        Assert.AreEqual(3, output.AngleStudyIdealRows.Count);
        Assert.AreEqual(3, output.AngleStudySkinRows.Count);

        // Идеальный проводник не поглощает энергию ни при одном угле.
        Assert.IsTrue(output.AngleStudyIdealRows.All(row => row.AbsorbedPercent < 1e-6),
            "Идеальный проводник не должен поглощать энергию.");

        // Скин-слой добавляет поглощение и меняет отражение при каждом угле.
        Assert.IsTrue(output.AngleStudySkinRows.All(row => row.AbsorbedPercent > 0),
            "Скин-слой должен давать ненулевое поглощение.");
        Assert.IsTrue(output.AngleStudySkinRows
                .Zip(output.AngleStudyIdealRows)
                .All(pair => Math.Abs(pair.First.ReflectedPercent - pair.Second.ReflectedPercent) > 1e-9),
            "Отражение со скин-слоем должно отличаться от идеального проводника.");

        // Таблица по δ совпадает по точкам с графиком энергии.
        CollectionAssert.AreEqual(
            output.SkinEnergyPlot.Series[0].Points.Select(point => point.X).ToArray(),
            output.SkinDepthStudyRows.Select(row => row.ArgumentValue).ToArray());
    }

    [TestMethod]
    public void SeriesCanRunBySkinDepthOrByAngleIndependently()
    {
        var parameters = CreateParameters();
        parameters.HarmonicCount = 4;
        parameters.SkinDepthMicrometers = 0.001;
        parameters.SeriesSkinDepthStart = 0;
        parameters.SeriesSkinDepthEnd = 0.001;
        parameters.SeriesPointCount = 2;
        parameters.SeriesAngleStartDegrees = 30;
        parameters.SeriesAngleEndDegrees = 40;
        parameters.SeriesAngleStepDegrees = 5;

        parameters.SeriesAngleEnabled = false;
        var skinOnly = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);
        Assert.AreEqual(2, skinOnly.SkinDepthStudyRows.Count);
        Assert.AreEqual(0, skinOnly.AngleStudyIdealRows.Count);
        Assert.AreEqual(0, skinOnly.AngleStudySkinRows.Count);
        Assert.IsFalse(skinOnly.AngleEnergyPlot.HasData);

        parameters.SeriesAngleEnabled = true;
        parameters.SeriesSkinDepthEnabled = false;
        var angleOnly = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);
        Assert.AreEqual(0, angleOnly.SkinDepthStudyRows.Count);
        Assert.AreEqual(3, angleOnly.AngleStudyIdealRows.Count);
        Assert.AreEqual(3, angleOnly.AngleStudySkinRows.Count);
        Assert.IsFalse(angleOnly.SkinEnergyPlot.HasData);

        parameters.SeriesSkinDepthEnabled = false;
        parameters.SeriesAngleEnabled = false;
        StringAssert.Contains(
            DiffractionCalculationService.GetValidationError(parameters, true),
            "хотя бы одну серию");
    }

    private static CalculationParameters CreateParameters() => new()
    {
        HarmonicCount = 2,
        SkinDepthMicrometers = 0,
        SeriesSkinDepthStart = 0,
        SeriesSkinDepthEnd = 0,
        SeriesPointCount = 2,
        SeriesAngleStartDegrees = 45,
        SeriesAngleEndDegrees = 45
    };
}
