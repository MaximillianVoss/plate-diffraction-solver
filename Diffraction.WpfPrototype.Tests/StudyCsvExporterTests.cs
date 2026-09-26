using System.Globalization;
using System.IO;
using System.Text;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualBasic.FileIO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class StudyCsvExporterTests
{
    private static readonly string[] EnergyHeaders =
    {
        "R_scat_pct_I_plate", "T_scat_pct_I_plate", "A_J_pct_I_plate", "sum_pct_I_plate",
        "extinction_pct_I_plate", "optical_balance_error_pct_extinction", "cross_section_scale_um",
        "has_global_balance"
    };

    [TestMethod]
    public void SkinDepthStudyCsvContainsHeaderAndAllRows()
    {
        var rows = new[]
        {
            CreateRow(0, 0.28, 0.70, 0.02),
            CreateRow(0.001, 0.26, 0.70, 0.15)
        };

        string csv = StudyCsvExporter.BuildSkinDepthStudyCsv(rows, "Параметры: тест");

        List<string[]> records = ReadCsv(csv);
        Assert.AreEqual(5, records.Count);
        CollectionAssert.AreEqual(new[] { "Исследование изменения толщины скин-слоя" }, records[0]);
        CollectionAssert.AreEqual(new[] { "Параметры: тест" }, records[1]);
        AssertHeader("skin_depth_um", records[2]);
        AssertEnergyRow(rows[0], records[3]);
        AssertEnergyRow(rows[1], records[4]);
        Assert.IsTrue(csv.Contains("\"R_scat_pct_I_plate\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AngleStudyCsvKeepsIdealAndSkinTablesSeparated()
    {
        var idealRows = new[] { CreateRow(10, 2.6201, 2.6201, 0), CreateRow(90, 1.0033, 1.0033, 0) };
        var skinRows = new[] { CreateRow(10, 2.3046, 2.3046, 0.4223), CreateRow(90, 0.8599, 0.8599, 0.1297) };

        string csv = StudyCsvExporter.BuildAngleStudyCsv(idealRows, skinRows, 0.001, "Параметры: тест");

        List<string[]> records = ReadCsv(csv);
        Assert.AreEqual(10, records.Count);
        CollectionAssert.AreEqual(new[] { "Исследование изменения угла падения" }, records[0]);
        CollectionAssert.AreEqual(new[] { "Таблица 1. Идеальный проводник (без скин-слоя)" }, records[2]);
        CollectionAssert.AreEqual(new[] { "Таблица 2. Со скин-слоем (δ = 0.001 мкм)" }, records[6]);
        AssertHeader("angle_deg", records[3]);
        AssertHeader("angle_deg", records[7]);
        AssertEnergyRow(idealRows[0], records[4]);
        AssertEnergyRow(idealRows[1], records[5]);
        AssertEnergyRow(skinRows[0], records[8]);
        AssertEnergyRow(skinRows[1], records[9]);
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

        List<string[]> records = ReadCsv(csv);
        Assert.AreEqual(4, records.Count);
        CollectionAssert.AreEqual(new[] { "Энергетика одиночного расчёта" }, records[0]);
        AssertHeader("skin_depth_um", records[2]);
        AssertEnergyRow(row, records[3]);
    }

    [TestMethod]
    public void EnergyStudyRowComparesSumWithExtinctionNotOneHundredPercent()
    {
        var row = CreateRow(45, 1.3, 1.3, 0, extinction: 2.6, crossSectionScale: 0.17364817766693033);
        Assert.AreEqual(260.0, row.SumPercent, 1e-12);
        Assert.AreEqual(260.0, row.ExtinctionPercent, 1e-12);
        Assert.AreEqual(0.0, row.ImbalancePercent, 1e-12);
        Assert.IsTrue(row.HasGlobalBalance);
        Assert.AreEqual(0.17364817766693033, row.CrossSectionScale);
    }

    [TestMethod]
    public void EnergyStudyRowUsesOpticalResidualInsteadOfLocalResidual()
    {
        var snapshot = new EnergySnapshot(0.01, 0.4, 0.4, 0.2, 0, 0, 0.2,
            localBalanceErrorPercent: 0.123, extinction: 1.25, crossSectionScale: 0.5);
        var row = new EnergyStudyRow(45, snapshot);
        Assert.AreEqual(100.0, row.SumPercent, 1e-12);
        Assert.AreEqual(20.0, row.ImbalancePercent, 1e-12);
        Assert.AreEqual(snapshot.OpticalBalanceErrorPercent, row.ImbalancePercent);
    }

    [TestMethod]
    public void MissingGlobalBalanceIsUnavailableInsteadOfZero()
    {
        var row = CreateRow(0.01, 0.4, 0.4, 0.2);
        Assert.IsFalse(row.HasGlobalBalance);
        Assert.IsTrue(double.IsNaN(row.ImbalancePercent));
        Assert.AreEqual("н/д", row.ImbalanceDisplay);
        Assert.AreEqual("н/д", row.ExtinctionDisplay);
        Assert.AreEqual("н/д", row.CrossSectionScaleDisplay);

        string[] fields = ReadCsv(StudyCsvExporter.BuildSingleRunCsv(row, "тест"))[3];
        Assert.AreEqual(9, fields.Length);
        Assert.AreEqual(string.Empty, fields[5]);
        Assert.AreEqual(string.Empty, fields[6]);
        Assert.AreEqual(string.Empty, fields[7]);
        Assert.AreEqual("false", fields[8]);
    }

    [TestMethod]
    public void CsvPreservesDoublePrecisionIncludingTinyValues()
    {
        var row = CreateRow(0.000000000123456789, 0.12345678901234567, 0.12345678901234567,
            1.2345678901234567e-14, extinction: 0.24691357802471, crossSectionScale: 0.7071067811865476);
        string csv = StudyCsvExporter.BuildSingleRunCsv(row, "round-trip");
        AssertEnergyRow(row, ReadCsv(csv)[3]);
        Assert.AreNotEqual(0.0, ParseNumber(ReadCsv(csv)[3][3]));
    }

    [TestMethod]
    public void CsvEscapesQuotesCommasAndMultilineMetadataWithoutChangingText()
    {
        const string summary = "Параметры: \"опыт, 1\"\r\nλ=1 мкм";
        var row = CreateRow(0.01, 1, 1, 0);
        string csv = StudyCsvExporter.BuildSingleRunCsv(row, summary);
        byte[] utf8 = new UTF8Encoding(false, true).GetBytes(csv);
        List<string[]> records = ReadCsv(new UTF8Encoding(false, true).GetString(utf8));
        Assert.AreEqual(4, records.Count);
        CollectionAssert.AreEqual(new[] { summary }, records[1]);
        AssertEnergyRow(row, records[3]);
    }

    [TestMethod]
    public void CsvNumbersRemainInvariantUnderRussianCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var row = CreateRow(0.00123, 0.45678, 0.45678, 0.00321, extinction: 0.91677);
            string csv = StudyCsvExporter.BuildSkinDepthStudyCsv(new[] { row }, "тест");
            AssertEnergyRow(row, ReadCsv(csv)[3]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static void AssertHeader(string argumentHeader, string[] header)
    {
        CollectionAssert.AreEqual(new[] { argumentHeader }.Concat(EnergyHeaders).ToArray(), header);
        Assert.AreEqual(header.Length, header.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertEnergyRow(EnergyStudyRow row, string[] fields)
    {
        Assert.AreEqual(9, fields.Length);
        Assert.AreEqual(row.ArgumentValue, ParseNumber(fields[0]));
        Assert.AreEqual(row.ReflectedPercent, ParseNumber(fields[1]));
        Assert.AreEqual(row.TransmittedPercent, ParseNumber(fields[2]));
        Assert.AreEqual(row.AbsorbedPercent, ParseNumber(fields[3]));
        Assert.AreEqual(row.SumPercent, ParseNumber(fields[4]));
        if (row.HasGlobalBalance)
        {
            Assert.AreEqual(row.ExtinctionPercent, ParseNumber(fields[5]));
            Assert.AreEqual(row.ImbalancePercent, ParseNumber(fields[6]));
        }
        else
        {
            Assert.AreEqual(string.Empty, fields[5]);
            Assert.AreEqual(string.Empty, fields[6]);
        }
        if (double.IsFinite(row.CrossSectionScale))
            Assert.AreEqual(row.CrossSectionScale, ParseNumber(fields[7]));
        else
            Assert.AreEqual(string.Empty, fields[7]);
        Assert.AreEqual(row.HasGlobalBalance ? "true" : "false", fields[8]);
    }

    private static double ParseNumber(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    private static List<string[]> ReadCsv(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv))
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        var records = new List<string[]>();
        while (!parser.EndOfData)
            records.Add(parser.ReadFields()!);
        return records;
    }

    private static EnergyStudyRow CreateRow(double argument, double reflected, double transmitted, double absorbed,
        double extinction = double.NaN, double crossSectionScale = double.NaN) =>
        new(argument, new EnergySnapshot(
            argument,
            reflected,
            transmitted,
            absorbed,
            sheetAbove: 0,
            sheetBelow: 0,
            fluxAbsorbed: 0,
            localBalanceErrorPercent: 0,
            extinction: extinction,
            crossSectionScale: crossSectionScale));
}
