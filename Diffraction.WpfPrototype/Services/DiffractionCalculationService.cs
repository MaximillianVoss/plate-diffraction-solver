using System.Globalization;
using Diffraction.Core;
using Diffraction.WpfPrototype.Models;
using static Diffraction.Core.DiffractionMath;

namespace Diffraction.WpfPrototype.Services;

public static class DiffractionCalculationService
{
    private const int ProfileSampleCount = 321;
    private const int MethodSampleCount = 241;
    private const int FieldMapWidth = 96;
    private const int FieldMapHeight = 72;

    public static CalculationOutput Calculate(
        CalculationParameters parameters,
        bool includeSeries,
        CancellationToken cancellationToken,
        bool includeFieldMaps = true)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Validate(parameters, includeSeries);

        double angleRadians = DegreesToRadians(parameters.IncidenceAngleDegrees);
        DifrOnLenta idealCollocation = SolveCollocation(parameters, angleRadians, 0, cancellationToken);
        DifrOnLenta skinCollocation = parameters.SkinDepthMicrometers == 0
            ? idealCollocation
            : SolveCollocation(parameters, angleRadians, parameters.SkinDepthMicrometers, cancellationToken);
        DifrOnLenta skinGalerkin = GalerkinSolver.SolveSinglePlate(
            parameters.PlateStart,
            parameters.PlateEnd,
            parameters.WavelengthMicrometers,
            angleRadians,
            parameters.HarmonicCount,
            parameters.SkinDepthMicrometers,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        EnergySnapshot selectedEnergy = CalculateEnergySnapshot(
            skinCollocation,
            parameters.SkinDepthMicrometers,
            angleSamples: 720,
            plateSamples: 400);

        MethodComparison comparison = BuildMethodComparison(
            parameters,
            skinCollocation,
            skinGalerkin,
            cancellationToken);
        PlotData slicePlot = BuildSkinComparisonPlot(
            parameters,
            idealCollocation,
            skinCollocation,
            cancellationToken);
        PlotData skinDifferencePlot = BuildSkinDifferencePlot(
            parameters,
            idealCollocation,
            skinCollocation,
            cancellationToken);

        (FieldMapData? idealMap, FieldMapData? skinMap) = includeFieldMaps
            ? BuildFieldMaps(parameters, idealCollocation, skinCollocation, cancellationToken)
            : (null, null);

        SeriesCalculation series = includeSeries
            ? BuildSeries(parameters, skinCollocation, cancellationToken)
            : SeriesCalculation.Empty;

        double collocationBoundaryErrorPercent = skinCollocation.VerifyBoundaryConditions() * 100.0;
        cancellationToken.ThrowIfCancellationRequested();
        double galerkinBoundaryErrorPercent = skinGalerkin.VerifyBoundaryConditions() * 100.0;
        double helmholtzResidual = skinCollocation.VerifyHelmholtz();

        IReadOnlyList<CoefficientRow> coefficients = BuildCoefficientRows(
            skinCollocation,
            skinGalerkin);
        IReadOnlyList<DiagnosticRow> diagnostics = BuildDiagnostics(
            selectedEnergy,
            collocationBoundaryErrorPercent,
            galerkinBoundaryErrorPercent,
            helmholtzResidual,
            comparison);

        return new CalculationOutput
        {
            Energy = selectedEnergy,
            Coefficients = coefficients,
            Diagnostics = diagnostics,
            SlicePlot = slicePlot,
            MethodPlot = comparison.FieldPlot,
            MethodDifferencePlot = comparison.DifferencePlot,
            SkinDifferencePlot = skinDifferencePlot,
            CurrentEnergyPlot = BuildCurrentEnergyPlot(selectedEnergy),
            SkinEnergyPlot = series.SkinEnergyPlot,
            AngleEnergyPlot = series.AngleEnergyPlot,
            SeriesDiagnosticsPlot = series.DiagnosticsPlot,
            IdealFieldMap = idealMap,
            SkinFieldMap = skinMap,
            DiagnosticsSummary = BuildDiagnosticsSummary(
                selectedEnergy,
                collocationBoundaryErrorPercent,
                galerkinBoundaryErrorPercent,
                helmholtzResidual,
                comparison),
            SeriesDiagnosticsCaption = series.DiagnosticsCaption,
            SeriesCheckSummaries = series.CheckSummaries,
            BackendName = skinCollocation.LastSolvePerformance?.BackendName ?? "CPU (C#)"
        };
    }

    private static void Validate(CalculationParameters parameters, bool includeSeries)
    {
        if (!double.IsFinite(parameters.WavelengthMicrometers) || parameters.WavelengthMicrometers <= 0)
            throw new ArgumentException("Длина волны должна быть положительным числом.");
        if (!double.IsFinite(parameters.IncidenceAngleDegrees) ||
            Math.Abs(Math.Sin(DegreesToRadians(parameters.IncidenceAngleDegrees))) < 1e-8)
        {
            throw new ArgumentException("Угол должен задавать ненулевой падающий поток через пластину.");
        }
        if (!double.IsFinite(parameters.PlateStart) || !double.IsFinite(parameters.PlateEnd) ||
            parameters.PlateStart >= parameters.PlateEnd)
        {
            throw new ArgumentException("Для одной пластины должно выполняться α₁ < β₁.");
        }
        if (parameters.HarmonicCount < 2 || parameters.HarmonicCount > 200)
            throw new ArgumentException("Параметр N должен находиться в диапазоне от 2 до 200.");
        if (!double.IsFinite(parameters.SkinDepthMicrometers) || parameters.SkinDepthMicrometers < 0)
            throw new ArgumentException("Толщина скин-слоя не может быть отрицательной.");
        if (!double.IsFinite(parameters.OutputLeft) || !double.IsFinite(parameters.OutputRight) ||
            parameters.OutputLeft >= parameters.OutputRight)
        {
            throw new ArgumentException("Левая граница области вывода должна быть меньше правой.");
        }
        if (!double.IsFinite(parameters.OutputBottom) || !double.IsFinite(parameters.OutputTop) ||
            parameters.OutputBottom >= parameters.OutputTop)
        {
            throw new ArgumentException("Нижняя граница области вывода должна быть меньше верхней.");
        }

        if (!includeSeries)
            return;

        if (!double.IsFinite(parameters.SeriesSkinDepthStart) ||
            !double.IsFinite(parameters.SeriesSkinDepthEnd) ||
            parameters.SeriesSkinDepthStart < 0 ||
            parameters.SeriesSkinDepthStart > parameters.SeriesSkinDepthEnd)
        {
            throw new ArgumentException("Некорректный диапазон толщины скин-слоя.");
        }
        if (parameters.SeriesPointCount < 2 || parameters.SeriesPointCount > 101)
            throw new ArgumentException("Число точек серии по δ должно быть от 2 до 101.");
        if (!double.IsFinite(parameters.SeriesAngleStartDegrees) ||
            !double.IsFinite(parameters.SeriesAngleEndDegrees) ||
            !double.IsFinite(parameters.SeriesAngleStepDegrees) ||
            parameters.SeriesAngleStepDegrees <= 0 ||
            parameters.SeriesAngleStartDegrees > parameters.SeriesAngleEndDegrees)
        {
            throw new ArgumentException("Некорректный диапазон углов серии.");
        }

        int anglePointCount = GetAnglePointCount(parameters);
        if (anglePointCount > 181)
            throw new ArgumentException("Серия по углу не должна содержать больше 181 точки.");

        for (int i = 0; i < anglePointCount; i++)
        {
            double angle = parameters.SeriesAngleStartDegrees + i * parameters.SeriesAngleStepDegrees;
            if (Math.Abs(Math.Sin(DegreesToRadians(angle))) < 1e-8)
                throw new ArgumentException("Диапазон углов содержит точку с нулевым падающим потоком.");
        }
    }

    private static DifrOnLenta SolveCollocation(
        CalculationParameters parameters,
        double angleRadians,
        double skinDepth,
        CancellationToken cancellationToken)
    {
        DifrOnLenta solver = new DifrOnLenta(
            parameters.PlateStart,
            parameters.PlateEnd,
            parameters.WavelengthMicrometers,
            angleRadians,
            parameters.HarmonicCount,
            skinDepth);

        int status = solver.SolveDifr(cancellationToken);
        if (status != 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Не удалось решить задачу методом коллокации.");
        }

        return solver;
    }

    private static EnergySnapshot CalculateEnergySnapshot(
        DifrOnLenta solver,
        double skinDepth,
        int angleSamples,
        int plateSamples)
    {
        double incident = solver.CalculatePlateIncidentEnergy();
        if (!double.IsFinite(incident) || incident <= 0)
            throw new InvalidOperationException("Падающий поток через пластину равен нулю.");

        DifrOnLenta.FarFieldScatteredEnergyComponents farField =
            solver.CalculateFarFieldScatteredEnergy(angleSamples, plateSamples);
        DifrOnLenta.ScatteredSheetFluxComponents sheetFlux =
            solver.CalculateScatteredSheetFluxComponents(Math.Max(160, plateSamples / 2));
        DifrOnLenta.EnergyComponents energy = solver.CalculateEnergyComponents(includeContourDiagnostic: false);

        return new EnergySnapshot(
            skinDepth,
            Normalize(farField.ReflectedScattered, incident),
            Normalize(farField.TransmittedScattered, incident),
            Normalize(energy.Absorbed, incident),
            Normalize(sheetFlux.AboveOutgoing, incident),
            Normalize(sheetFlux.BelowOutgoing, incident),
            Normalize(energy.FluxAbsorbed, incident),
            Math.Abs(energy.LocalBalanceResidual) / incident * 100.0);
    }

    private static PlotData BuildSkinComparisonPlot(
        CalculationParameters parameters,
        DifrOnLenta idealSolver,
        DifrOnLenta skinSolver,
        CancellationToken cancellationToken)
    {
        double[] xValues = BuildLinearValues(parameters.OutputLeft, parameters.OutputRight, ProfileSampleCount);
        double z = parameters.WavelengthMicrometers / 10.0;
        double[] ideal = SampleRealField(idealSolver, xValues, z, cancellationToken);
        double[] skin = ReferenceEquals(idealSolver, skinSolver)
            ? (double[])ideal.Clone()
            : SampleRealField(skinSolver, xValues, z, cancellationToken);

        return CreatePlot(
            "x",
            "Re u(x, λ/10)",
            new PlotSeriesData("Без скин-слоя", "#2563EB", ToPoints(xValues, ideal)),
            new PlotSeriesData("Со скин-слоем", "#DC2626", ToPoints(xValues, skin)));
    }

    private static PlotData BuildSkinDifferencePlot(
        CalculationParameters parameters,
        DifrOnLenta idealSolver,
        DifrOnLenta skinSolver,
        CancellationToken cancellationToken)
    {
        double[] xValues = BuildLinearValues(parameters.OutputLeft, parameters.OutputRight, ProfileSampleCount);
        double z = parameters.WavelengthMicrometers / 10.0;
        double[] differences = new double[xValues.Length];

        Parallel.For(0, xValues.Length, new ParallelOptions { CancellationToken = cancellationToken }, i =>
        {
            Compl ideal = idealSolver.u(xValues[i], z);
            Compl skin = skinSolver.u(xValues[i], z);
            differences[i] = Compl.Abs(skin - ideal);
        });

        return CreatePlot(
            "x",
            "|u_skin − u_ideal|",
            includeZero: true,
            new PlotSeriesData("Разность со скин-слоем / без", "#EA580C", ToPoints(xValues, differences)));
    }

    private static MethodComparison BuildMethodComparison(
        CalculationParameters parameters,
        DifrOnLenta collocation,
        DifrOnLenta galerkin,
        CancellationToken cancellationToken)
    {
        double[] xValues = new double[MethodSampleCount];
        double[] collocationValues = new double[MethodSampleCount];
        double[] galerkinValues = new double[MethodSampleCount];
        double[] differences = new double[MethodSampleCount];
        double z = parameters.WavelengthMicrometers / 10.0;

        Parallel.For(0, MethodSampleCount, new ParallelOptions { CancellationToken = cancellationToken }, i =>
        {
            double x = parameters.PlateStart + (i + 0.5) / MethodSampleCount *
                (parameters.PlateEnd - parameters.PlateStart);
            Compl collocationField = collocation.u(x, z);
            Compl galerkinField = galerkin.u(x, z);

            xValues[i] = x;
            collocationValues[i] = collocationField.Re;
            galerkinValues[i] = galerkinField.Re;
            differences[i] = Compl.Abs(collocationField - galerkinField);
        });

        double maxDifference = differences.Max();
        double meanDifference = differences.Average();
        double maxCoefficientDifference = 0;
        for (int i = 0; i < Math.Min(collocation.y.Length, galerkin.y.Length); i++)
            maxCoefficientDifference = Math.Max(maxCoefficientDifference, Compl.Abs(collocation.y[i] - galerkin.y[i]));

        return new MethodComparison
        {
            FieldPlot = CreatePlot(
                "x",
                "Re u(x, λ/10)",
                new PlotSeriesData("Коллокация", "#2563EB", ToPoints(xValues, collocationValues)),
                new PlotSeriesData("Галеркин", "#DB2777", ToPoints(xValues, galerkinValues), isDashed: true)),
            DifferencePlot = CreatePlot(
                "x",
                "|u_col − u_gal|",
                includeZero: true,
                new PlotSeriesData("Модуль разности", "#EA580C", ToPoints(xValues, differences))),
            MaximumDifference = maxDifference,
            MeanDifference = meanDifference,
            MaximumCoefficientDifference = maxCoefficientDifference
        };
    }

    private static IReadOnlyList<CoefficientRow> BuildCoefficientRows(
        DifrOnLenta collocation,
        DifrOnLenta galerkin)
    {
        int count = Math.Min(collocation.y.Length, galerkin.y.Length);
        List<CoefficientRow> rows = new(count);
        for (int i = 0; i < count; i++)
        {
            rows.Add(new CoefficientRow
            {
                Index = i + 1,
                CollocationRe = collocation.y[i].Re,
                CollocationIm = collocation.y[i].Im,
                GalerkinRe = galerkin.y[i].Re,
                GalerkinIm = galerkin.y[i].Im
            });
        }

        return rows;
    }

    private static (FieldMapData Ideal, FieldMapData Skin) BuildFieldMaps(
        CalculationParameters parameters,
        DifrOnLenta idealSolver,
        DifrOnLenta skinSolver,
        CancellationToken cancellationToken)
    {
        double[] idealValues = SampleFieldMap(parameters, idealSolver, cancellationToken);
        double[] skinValues = ReferenceEquals(idealSolver, skinSolver)
            ? (double[])idealValues.Clone()
            : SampleFieldMap(parameters, skinSolver, cancellationToken);
        double scaleMaximum = Math.Max(idealValues.Max(), skinValues.Max());
        if (!double.IsFinite(scaleMaximum) || scaleMaximum <= 0)
            scaleMaximum = 1.0;

        FieldMapData Create(double[] values) => new(
            FieldMapWidth,
            FieldMapHeight,
            values,
            parameters.OutputLeft,
            parameters.OutputRight,
            parameters.OutputBottom,
            parameters.OutputTop,
            0,
            scaleMaximum,
            parameters.PlateStart,
            parameters.PlateEnd);

        return (Create(idealValues), Create(skinValues));
    }

    private static double[] SampleFieldMap(
        CalculationParameters parameters,
        DifrOnLenta solver,
        CancellationToken cancellationToken)
    {
        double[] values = new double[FieldMapWidth * FieldMapHeight];
        Parallel.For(0, FieldMapHeight, new ParallelOptions { CancellationToken = cancellationToken }, row =>
        {
            double y = parameters.OutputTop -
                (parameters.OutputTop - parameters.OutputBottom) * row / (FieldMapHeight - 1.0);
            for (int column = 0; column < FieldMapWidth; column++)
            {
                double x = parameters.OutputLeft +
                    (parameters.OutputRight - parameters.OutputLeft) * column / (FieldMapWidth - 1.0);
                double value = Compl.Abs(solver.u(x, y));
                values[row * FieldMapWidth + column] = double.IsFinite(value) ? value : 0;
            }
        });
        return values;
    }

    private static SeriesCalculation BuildSeries(
        CalculationParameters parameters,
        DifrOnLenta selectedSolver,
        CancellationToken cancellationToken)
    {
        List<EnergySnapshot> skinSnapshots = new(parameters.SeriesPointCount);
        double[] skinDepths = BuildLinearValues(
            parameters.SeriesSkinDepthStart,
            parameters.SeriesSkinDepthEnd,
            parameters.SeriesPointCount);

        foreach (double skinDepth in skinDepths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool useSelected = Math.Abs(skinDepth - parameters.SkinDepthMicrometers) <= 1e-12;
            DifrOnLenta solver = useSelected
                ? selectedSolver
                : SolveCollocation(parameters, DegreesToRadians(parameters.IncidenceAngleDegrees), skinDepth, cancellationToken);
            skinSnapshots.Add(CalculateEnergySnapshot(
                solver,
                skinDepth,
                angleSamples: 180,
                plateSamples: 160));
        }

        int anglePointCount = GetAnglePointCount(parameters);
        double[] angles = new double[anglePointCount];
        List<EnergySnapshot> angleSnapshots = new(anglePointCount);
        for (int i = 0; i < anglePointCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double angle = parameters.SeriesAngleStartDegrees + i * parameters.SeriesAngleStepDegrees;
            angles[i] = angle;
            bool useSelected = Math.Abs(angle - parameters.IncidenceAngleDegrees) <= 1e-12;
            DifrOnLenta solver = useSelected
                ? selectedSolver
                : SolveCollocation(parameters, DegreesToRadians(angle), parameters.SkinDepthMicrometers, cancellationToken);
            angleSnapshots.Add(CalculateEnergySnapshot(
                solver,
                parameters.SkinDepthMicrometers,
                angleSamples: 180,
                plateSamples: 160));
        }

        PlotData skinPlot = CreateEnergyPlot("Толщина δ", skinDepths, skinSnapshots);
        PlotData anglePlot = CreateEnergyPlot("Угол θ, °", angles, angleSnapshots);
        PlotData diagnosticsPlot = CreatePlot(
            "Толщина δ",
            "Отклонение, %",
            includeZero: true,
            new PlotSeriesData(
                "Невязка ЗСЭ",
                "#EA580C",
                ToPoints(skinDepths, skinSnapshots.Select(item => item.LocalBalanceErrorPercent).ToArray())),
            new PlotSeriesData(
                "|R_scat − T_scat|",
                "#16A34A",
                ToPoints(skinDepths, skinSnapshots.Select(item => item.FarFieldMismatchPercent).ToArray()),
                isDashed: true));

        double maximumBalanceError = skinSnapshots.Max(item => item.LocalBalanceErrorPercent);
        double maximumFarFieldMismatch = skinSnapshots.Max(item => item.FarFieldMismatchPercent);
        string caption = string.Format(
            RussianCulture,
            "Максимум по серии: невязка ЗСЭ {0:0.000000}%; |R_scat − T_scat| {1:0.000000}%.",
            maximumBalanceError,
            maximumFarFieldMismatch);

        return new SeriesCalculation
        {
            SkinEnergyPlot = skinPlot,
            AngleEnergyPlot = anglePlot,
            DiagnosticsPlot = diagnosticsPlot,
            DiagnosticsCaption = caption,
            CheckSummaries = new[]
            {
                string.Format(
                    RussianCulture,
                    "Серия δ: {0} точек; R_scat {1:0.000000}…{2:0.000000}; A_J {3:0.000000}…{4:0.000000}",
                    skinSnapshots.Count,
                    skinSnapshots.Min(item => item.ReflectedScattered),
                    skinSnapshots.Max(item => item.ReflectedScattered),
                    skinSnapshots.Min(item => item.Absorbed),
                    skinSnapshots.Max(item => item.Absorbed)),
                string.Format(
                    RussianCulture,
                    "Серия θ: {0} точек; R_scat {1:0.000000}…{2:0.000000}",
                    angleSnapshots.Count,
                    angleSnapshots.Min(item => item.ReflectedScattered),
                    angleSnapshots.Max(item => item.ReflectedScattered)),
                caption
            }
        };
    }

    private static PlotData BuildCurrentEnergyPlot(EnergySnapshot energy)
    {
        double x = energy.SkinDepth;
        return CreatePlot(
            "Толщина δ",
            "Доля падающей энергии",
            includeZero: true,
            new PlotSeriesData("R_scat обратно", "#2563EB", OnePoint(x, energy.ReflectedScattered), showMarkers: true),
            new PlotSeriesData("T_scat вперёд", "#16A34A", OnePoint(x, energy.ForwardScattered), isDashed: true, showMarkers: true),
            new PlotSeriesData("A_J пластина", "#EA580C", OnePoint(x, energy.Absorbed), showMarkers: true));
    }

    private static PlotData CreateEnergyPlot(
        string xAxisTitle,
        double[] xValues,
        IReadOnlyList<EnergySnapshot> snapshots)
    {
        return CreatePlot(
            xAxisTitle,
            "Доля падающей энергии",
            includeZero: true,
            new PlotSeriesData(
                "R_scat обратно",
                "#2563EB",
                ToPoints(xValues, snapshots.Select(item => item.ReflectedScattered).ToArray())),
            new PlotSeriesData(
                "T_scat вперёд",
                "#16A34A",
                ToPoints(xValues, snapshots.Select(item => item.ForwardScattered).ToArray()),
                isDashed: true),
            new PlotSeriesData(
                "A_J пластина",
                "#EA580C",
                ToPoints(xValues, snapshots.Select(item => item.Absorbed).ToArray())));
    }

    private static IReadOnlyList<DiagnosticRow> BuildDiagnostics(
        EnergySnapshot energy,
        double collocationBoundaryErrorPercent,
        double galerkinBoundaryErrorPercent,
        double helmholtzResidual,
        MethodComparison comparison)
    {
        return new[]
        {
            Diagnostic(
                "Граничное условие",
                "Коллокация: средняя относительная невязка",
                FormatPercent(collocationBoundaryErrorPercent),
                "≤ 1,00%",
                collocationBoundaryErrorPercent <= 1.0),
            Diagnostic(
                "Граничное условие",
                "Галеркин: средняя относительная невязка",
                FormatPercent(galerkinBoundaryErrorPercent),
                "≤ 1,00%",
                galerkinBoundaryErrorPercent <= 1.0),
            Diagnostic(
                "Уравнение Гельмгольца",
                "Коллокация: относительная невязка вне пластины",
                helmholtzResidual.ToString("0.000E+00", RussianCulture),
                "≤ 1,00E-03",
                helmholtzResidual <= 1e-3),
            Diagnostic(
                "Энергетика",
                "Локальная невязка ЗСЭ",
                FormatPercent(energy.LocalBalanceErrorPercent),
                "≤ 2,00%",
                energy.LocalBalanceErrorPercent <= 2.0),
            Diagnostic(
                "Рассеяние",
                "Разность R_scat / T_scat",
                FormatPercent(energy.FarFieldMismatchPercent),
                "≤ 1,0E-06%",
                energy.FarFieldMismatchPercent <= 1e-6),
            Diagnostic(
                "Потоки",
                "Разность сверху / снизу у листа",
                FormatPercent(energy.SheetMismatchPercent),
                "≤ 1,0E-06%",
                energy.SheetMismatchPercent <= 1e-6),
            new DiagnosticRow
            {
                Group = "Методы",
                Check = "max |u_col − u_gal| на пластине",
                Value = comparison.MaximumDifference.ToString("0.000E+00", RussianCulture),
                Tolerance = "справочно",
                Status = "Сравнено"
            },
            new DiagnosticRow
            {
                Group = "Методы",
                Check = "max |a_col − a_gal|",
                Value = comparison.MaximumCoefficientDifference.ToString("0.000E+00", RussianCulture),
                Tolerance = "справочно",
                Status = "Сравнено"
            }
        };
    }

    private static DiagnosticRow Diagnostic(
        string group,
        string check,
        string value,
        string tolerance,
        bool passed)
    {
        return new DiagnosticRow
        {
            Group = group,
            Check = check,
            Value = value,
            Tolerance = tolerance,
            Status = passed ? "В допуске" : "Проверить"
        };
    }

    private static string BuildDiagnosticsSummary(
        EnergySnapshot energy,
        double collocationBoundaryErrorPercent,
        double galerkinBoundaryErrorPercent,
        double helmholtzResidual,
        MethodComparison comparison)
    {
        return string.Format(
            RussianCulture,
            "РАСЧЁТ ДЛЯ ОДНОЙ ПЛАСТИНЫ\n" +
            "Граничное условие, коллокация: {0:0.000000}%\n" +
            "Граничное условие, Галеркин: {1:0.000000}%\n" +
            "Невязка уравнения Гельмгольца: {2:0.000E+00}\n" +
            "Локальная невязка ЗСЭ: {3:0.000000}%\n" +
            "R_scat / T_scat: {4:0.000000} / {5:0.000000}\n" +
            "A_J / A_flux: {6:0.000000} / {7:0.000000}\n" +
            "max |u_col − u_gal|: {8:0.000E+00}\n" +
            "mean |u_col − u_gal|: {9:0.000E+00}\n" +
            "max |a_col − a_gal|: {10:0.000E+00}",
            collocationBoundaryErrorPercent,
            galerkinBoundaryErrorPercent,
            helmholtzResidual,
            energy.LocalBalanceErrorPercent,
            energy.ReflectedScattered,
            energy.ForwardScattered,
            energy.Absorbed,
            energy.FluxAbsorbed,
            comparison.MaximumDifference,
            comparison.MeanDifference,
            comparison.MaximumCoefficientDifference);
    }

    private static double[] SampleRealField(
        DifrOnLenta solver,
        double[] xValues,
        double z,
        CancellationToken cancellationToken)
    {
        double[] values = new double[xValues.Length];
        Parallel.For(0, xValues.Length, new ParallelOptions { CancellationToken = cancellationToken }, i =>
        {
            values[i] = solver.u(xValues[i], z).Re;
        });
        return values;
    }

    private static PlotData CreatePlot(
        string xAxisTitle,
        string yAxisTitle,
        params PlotSeriesData[] series) =>
        CreatePlot(xAxisTitle, yAxisTitle, includeZero: false, series);

    private static PlotData CreatePlot(
        string xAxisTitle,
        string yAxisTitle,
        bool includeZero,
        params PlotSeriesData[] series)
    {
        PlotPointData[] points = series
            .SelectMany(item => item.Points)
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .ToArray();
        if (points.Length == 0)
            return PlotData.Empty(xAxisTitle, yAxisTitle);

        double xMinimum = points.Min(point => point.X);
        double xMaximum = points.Max(point => point.X);
        double yMinimum = points.Min(point => point.Y);
        double yMaximum = points.Max(point => point.Y);
        if (includeZero)
        {
            yMinimum = Math.Min(0, yMinimum);
            yMaximum = Math.Max(0, yMaximum);
        }

        ExpandRange(ref xMinimum, ref xMaximum, minimumPadding: 0.01);
        ExpandRange(ref yMinimum, ref yMaximum, minimumPadding: 1e-6);
        return new PlotData(
            xAxisTitle,
            yAxisTitle,
            series,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);
    }

    private static void ExpandRange(ref double minimum, ref double maximum, double minimumPadding)
    {
        double range = maximum - minimum;
        double padding = Math.Max(Math.Abs(range) * 0.07, minimumPadding);
        if (range <= 1e-15)
            padding = Math.Max(Math.Abs(maximum) * 0.12, minimumPadding);
        minimum -= padding;
        maximum += padding;
    }

    private static IReadOnlyList<PlotPointData> ToPoints(double[] xValues, double[] yValues)
    {
        if (xValues.Length != yValues.Length)
            throw new ArgumentException("Размеры массивов точек графика не совпадают.");

        PlotPointData[] points = new PlotPointData[xValues.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = new PlotPointData(xValues[i], yValues[i]);
        return points;
    }

    private static IReadOnlyList<PlotPointData> OnePoint(double x, double y) =>
        new[] { new PlotPointData(x, y) };

    private static double[] BuildLinearValues(double start, double end, int count)
    {
        double[] values = new double[count];
        if (count == 1)
        {
            values[0] = start;
            return values;
        }

        for (int i = 0; i < count; i++)
            values[i] = start + (end - start) * i / (count - 1.0);
        return values;
    }

    private static int GetAnglePointCount(CalculationParameters parameters) =>
        (int)Math.Floor(
            (parameters.SeriesAngleEndDegrees - parameters.SeriesAngleStartDegrees) /
            parameters.SeriesAngleStepDegrees + 1e-9) + 1;

    private static double Normalize(double value, double incident) => value / incident;
    private static double DegreesToRadians(double angleDegrees) => angleDegrees * Math.PI / 180.0;
    private static string FormatPercent(double value) => value.ToString("0.000000", RussianCulture) + "%";
    private static CultureInfo RussianCulture { get; } = CultureInfo.GetCultureInfo("ru-RU");

    private sealed class MethodComparison
    {
        public required PlotData FieldPlot { get; init; }
        public required PlotData DifferencePlot { get; init; }
        public double MaximumDifference { get; init; }
        public double MeanDifference { get; init; }
        public double MaximumCoefficientDifference { get; init; }
    }

    private sealed class SeriesCalculation
    {
        public static SeriesCalculation Empty { get; } = new()
        {
            SkinEnergyPlot = PlotData.Empty("Толщина δ", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»"),
            AngleEnergyPlot = PlotData.Empty("Угол θ, °", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»"),
            DiagnosticsPlot = PlotData.Empty("Толщина δ", "Отклонение, %", "Запустите расчёт в режиме «Серия»"),
            DiagnosticsCaption = "Серийная диагностика ещё не рассчитана.",
            CheckSummaries = Array.Empty<string>()
        };

        public required PlotData SkinEnergyPlot { get; init; }
        public required PlotData AngleEnergyPlot { get; init; }
        public required PlotData DiagnosticsPlot { get; init; }
        public required string DiagnosticsCaption { get; init; }
        public required IReadOnlyList<string> CheckSummaries { get; init; }
    }
}
