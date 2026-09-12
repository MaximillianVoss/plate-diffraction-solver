using System.Globalization;

namespace Diffraction.WpfPrototype.Models;

public sealed class CalculationRun
{
    public required int RunNumber { get; init; }
    public required string DateLabel { get; init; }
    public required CalculationParameters Parameters { get; init; }
    public required bool IsSeries { get; init; }
    public required string Backend { get; init; }
    public required string Status { get; init; }
    public required string StatusKind { get; init; }
    public CalculationOutput? Output { get; init; }

    private static CultureInfo RussianCulture { get; } = CultureInfo.GetCultureInfo("ru-RU");

    public int N => Parameters.HarmonicCount;
    public string SkinDepth => IsSeries
        ? $"δ {Format(Parameters.SeriesSkinDepthStart, "0.000")}…{Format(Parameters.SeriesSkinDepthEnd, "0.000")}"
        : $"δ {Format(Parameters.SkinDepthMicrometers, "0.000000")}";
    public string Details => $"N {N}  •  {(IsSeries ? "серия δ/θ" : "одиночный расчёт")}  •  {Backend}";
    public string HistoryTitle => $"#{RunNumber:000}  •  {DateLabel}";
    public string Title => $"{(IsSeries ? "Серия" : "Расчёт")} #{RunNumber:000} — тонкая пластина";
    public string ParameterSummary => IsSeries
        ? $"λ {Format(Parameters.WavelengthMicrometers, "0.000")} мкм  •  N {N}  •  δ {Format(Parameters.SeriesSkinDepthStart, "0.000")}…{Format(Parameters.SeriesSkinDepthEnd, "0.000")} ({Parameters.SeriesPointCount})  •  θ {Format(Parameters.SeriesAngleStartDegrees, "0.#")}…{Format(Parameters.SeriesAngleEndDegrees, "0.#")}° / {Format(Parameters.SeriesAngleStepDegrees, "0.#")}°  •  {Backend}"
        : $"λ {Format(Parameters.WavelengthMicrometers, "0.000")} мкм  •  θ {Format(Parameters.IncidenceAngleDegrees, "0.0")}°  •  {SkinDepth}  •  N {N}  •  {Backend}";
    public string FullParameterSummary =>
        $"{ParameterSummary}  •  пластина [{Format(Parameters.PlateStart, "0.000")}; {Format(Parameters.PlateEnd, "0.000")}]  •  область x [{Format(Parameters.OutputLeft, "0.00")}; {Format(Parameters.OutputRight, "0.00")}], y [{Format(Parameters.OutputBottom, "0.00")}; {Format(Parameters.OutputTop, "0.00")}]  •  {DateLabel}";
    public string SkinDepthValue => SkinDepth.StartsWith("δ ", StringComparison.Ordinal) ? SkinDepth[2..] : SkinDepth;

    private static string Format(double value, string format) => value.ToString(format, RussianCulture);
}

public sealed class FluxRow
{
    public required string Category { get; init; }
    public required string Metric { get; init; }
    public required double Top { get; init; }
    public required double Bottom { get; init; }
    public required double DifferencePercent { get; init; }
    public required double TolerancePercent { get; init; }
    public required string Status { get; init; }
}

public sealed class CoefficientRow
{
    public required int Index { get; init; }
    public required double CollocationRe { get; init; }
    public required double CollocationIm { get; init; }
    public required double GalerkinRe { get; init; }
    public required double GalerkinIm { get; init; }
    public double Delta => Math.Sqrt(
        Math.Pow(CollocationRe - GalerkinRe, 2) +
        Math.Pow(CollocationIm - GalerkinIm, 2));
}

public sealed class DiagnosticRow
{
    public required string Group { get; init; }
    public required string Check { get; init; }
    public required string Value { get; init; }
    public required string Tolerance { get; init; }
    public required string Status { get; init; }
}

public sealed class PlotPointData
{
    public PlotPointData(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

public sealed class PlotSeriesData
{
    public PlotSeriesData(
        string name,
        string color,
        IReadOnlyList<PlotPointData> points,
        bool isDashed = false,
        bool showMarkers = false,
        double thickness = 2.2)
    {
        Name = name;
        Color = color;
        Points = points;
        IsDashed = isDashed;
        ShowMarkers = showMarkers;
        Thickness = thickness;
    }

    public string Name { get; }
    public string Color { get; }
    public IReadOnlyList<PlotPointData> Points { get; }
    public bool IsDashed { get; }
    public bool ShowMarkers { get; }
    public double Thickness { get; }
}

public sealed class PlotData
{
    public PlotData(
        string xAxisTitle,
        string yAxisTitle,
        IReadOnlyList<PlotSeriesData> series,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum,
        string emptyMessage = "Выполните расчёт")
    {
        XAxisTitle = xAxisTitle;
        YAxisTitle = yAxisTitle;
        Series = series;
        XMinimum = xMinimum;
        XMaximum = xMaximum;
        YMinimum = yMinimum;
        YMaximum = yMaximum;
        EmptyMessage = emptyMessage;
    }

    public string XAxisTitle { get; }
    public string YAxisTitle { get; }
    public IReadOnlyList<PlotSeriesData> Series { get; }
    public double XMinimum { get; }
    public double XMaximum { get; }
    public double YMinimum { get; }
    public double YMaximum { get; }
    public string EmptyMessage { get; }
    public bool HasData => Series.Any(item => item.Points.Count > 0);

    public static PlotData Empty(string xAxisTitle, string yAxisTitle, string? message = null) =>
        new(
            xAxisTitle,
            yAxisTitle,
            Array.Empty<PlotSeriesData>(),
            0,
            1,
            0,
            1,
            message ?? "Задайте параметры и выполните расчёт");
}

public sealed class FieldMapData
{
    public FieldMapData(
        int width,
        int height,
        double[] values,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum,
        double scaleMinimum,
        double scaleMaximum,
        double plateStart,
        double plateEnd)
    {
        if (width <= 1 || height <= 1)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (values.Length != width * height)
            throw new ArgumentException("Размер массива поля не совпадает с размером сетки.", nameof(values));

        Width = width;
        Height = height;
        Values = values;
        XMinimum = xMinimum;
        XMaximum = xMaximum;
        YMinimum = yMinimum;
        YMaximum = yMaximum;
        ScaleMinimum = scaleMinimum;
        ScaleMaximum = scaleMaximum;
        PlateStart = plateStart;
        PlateEnd = plateEnd;
    }

    public int Width { get; }
    public int Height { get; }
    public double[] Values { get; }
    public double XMinimum { get; }
    public double XMaximum { get; }
    public double YMinimum { get; }
    public double YMaximum { get; }
    public double ScaleMinimum { get; }
    public double ScaleMaximum { get; }
    public double PlateStart { get; }
    public double PlateEnd { get; }
}

public sealed class CalculationOutput
{
    public required EnergySnapshot Energy { get; init; }
    public required IReadOnlyList<CoefficientRow> Coefficients { get; init; }
    public required IReadOnlyList<DiagnosticRow> Diagnostics { get; init; }
    public required PlotData SlicePlot { get; init; }
    public required PlotData MethodPlot { get; init; }
    public required PlotData MethodDifferencePlot { get; init; }
    public required PlotData SkinDifferencePlot { get; init; }
    public required PlotData CurrentEnergyPlot { get; init; }
    public required PlotData SkinEnergyPlot { get; init; }
    public required PlotData AngleEnergyPlot { get; init; }
    public required PlotData SeriesDiagnosticsPlot { get; init; }
    public FieldMapData? IdealFieldMap { get; init; }
    public FieldMapData? SkinFieldMap { get; init; }
    public required string DiagnosticsSummary { get; init; }
    public required string SeriesDiagnosticsCaption { get; init; }
    public required IReadOnlyList<string> SeriesCheckSummaries { get; init; }
    public required string BackendName { get; init; }
}
