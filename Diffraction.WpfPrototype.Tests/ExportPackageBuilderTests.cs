using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Microsoft.VisualBasic.FileIO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class ExportPackageBuilderTests
{
    [TestMethod]
    public void PlotCsvContainsEverySeriesAndPoint()
    {
        PlotData plot = CreatePlot();
        string csv = ExportPackageBuilder.BuildPlotCsv("slice", plot);

        string[] lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("\"slice\"", lines[0]);
        StringAssert.Contains(lines[2], "\"first x\"");
        StringAssert.Contains(lines[2], "\"second y\"");
        Assert.AreEqual(3 + 3, lines.Length);
        StringAssert.Contains(lines[3], "0");
        StringAssert.Contains(lines[5], "2");
    }

    [TestMethod]
    public void PlotCsvPreservesTinyValuesRoundTripAndQuotedLabels()
    {
        var points = new[]
        {
            new PlotPointData(1e-9, 1.0001264794213719e-8),
            new PlotPointData(Math.BitIncrement(1.0), 0.12345678901234567)
        };
        const string name = "C_abs, \"finite\"";
        var plot = new PlotData("skin_depth_um", "cross_section_um",
            new[] { new PlotSeriesData(name, "#EA580C", points) }, 0, 2, 0, 1);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            const string title = "skin, \"finite\"\nseries";
            List<string[]> records = ReadCsv(ExportPackageBuilder.BuildPlotCsv(title, plot));
            Assert.AreEqual(title, records[0][0]);
            Assert.AreEqual(name + " y", records[2][1]);
            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(2, records[i + 3].Length);
                Assert.AreEqual(points[i].X, double.Parse(records[i + 3][0], CultureInfo.InvariantCulture));
                Assert.AreEqual(points[i].Y, double.Parse(records[i + 3][1], CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [DataTestMethod]
    [DataRow(0.0, "T_n")]
    [DataRow(1e-12, "P_n")]
    public void ExportsDescribeCurrentBasisAndBothRulesForMixedSkinSeries(double skinDepth, string basis)
    {
        var parameters = new CalculationParameters { SkinDepthMicrometers = skinDepth, SeriesSkinDepthStart = 0 };
        using JsonDocument json = JsonDocument.Parse(ExportPackageBuilder.BuildParametersJson(parameters, "CPU", DateTime.UnixEpoch));
        JsonElement solver = json.RootElement.GetProperty("solver");
        StringAssert.Contains(solver.GetProperty("currentCoefficientBasis").GetString()!, basis);
        JsonElement rules = solver.GetProperty("coefficientBasisBySkinDepth");
        Assert.AreEqual("T_n(tau)/sqrt(1-tau^2)", rules.GetProperty("zero").GetString());
        Assert.AreEqual("P_n(tau)", rules.GetProperty("positive").GetString());
        var energy = new EnergySnapshot(skinDepth, 0.4, 0.4, 0.2, 0, 0, 0.2, 0, extinction: 1, crossSectionScale: 1);
        string report = ExportPackageBuilder.BuildReportText(energy, Array.Empty<FluxRow>(),
            Array.Empty<DiagnosticRow>(), new[] { "skin 0..0.01" }, "CPU", DateTime.UnixEpoch);
        StringAssert.Contains(report, "Базис выбранного расчёта: " + energy.CoefficientBasisDisplay);
        StringAssert.Contains(report, "T_n");
        StringAssert.Contains(report, "P_n");
    }

    [TestMethod]
    public void SingleAngleStudyHasMarkersInUiDataAndSvg()
    {
        var parameters = new CalculationParameters
        {
            HarmonicCount = 6,
            SeriesSkinDepthEnabled = false,
            SeriesAngleEnabled = true,
            SeriesAngleStartDegrees = 30,
            SeriesAngleEndDegrees = 30
        };
        CalculationOutput output = DiffractionCalculationService.Calculate(parameters, true, CancellationToken.None, false);
        Assert.AreEqual(4, output.AngleEnergyPlot.Series.Count);
        foreach (PlotSeriesData series in output.AngleEnergyPlot.Series)
        {
            Assert.AreEqual(1, series.Points.Count);
            Assert.IsTrue(series.ShowMarkers);
        }
        XDocument svg = XDocument.Parse(ExportPackageBuilder.BuildPlotSvg(output.AngleEnergyPlot));
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.AreEqual(4, svg.Descendants(ns + "circle").Count());
    }

    [TestMethod]
    public void StudyAndPlotCsvKeepScatteringComponentsInTheCorrectColumns()
    {
        var energy = new EnergySnapshot(0.01, 0.26, 0.70, 0.15, 0, 0, 0.15, 0,
            extinction: 1.11, crossSectionScale: 0.5);
        var row = new EnergyStudyRow(45, energy);
        var plot = new PlotData("angle_deg", "fraction of I_plate", new[]
        {
            new PlotSeriesData("R_scat", "#2563EB", new[] { new PlotPointData(45, energy.ReflectedScattered) }),
            new PlotSeriesData("T_scat", "#16A34A", new[] { new PlotPointData(45, energy.ForwardScattered) }),
            new PlotSeriesData("A_J", "#EA580C", new[] { new PlotPointData(45, energy.Absorbed) })
        }, 44, 46, 0, 1);

        List<string[]> studyRecords = ReadCsv(StudyCsvExporter.BuildAngleStudyCsv(
            new[] { row }, new[] { row }, 0.01, "column mapping"));
        List<string[]> plotRecords = ReadCsv(ExportPackageBuilder.BuildPlotCsv("angle", plot));
        string[] studyHeader = studyRecords[3];
        string[] studyValues = studyRecords[4];
        string[] plotHeader = plotRecords[2];
        string[] plotValues = plotRecords[3];
        Assert.AreEqual(studyHeader.Length, studyValues.Length);
        Assert.AreEqual(plotHeader.Length, plotValues.Length);

        foreach (string component in new[] { "R_scat", "T_scat", "A_J" })
        {
            int studyIndex = Array.IndexOf(studyHeader, component + "_pct_I_plate");
            int plotIndex = Array.IndexOf(plotHeader, component + " y");
            Assert.IsTrue(studyIndex >= 0 && plotIndex >= 0);
            double percent = double.Parse(studyValues[studyIndex], CultureInfo.InvariantCulture);
            double fraction = double.Parse(plotValues[plotIndex], CultureInfo.InvariantCulture);
            Assert.AreEqual(fraction * 100.0, percent, 1e-12, component);
        }
        Assert.AreEqual(row.ExtinctionPercent,
            double.Parse(studyValues[Array.IndexOf(studyHeader, "extinction_pct_I_plate")], CultureInfo.InvariantCulture));
        Assert.AreEqual(row.ImbalancePercent,
            double.Parse(studyValues[Array.IndexOf(studyHeader, "optical_balance_error_pct_extinction")], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ParametersJsonContainsSeriesFlagsAndGeometry()
    {
        var parameters = new CalculationParameters
        {
            HarmonicCount = 10,
            SkinDepthMicrometers = 0.001,
            SeriesSkinDepthEnabled = false,
            SeriesAngleEnabled = true
        };

        string json = ExportPackageBuilder.BuildParametersJson(parameters, "CPU (C#)", new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));

        StringAssert.Contains(json, "\"harmonicCount\": 10");
        StringAssert.Contains(json, "\"skinDepthMicrometers\": 0.001");
        StringAssert.Contains(json, "\"skinDepthEnabled\": false");
        StringAssert.Contains(json, "\"angleEnabled\": true");
        StringAssert.Contains(json, "\"plateStart\": -1.5");
        StringAssert.Contains(json, "\"backend\": \"CPU (C#)\"");
    }

    [TestMethod]
    public void ReportContainsEnergyFluxAndDiagnosticsSections()
    {
        var energy = new EnergySnapshot(0.01, 0.4, 0.55, 0.05, 0.4, 0.6, 0.05, 0.1);
        var fluxRows = new[]
        {
            new FluxRow
            {
                Category = "Баланс",
                Metric = "A_flux / A_J",
                Top = 0.05,
                Bottom = 0.05,
                DifferencePercent = 0.1,
                TolerancePercent = 2.0,
                Status = "В допуске"
            }
        };
        var diagnostics = new[]
        {
            new DiagnosticRow
            {
                Group = "Энергетика",
                Check = "Локальная невязка ЗСЭ",
                Value = "0.100000%",
                Tolerance = "≤ 2,00%",
                Status = "В допуске"
            }
        };

        string report = ExportPackageBuilder.BuildReportText(
            energy, fluxRows, diagnostics, new[] { "Серия δ: 2 точек" }, "CPU (C#)", DateTime.UtcNow);

        StringAssert.Contains(report, "ЭНЕРГЕТИКА");
        StringAssert.Contains(report, "R_scat обратно");
        StringAssert.Contains(report, "ПОТОКИ");
        StringAssert.Contains(report, "A_flux / A_J");
        StringAssert.Contains(report, "ДИАГНОСТИКА");
        StringAssert.Contains(report, "Локальная невязка ЗСЭ");
        StringAssert.Contains(report, "Серия δ: 2 точек");
    }

    [TestMethod]
    public void PlotSvgContainsPolylinesWithSeriesColors()
    {
        PlotData plot = CreatePlot();
        string svg = ExportPackageBuilder.BuildPlotSvg(plot, 480, 270);

        StringAssert.StartsWith(svg, "<svg");
        StringAssert.Contains(svg, "<polyline");
        Assert.AreEqual(2, svg.Split(new[] { "<polyline" }, StringSplitOptions.None).Length - 1);
        StringAssert.Contains(svg, "#2563EB");
        StringAssert.Contains(svg, "stroke-dasharray");
        StringAssert.Contains(svg, "</svg>");
    }

    [TestMethod]
    public void PlotSvgRejectsEmptyPlot()
    {
        PlotData empty = PlotData.Empty("x", "y");
        Assert.ThrowsException<ArgumentException>(() => ExportPackageBuilder.BuildPlotSvg(empty));
    }

    [TestMethod]
    public void SinglePointDimensionalEnergySvgContainsVisibleMarkers()
    {
        var series = new[]
        {
            new PlotSeriesData("C_back", "#2563EB", new[] { new PlotPointData(0.01, 0.2) }, showMarkers: true),
            new PlotSeriesData("C_forward", "#16A34A", new[] { new PlotPointData(0.01, 0.2) }, isDashed: true, showMarkers: true),
            new PlotSeriesData("C_abs", "#EA580C", new[] { new PlotPointData(0.01, 0.05) }, showMarkers: true),
            new PlotSeriesData("C_ext", "#374151", new[] { new PlotPointData(0.01, 0.45) }, showMarkers: true)
        };
        var plot = new PlotData("Толщина δ", "Сечение, мкм", series, 0, 0.02, 0, 0.6);
        XDocument svg = XDocument.Parse(ExportPackageBuilder.BuildPlotSvg(plot));
        XNamespace ns = "http://www.w3.org/2000/svg";
        XElement[] markers = svg.Descendants(ns + "circle").ToArray();

        Assert.AreEqual(4, markers.Length);
        Assert.IsTrue(svg.Descendants(ns + "text").Any(text => text.Value == "Сечение, мкм"));
        for (int i = 0; i < series.Length; i++)
        {
            Assert.AreEqual(series[i].Name, markers[i].Element(ns + "title")!.Value);
            Assert.AreEqual(series[i].Color, (string?)markers[i].Attribute("fill"));
            Assert.AreEqual(505.0, SvgNumber(markers[i], "cx"), 1e-6);
            Assert.AreEqual(480.0 - series[i].Points[0].Y / 0.6 * 460.0, SvgNumber(markers[i], "cy"), 1e-6);
            Assert.AreEqual(3.5, SvgNumber(markers[i], "r"), 1e-12);
            Assert.AreEqual("white", (string?)markers[i].Attribute("stroke"));
        }
    }

    [DataTestMethod]
    [DataRow(true, 3)]
    [DataRow(false, 0)]
    public void PlotSvgHonorsMarkerVisibilityForMultiPointSeries(bool showMarkers, int markerCount)
    {
        var series = new PlotSeriesData("marked & <phase>", "#2563EB",
            new[] { new PlotPointData(0, 0.25), new PlotPointData(1, 0.5), new PlotPointData(2, 0.75) },
            showMarkers: showMarkers);
        var plot = new PlotData("x", "y", new[] { series }, 0, 2, 0, 1);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            XDocument svg = XDocument.Parse(ExportPackageBuilder.BuildPlotSvg(plot, 480, 270));
            XNamespace ns = "http://www.w3.org/2000/svg";
            XElement[] markers = svg.Descendants(ns + "circle").ToArray();
            Assert.AreEqual(markerCount, markers.Length);
            for (int i = 0; i < markers.Length; i++)
            {
                Assert.AreEqual(series.Name, markers[i].Element(ns + "title")!.Value);
                Assert.AreEqual(70.0 + i * 195.0, SvgNumber(markers[i], "cx"), 1e-6);
                Assert.AreEqual(210.0 - series.Points[i].Y * 190.0, SvgNumber(markers[i], "cy"), 1e-6);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static double SvgNumber(XElement element, string attribute) =>
        double.Parse(element.Attribute(attribute)!.Value, CultureInfo.InvariantCulture);

    private static PlotData CreatePlot() =>
        new(
            "x",
            "Re u",
            new[]
            {
                new PlotSeriesData("first", "#2563EB",
                    new[] { new PlotPointData(0, 1), new PlotPointData(1, 2), new PlotPointData(2, 3) }),
                new PlotSeriesData("second", "#16A34A",
                    new[] { new PlotPointData(0, 3), new PlotPointData(1, 2), new PlotPointData(2, 1) },
                    isDashed: true)
            },
            -0.1, 2.1, 0.9, 3.1);

    private static List<string[]> ReadCsv(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv));
        parser.SetDelimiters(",");
        var records = new List<string[]>();
        while (!parser.EndOfData)
            records.Add(parser.ReadFields()!);
        return records;
    }
}
