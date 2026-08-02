namespace Diffraction.WpfPrototype.Models;

public sealed class CalculationRun
{
    public required string DateLabel { get; init; }
    public required string SkinDepth { get; init; }
    public required int N { get; init; }
    public required string Backend { get; init; }
    public required string Status { get; init; }
    public required string StatusKind { get; init; }

    public string Details => $"N {N}  •  тонкая пластина  •  {Backend}";
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
