using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class OpticalBalanceTests
{
    [DataTestMethod]
    [DataRow(10, "Проверить")]
    [DataRow(55, "В допуске")]
    public void EdgeResidualIsCheckedSeparatelyFromTheIntegralBalance(int order, string expectedStatus)
    {
        var parameters = new CalculationParameters
        {
            HarmonicCount = order,
            IncidenceAngleDegrees = 10,
            SkinDepthMicrometers = 0.01
        };
        var output = DiffractionCalculationService.Calculate(parameters, false, CancellationToken.None, false);
        Assert.IsTrue(output.Energy.IsWithinTolerance);
        var edges = output.Diagnostics.Where(row => row.Check.Contains("краёв")).ToArray();
        Assert.AreEqual(2, edges.Length);
        Assert.IsTrue(edges.All(row => row.Status == expectedStatus));
    }

    [TestMethod]
    public void LocalBalanceAndSymmetryCannotHideAGlobalEnergyError()
    {
        var snapshot = new EnergySnapshot(0, 2.5, 2.5, 0, 2.5, 2.5, 0, 0,
            extinction: 1, crossSectionScale: 1);
        Assert.AreEqual(400.0, snapshot.OpticalBalanceErrorPercent, 1e-12);
        Assert.IsTrue(snapshot.IsFarFieldWithinTolerance);
        Assert.IsFalse(snapshot.IsWithinTolerance);
        Assert.AreEqual("Проверить", snapshot.BalanceStatus);
    }

    [TestMethod]
    public void MissingIndependentExtinctionIsNotAGreenResult()
    {
        var snapshot = new EnergySnapshot(0, 0.5, 0.5, 0, 0.5, 0.5, 0, 0);
        Assert.IsFalse(snapshot.HasGlobalBalance);
        Assert.IsFalse(snapshot.IsWithinTolerance);
        Assert.AreEqual("н/д", snapshot.OpticalBalanceErrorDisplay);
    }

    [TestMethod]
    public void GrazingCalculationExposesBothCrossSectionsAndIndependentBalance()
    {
        var parameters = new CalculationParameters
        {
            HarmonicCount = 30,
            IncidenceAngleDegrees = 10,
            SkinDepthMicrometers = 0.01
        };
        var output = DiffractionCalculationService.Calculate(parameters, false, CancellationToken.None, false);
        var energy = output.Energy;
        Assert.IsTrue(energy.ReflectedScattered > 2, "Do not clip the geometric-projection ratio.");
        Assert.IsTrue(energy.OpticalBalanceErrorPercent < 0.5);
        Assert.IsTrue(energy.IsWithinTolerance);
        Assert.AreEqual(Math.Sin(Math.PI / 18), energy.CrossSectionScale, 1e-12);
        Assert.AreEqual("Сечение, мкм", output.CurrentEnergyPlot.YAxisTitle);
        Assert.AreEqual(energy.ReflectedCrossSection, output.CurrentEnergyPlot.Series[0].Points[0].Y, 1e-12);
        Assert.IsTrue(output.Diagnostics.Any(row => row.Check.Contains("Оптическая теорема")));
        Assert.IsTrue(output.DiagnosticsSummary.Contains("независимый интеграл"));
    }
}
