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
