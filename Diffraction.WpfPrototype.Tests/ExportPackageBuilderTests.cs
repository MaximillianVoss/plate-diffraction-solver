using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
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
}
