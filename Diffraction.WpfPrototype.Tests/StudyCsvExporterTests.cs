using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class StudyCsvExporterTests
{
    [TestMethod]
    public void SkinDepthStudyCsvContainsHeaderAndAllRows()
    {
        var rows = new[]
        {
            CreateRow(0, 0.28, 0.70, 0.02),
            CreateRow(0.001, 0.26, 0.70, 0.15)
        };

        string csv = StudyCsvExporter.BuildSkinDepthStudyCsv(rows, "Параметры: тест");

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("Исследование изменения толщины скин-слоя", lines[0]);
        Assert.AreEqual("\"Параметры: тест\"", lines[1]);
        Assert.AreEqual("Толщина скин-слоя δ, мкм,E_ref, %,E_tr, %,E_abs, %,Сумма, %,Дисбаланс, %", lines[2]);
        Assert.AreEqual("0,28.00,70.00,2.00,100.00,0.00", lines[3]);
        Assert.AreEqual("0.001,26.00,70.00,15.00,111.00,11.00", lines[4]);
    }

    [TestMethod]
    public void AngleStudyCsvKeepsIdealAndSkinTablesSeparated()
    {
        var idealRows = new[] { CreateRow(0, 0.0208, 1.0606, 0), CreateRow(15, 0.1337, 0.8693, 0) };
        var skinRows = new[] { CreateRow(0, 0.0208, 1.0419, 0.0106), CreateRow(15, 0.1269, 0.8522, 0.0043) };

        string csv = StudyCsvExporter.BuildAngleStudyCsv(idealRows, skinRows, 0.001, "Параметры: тест");

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        CollectionAssert.Contains(lines, "Исследование изменения угла падения");
        CollectionAssert.Contains(lines, "Таблица 1. Идеальный проводник (без скин-слоя)");
        CollectionAssert.Contains(lines, "Таблица 2. Со скин-слоем (δ = 0.001 мкм)");
        CollectionAssert.Contains(lines, "0,2.08,106.06,0.00,108.14,8.14");
        CollectionAssert.Contains(lines, "15,12.69,85.22,0.43,98.34,1.66");
    }

    [TestMethod]
    public void AngleStudyCsvRejectsMismatchedTables()
    {
        var idealRows = new[] { CreateRow(0, 0.1, 0.9, 0) };
        var skinRows = Array.Empty<EnergyStudyRow>();
        Assert.ThrowsException<ArgumentException>(() =>
            StudyCsvExporter.BuildAngleStudyCsv(idealRows, skinRows, 0.001, "тест"));
    }

    [TestMethod]
    public void SingleRunCsvContainsOneEnergyRow()
    {
        var row = CreateRow(0.01, 0.3, 0.65, 0.05);
        string csv = StudyCsvExporter.BuildSingleRunCsv(row, "Параметры: тест");

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("Энергетика одиночного расчёта", lines[0]);
        Assert.AreEqual("0.01,30.00,65.00,5.00,100.00,0.00", lines[3]);
    }

    [TestMethod]
    public void EnergyStudyRowComputesSumAndImbalance()
    {
        var row = CreateRow(45, 0.2828, 0.7046, 0);
        Assert.AreEqual(98.74, row.SumPercent, 1e-9);
        Assert.AreEqual(1.26, row.ImbalancePercent, 1e-9);
    }

    private static EnergyStudyRow CreateRow(double argument, double reflected, double transmitted, double absorbed) =>
        new(argument, new EnergySnapshot(
            argument,
            reflected,
            transmitted,
            absorbed,
            sheetAbove: 0,
            sheetBelow: 0,
            fluxAbsorbed: 0,
            localBalanceErrorPercent: 0));
}
