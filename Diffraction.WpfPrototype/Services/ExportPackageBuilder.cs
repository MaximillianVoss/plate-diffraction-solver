using System.Globalization;
using System.Text;
using System.Text.Json;
using Diffraction.WpfPrototype.Models;

namespace Diffraction.WpfPrototype.Services;

/// <summary>
/// Формирует текстовое содержимое пакета экспорта: CSV точек графиков,
/// JSON параметров, отчёт по ЗСЭ и потокам, SVG графики.
/// </summary>
public static class ExportPackageBuilder
{
    private static CultureInfo InvariantCulture { get; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// CSV с точками графика: пара колонок (x, y) на каждую серию.
    /// </summary>
    public static string BuildPlotCsv(string title, PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        var builder = new StringBuilder();
        builder.AppendLine(CsvCell(title));
        builder.AppendLine(CsvCell($"{plot.XAxisTitle} | {plot.YAxisTitle}"));
        bool firstColumn = true;
        foreach (PlotSeriesData series in plot.Series)
        {
            if (!firstColumn)
                builder.Append(',');
            firstColumn = false;
            builder.Append(CsvCell(series.Name + " x"));
            builder.Append(',');
            builder.Append(CsvCell(series.Name + " y"));
        }
        builder.AppendLine();
        int rowCount = plot.Series.Max(item => item.Points.Count);
        for (int row = 0; row < rowCount; row++)
        {
            bool first = true;
            foreach (PlotSeriesData series in plot.Series)
            {
                if (!first)
                    builder.Append(',');
                first = false;
                if (row < series.Points.Count)
                {
                    builder.Append(series.Points[row].X.ToString("R", InvariantCulture));
                    builder.Append(',');
                    builder.Append(series.Points[row].Y.ToString("R", InvariantCulture));
                }
                else
                {
                    builder.Append(',');
                }
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    /// <summary>
    /// JSON с параметрами расчёта и сведениями о решателе.
    /// </summary>
    public static string BuildParametersJson(
        CalculationParameters parameters,
        string backendName,
        DateTime exportedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var payload = new
        {
            exportedAtUtc = exportedAtUtc.ToString("O", InvariantCulture),
            solver = new
            {
                backend = backendName,
                methods = new[] { "collocation", "galerkin" },
                currentCoefficientBasis = EnergySnapshot.DescribeCoefficientBasis(parameters.SkinDepthMicrometers),
                coefficientBasisBySkinDepth = new
                {
                    zero = "T_n(tau)/sqrt(1-tau^2)",
                    positive = "P_n(tau)",
                    indices = "n=0..N-1"
                },
                application = "Diffraction.WpfPrototype"
            },
            parameters = new
            {
                wavelengthMicrometers = parameters.WavelengthMicrometers,
                incidenceAngleDegrees = parameters.IncidenceAngleDegrees,
                plateStart = parameters.PlateStart,
                plateEnd = parameters.PlateEnd,
                harmonicCount = parameters.HarmonicCount,
                skinDepthMicrometers = parameters.SkinDepthMicrometers,
                outputLeft = parameters.OutputLeft,
                outputRight = parameters.OutputRight,
                outputBottom = parameters.OutputBottom,
                outputTop = parameters.OutputTop,
                series = new
                {
                    skinDepthEnabled = parameters.SeriesSkinDepthEnabled,
                    skinDepthStart = parameters.SeriesSkinDepthStart,
                    skinDepthEnd = parameters.SeriesSkinDepthEnd,
                    pointCount = parameters.SeriesPointCount,
                    angleEnabled = parameters.SeriesAngleEnabled,
                    angleStartDegrees = parameters.SeriesAngleStartDegrees,
                    angleEndDegrees = parameters.SeriesAngleEndDegrees,
                    angleStepDegrees = parameters.SeriesAngleStepDegrees
                }
            }
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Текстовый отчёт: баланс энергии, потоки, диагностика и сводки серий.
    /// </summary>
    public static string BuildReportText(
        EnergySnapshot energy,
        IEnumerable<FluxRow> fluxRows,
        IEnumerable<DiagnosticRow> diagnostics,
        IEnumerable<string> seriesSummaries,
        string backendName,
        DateTime exportedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(energy);
        var builder = new StringBuilder();
        builder.AppendLine("ОТЧЁТ О РАСЧЁТЕ ДИФРАКЦИИ НА ПЛАСТИНЕ");
        builder.AppendLine("Экспортировано (UTC): " + exportedAtUtc.ToString("O", InvariantCulture));
        builder.AppendLine("Решатель: " + backendName);
        builder.AppendLine("Базис выбранного расчёта: " + energy.CoefficientBasisDisplay);
        builder.AppendLine("Базисы в сериях меняются при δ = 0: " + EnergySnapshot.DescribeCoefficientBasis(0) + " " + EnergySnapshot.DescribeCoefficientBasis(1));
        builder.AppendLine();
        builder.AppendLine("=== ЭНЕРГЕТИКА (доли падающего потока через пластину) ===");
        if (energy.IsAvailable)
        {
            builder.AppendLine($"R_scat обратно:      {energy.ReflectedScattered.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"T_scat вперёд:       {energy.ForwardScattered.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"A_J пластина:        {energy.Absorbed.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"A_flux:              {energy.FluxAbsorbed.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"P_ext / I_plate (независимый интеграл): {energy.Extinction.ToString("R", InvariantCulture)}");
            builder.AppendLine($"Оптическая теорема, % от P_ext: {energy.OpticalBalanceErrorPercent.ToString("R", InvariantCulture)}");
            builder.AppendLine($"C_back / C_forward / C_abs / C_ext, мкм: {energy.ReflectedCrossSection.ToString("R", InvariantCulture)} / {energy.ForwardCrossSection.ToString("R", InvariantCulture)} / {energy.AbsorbedCrossSection.ToString("R", InvariantCulture)} / {energy.ExtinctionCrossSection.ToString("R", InvariantCulture)}");
            builder.AppendLine("R_scat и T_scat не являются коэффициентами R и T. Их сумма с A сравнивается с P_ext/I_plate, а не с единицей.");
            builder.AppendLine($"Локальная невязка ЗСЭ, %: {energy.LocalBalanceErrorPercent.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"|R_scat − T_scat|, %:     {energy.FarFieldMismatchPercent.ToString("0.000000", InvariantCulture)}");
            builder.AppendLine($"Поток сверху / снизу:     {energy.SheetAbove.ToString("0.000000", InvariantCulture)} / {energy.SheetBelow.ToString("0.000000", InvariantCulture)}");
        }
        else
        {
            builder.AppendLine("Расчёт не выполнен.");
        }
        builder.AppendLine();
        builder.AppendLine("=== ПОТОКИ ===");
        foreach (FluxRow row in fluxRows)
        {
            builder.AppendLine($"[{row.Category}] {row.Metric}: {row.Top.ToString("0.000000", InvariantCulture)} / {row.Bottom.ToString("0.000000", InvariantCulture)} " +
                               $"(Δ {row.DifferencePercent.ToString("0.000000", InvariantCulture)}%, допуск {row.TolerancePercent.ToString("0.######", InvariantCulture)}%) — {row.Status}");
        }
        builder.AppendLine();
        builder.AppendLine("=== ДИАГНОСТИКА ===");
        foreach (DiagnosticRow row in diagnostics)
        {
            builder.AppendLine($"[{row.Group}] {row.Check}: {row.Value} (допуск {row.Tolerance}) — {row.Status}");
        }
        builder.AppendLine();
        builder.AppendLine("=== СЕРИИ ===");
        foreach (string summary in seriesSummaries)
            builder.AppendLine(summary);
        return builder.ToString();
    }

    /// <summary>
    /// Простой SVG графика: оси, сетка, ломаные серий и легенда.
    /// </summary>
    public static string BuildPlotSvg(PlotData plot, int width = 960, int height = 540)
    {
        ArgumentNullException.ThrowIfNull(plot);
        if (!plot.HasData)
            throw new ArgumentException("График без точек не может быть экспортирован в SVG.", nameof(plot));

        const int left = 70;
        const int right = 20;
        const int top = 20;
        const int bottom = 60;
        int plotWidth = width - left - right;
        int plotHeight = height - top - bottom;

        double xMin = plot.XMinimum;
        double xMax = plot.XMaximum;
        double yMin = plot.YMinimum;
        double yMax = plot.YMaximum;

        var builder = new StringBuilder();
        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
        builder.AppendLine($"<rect x=\"{left}\" y=\"{top}\" width=\"{plotWidth}\" height=\"{plotHeight}\" fill=\"none\" stroke=\"#333\" stroke-width=\"1\"/>");

        for (int i = 1; i < 5; i++)
        {
            int gx = left + plotWidth * i / 5;
            int gy = top + plotHeight * i / 5;
            builder.AppendLine($"<line x1=\"{gx}\" y1=\"{top}\" x2=\"{gx}\" y2=\"{top + plotHeight}\" stroke=\"#ddd\" stroke-width=\"1\"/>");
            builder.AppendLine($"<line x1=\"{left}\" y1=\"{gy}\" x2=\"{left + plotWidth}\" y2=\"{gy}\" stroke=\"#ddd\" stroke-width=\"1\"/>");
            double xValue = xMin + (xMax - xMin) * i / 5.0;
            double yValue = yMax - (yMax - yMin) * i / 5.0;
            builder.AppendLine($"<text x=\"{gx}\" y=\"{top + plotHeight + 16}\" font-size=\"11\" text-anchor=\"middle\" fill=\"#555\">{FormatNumber(xValue)}</text>");
            builder.AppendLine($"<text x=\"{left - 8}\" y=\"{gy + 4}\" font-size=\"11\" text-anchor=\"end\" fill=\"#555\">{FormatNumber(yValue)}</text>");
        }
        builder.AppendLine($"<text x=\"{left + plotWidth / 2}\" y=\"{height - 12}\" font-size=\"13\" text-anchor=\"middle\" fill=\"#222\">{EscapeXml(plot.XAxisTitle)}</text>");
        builder.AppendLine($"<text x=\"{18}\" y=\"{top + plotHeight / 2}\" font-size=\"13\" text-anchor=\"middle\" fill=\"#222\" transform=\"rotate(-90 18 {top + plotHeight / 2})\">{EscapeXml(plot.YAxisTitle)}</text>");

        foreach (PlotSeriesData series in plot.Series)
        {
            var points = new StringBuilder();
            foreach (PlotPointData point in series.Points)
            {
                double sx = left + (point.X - xMin) / (xMax - xMin) * plotWidth;
                double sy = top + plotHeight - (point.Y - yMin) / (yMax - yMin) * plotHeight;
                points.Append(FormatNumber(sx)).Append(',').Append(FormatNumber(sy)).Append(' ');
            }
            string dash = series.IsDashed ? " stroke-dasharray=\"7,5\"" : string.Empty;
            builder.AppendLine($"<polyline fill=\"none\" stroke=\"{series.Color}\" stroke-width=\"{series.Thickness.ToString("0.0", InvariantCulture)}\"{dash} points=\"{points.ToString().Trim()}\">" +
                               $"<title>{EscapeXml(series.Name)}</title></polyline>");
            if (series.ShowMarkers)
            {
                foreach (PlotPointData point in series.Points)
                {
                    double sx = left + (point.X - xMin) / (xMax - xMin) * plotWidth;
                    double sy = top + plotHeight - (point.Y - yMin) / (yMax - yMin) * plotHeight;
                    builder.AppendLine($"<circle cx=\"{FormatNumber(sx)}\" cy=\"{FormatNumber(sy)}\" r=\"3.5\" fill=\"{series.Color}\" stroke=\"white\" stroke-width=\"1\">" +
                                       $"<title>{EscapeXml(series.Name)}</title></circle>");
                }
            }
        }

        int legendX = left + 12;
        int legendY = top + 18;
        foreach (PlotSeriesData series in plot.Series)
        {
            builder.AppendLine($"<line x1=\"{legendX}\" y1=\"{legendY - 4}\" x2=\"{legendX + 26}\" y2=\"{legendY - 4}\" stroke=\"{series.Color}\" stroke-width=\"3\"/>");
            builder.AppendLine($"<text x=\"{legendX + 32}\" y=\"{legendY}\" font-size=\"12\" fill=\"#222\">{EscapeXml(series.Name)}</text>");
            legendY += 20;
        }
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string FormatNumber(double value) => value.ToString("0.######", InvariantCulture);
    private static string CsvCell(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
