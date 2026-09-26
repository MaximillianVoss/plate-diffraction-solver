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
        string? validationError = GetValidationError(parameters, includeSeries);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(parameters));

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
        ValidateCoefficients(skinGalerkin);

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

        bool includeSkinDepthSeries = includeSeries && parameters.SeriesSkinDepthEnabled;
        bool includeAngleSeries = includeSeries && parameters.SeriesAngleEnabled;
        SeriesCalculation series = includeSkinDepthSeries || includeAngleSeries
            ? BuildSeries(parameters, idealCollocation, skinCollocation, cancellationToken)
            : SeriesCalculation.Empty;

        double collocationBoundaryErrorPercent = skinCollocation.VerifyBoundaryConditions() * 100.0;
        cancellationToken.ThrowIfCancellationRequested();
        double galerkinBoundaryErrorPercent = skinGalerkin.VerifyBoundaryConditions() * 100.0;
        double collocationEdgeErrorPercent = skinCollocation.VerifyBoundaryConditionsNearEdges() * 100.0;
        double galerkinEdgeErrorPercent = skinGalerkin.VerifyBoundaryConditionsNearEdges() * 100.0;
        if (parameters.SkinDepthMicrometers > 0)
            EnsureFinite("граничное условие у краёв", collocationEdgeErrorPercent, galerkinEdgeErrorPercent);
        double helmholtzResidual = skinCollocation.VerifyHelmholtz();
        EnsureFinite("диагностика ГУ и уравнения Гельмгольца",
            collocationBoundaryErrorPercent, galerkinBoundaryErrorPercent, helmholtzResidual);

        IReadOnlyList<CoefficientRow> coefficients = BuildCoefficientRows(
            skinCollocation,
            skinGalerkin);
        IReadOnlyList<DiagnosticRow> diagnostics = BuildDiagnostics(
            selectedEnergy,
            collocationBoundaryErrorPercent,
            galerkinBoundaryErrorPercent,
            helmholtzResidual,
            comparison,
            collocationEdgeErrorPercent,
            galerkinEdgeErrorPercent);

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
            SkinDepthStudyRows = series.SkinDepthRows,
            AngleStudyIdealRows = series.AngleIdealRows,
            AngleStudySkinRows = series.AngleSkinRows,
            IdealFieldMap = idealMap,
            SkinFieldMap = skinMap,
            DiagnosticsSummary = BuildDiagnosticsSummary(
                selectedEnergy,
                collocationBoundaryErrorPercent,
                galerkinBoundaryErrorPercent,
                helmholtzResidual,
                comparison) + (parameters.SkinDepthMicrometers > 0
                    ? $"\nМаксимальная невязка у краёв, коллокация: {collocationEdgeErrorPercent:0.000000}%" +
                      $"\nМаксимальная невязка у краёв, Галеркин: {galerkinEdgeErrorPercent:0.000000}%"
                    : string.Empty),
            SeriesDiagnosticsCaption = series.DiagnosticsCaption,
            SeriesCheckSummaries = series.CheckSummaries,
            BackendName = skinCollocation.LastSolvePerformance?.BackendName ?? "CPU (C#)"
        };
    }

    public static string? GetValidationError(CalculationParameters parameters, bool includeSeries)
    {
        if (!double.IsFinite(parameters.WavelengthMicrometers) || parameters.WavelengthMicrometers <= 0)
            return "Длина волны должна быть положительным числом.";
        if (!double.IsFinite(parameters.IncidenceAngleDegrees) ||
            Math.Abs(Math.Sin(DegreesToRadians(parameters.IncidenceAngleDegrees))) < 1e-8)
        {
            return "Угол должен задавать ненулевой падающий поток через пластину.";
        }
        if (!double.IsFinite(parameters.PlateStart) || !double.IsFinite(parameters.PlateEnd) ||
            parameters.PlateStart >= parameters.PlateEnd)
        {
            return "Для одной пластины должно выполняться α₁ < β₁.";
        }
        if (parameters.HarmonicCount < 2 || parameters.HarmonicCount > 200)
            return "Параметр N должен находиться в диапазоне от 2 до 200.";
        if (!double.IsFinite(parameters.SkinDepthMicrometers) || parameters.SkinDepthMicrometers < 0)
            return "Толщина скин-слоя не может быть отрицательной.";
        if (!double.IsFinite(parameters.OutputLeft) || !double.IsFinite(parameters.OutputRight) ||
            parameters.OutputLeft >= parameters.OutputRight)
        {
            return "Левая граница области вывода должна быть меньше правой.";
        }
        if (!double.IsFinite(parameters.OutputBottom) || !double.IsFinite(parameters.OutputTop) ||
            parameters.OutputBottom >= parameters.OutputTop)
        {
            return "Нижняя граница области вывода должна быть меньше верхней.";
        }
        if (!double.IsFinite(parameters.PlateEnd - parameters.PlateStart) ||
            !double.IsFinite(parameters.OutputRight - parameters.OutputLeft) ||
            !double.IsFinite(parameters.OutputTop - parameters.OutputBottom))
            return "Диапазон координат слишком велик для численного расчёта. Уменьшите его.";

        if (!includeSeries)
            return null;

        bool skinDepthSeries = parameters.SeriesSkinDepthEnabled;
        bool angleSeries = parameters.SeriesAngleEnabled;
        if (!skinDepthSeries && !angleSeries)
            return "Отметьте хотя бы одну серию: по толщине δ или по углу θ.";

        if (skinDepthSeries)
        {
            if (!double.IsFinite(parameters.SeriesSkinDepthStart) ||
                !double.IsFinite(parameters.SeriesSkinDepthEnd) ||
                parameters.SeriesSkinDepthStart < 0 ||
                parameters.SeriesSkinDepthStart > parameters.SeriesSkinDepthEnd)
            {
                return "Некорректный диапазон толщины скин-слоя.";
            }
            if (parameters.SeriesPointCount < 2 || parameters.SeriesPointCount > 101)
                return "Число точек серии по δ должно быть от 2 до 101.";
        }

        if (angleSeries)
        {
            if (!double.IsFinite(parameters.SeriesAngleStartDegrees) ||
                !double.IsFinite(parameters.SeriesAngleEndDegrees) ||
                !double.IsFinite(parameters.SeriesAngleStepDegrees) ||
                parameters.SeriesAngleStepDegrees <= 0 ||
                parameters.SeriesAngleStartDegrees > parameters.SeriesAngleEndDegrees)
            {
                return "Некорректный диапазон углов серии.";
            }

            double[] angles = BuildAngleValues(parameters);
            if (angles.Length == 0)
                return "Серия по углу должна содержать от 1 до 181 точки с различными углами. Увеличьте шаг или уменьшите диапазон.";

            foreach (double angle in angles)
            {
                if (Math.Abs(Math.Sin(DegreesToRadians(angle))) < 1e-8)
                    return "Диапазон углов содержит точку с нулевым падающим потоком.";
            }
        }
        return null;
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

        ValidateCoefficients(solver);
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

        var snapshot = new EnergySnapshot(
            skinDepth,
            Normalize(farField.ReflectedScattered, incident),
            Normalize(farField.TransmittedScattered, incident),
            Normalize(energy.Absorbed, incident),
            Normalize(sheetFlux.AboveOutgoing, incident),
            Normalize(sheetFlux.BelowOutgoing, incident),
            Normalize(energy.FluxAbsorbed, incident),
            Math.Abs(energy.LocalBalanceResidual) / incident * 100.0,
            extinction: solver.CalculateExtinctionEnergy(plateSamples) / incident,
            crossSectionScale: incident / (Math.PI / solver.lambda));
        EnsureFinite("энергия и потоки", snapshot.SkinDepth,
            snapshot.ReflectedScattered, snapshot.ForwardScattered, snapshot.Absorbed,
            snapshot.SheetAbove, snapshot.SheetBelow, snapshot.FluxAbsorbed,
            snapshot.LocalBalanceErrorPercent, snapshot.FarFieldMismatchPercent, snapshot.SheetMismatchPercent,
            snapshot.Extinction, snapshot.OpticalBalanceErrorPercent,
            snapshot.ReflectedCrossSection, snapshot.ForwardCrossSection, snapshot.AbsorbedCrossSection);
        return snapshot;
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
            EnsureFinite("сравнение полей", collocationField.Re, collocationField.Im,
                galerkinField.Re, galerkinField.Im);

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
        EnsureFinite("разность методов", maxDifference, meanDifference, maxCoefficientDifference);

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
        if (scaleMaximum <= 0)
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
                (parameters.OutputTop - parameters.OutputBottom) * (row / (FieldMapHeight - 1.0));
            for (int column = 0; column < FieldMapWidth; column++)
            {
                double x = parameters.OutputLeft +
                    (parameters.OutputRight - parameters.OutputLeft) * (column / (FieldMapWidth - 1.0));
                double value = Compl.Abs(solver.u(x, y));
                EnsureFinite("карта поля", value);
                values[row * FieldMapWidth + column] = value;
            }
        });
        return values;
    }

    private static SeriesCalculation BuildSeries(
        CalculationParameters parameters,
        DifrOnLenta idealSolver,
        DifrOnLenta selectedSolver,
        CancellationToken cancellationToken)
    {
        bool skinDepthSeries = parameters.SeriesSkinDepthEnabled;
        bool angleSeries = parameters.SeriesAngleEnabled;

        List<EnergySnapshot> skinSnapshots = new(skinDepthSeries ? parameters.SeriesPointCount : 0);
        double[] skinDepths = skinDepthSeries
            ? BuildLinearValues(
                parameters.SeriesSkinDepthStart,
                parameters.SeriesSkinDepthEnd,
                parameters.SeriesPointCount)
            : Array.Empty<double>();

        foreach (double skinDepth in skinDepths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool useSelected = skinDepth == parameters.SkinDepthMicrometers;
            DifrOnLenta solver = useSelected
                ? selectedSolver
                : SolveCollocation(parameters, DegreesToRadians(parameters.IncidenceAngleDegrees), skinDepth, cancellationToken);
            skinSnapshots.Add(CalculateEnergySnapshot(
                solver,
                skinDepth,
                angleSamples: 180,
                plateSamples: 160));
        }

        double[] angles = angleSeries ? BuildAngleValues(parameters) : Array.Empty<double>();

        // Исследование изменения угла считается отдельно для идеального проводника (δ = 0)
        // и для выбранной толщины скин-слоя — это две независимые таблицы.
        List<EnergySnapshot> angleIdealSnapshots = new(angles.Length);
        List<EnergySnapshot> angleSkinSnapshots = new(angles.Length);
        foreach (double angle in angles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isBaseAngle = angle == parameters.IncidenceAngleDegrees;
            double angleRadians = DegreesToRadians(angle);

            DifrOnLenta idealCase = isBaseAngle
                ? idealSolver
                : SolveCollocation(parameters, angleRadians, 0, cancellationToken);
            angleIdealSnapshots.Add(CalculateEnergySnapshot(
                idealCase,
                0,
                angleSamples: 180,
                plateSamples: 160));

            DifrOnLenta skinCase = isBaseAngle
                ? selectedSolver
                : SolveCollocation(parameters, angleRadians, parameters.SkinDepthMicrometers, cancellationToken);
            angleSkinSnapshots.Add(CalculateEnergySnapshot(
                skinCase,
                parameters.SkinDepthMicrometers,
                angleSamples: 180,
                plateSamples: 160));
        }

        PlotData skinPlot = skinDepthSeries
            ? CreateEnergyPlot("Толщина δ", skinDepths, skinSnapshots)
            : PlotData.Empty("Толщина δ", "Сечение, мкм", "Серия по толщине δ отключена.");
        PlotData anglePlot = angleSeries
            ? CreateEnergyPlot("Угол θ, °", angles, angleSkinSnapshots)
            : PlotData.Empty("Угол θ, °", "Сечение, мкм", "Серия по углу θ отключена.");
        PlotData diagnosticsPlot = skinDepthSeries
            ? CreatePlot(
                "Толщина δ",
                "Отклонение, %",
                includeZero: true,
                new PlotSeriesData(
                    "Оптическая теорема",
                    "#EA580C",
                    ToPoints(skinDepths, skinSnapshots.Select(item => item.OpticalBalanceErrorPercent).ToArray())),
                new PlotSeriesData(
                    "|R_scat − T_scat|",
                    "#16A34A",
                    ToPoints(skinDepths, skinSnapshots.Select(item => item.FarFieldMismatchPercent).ToArray()),
                    isDashed: true))
            : PlotData.Empty("Толщина δ", "Отклонение, %", "Серия по толщине δ отключена.");

        string caption = skinDepthSeries
            ? string.Format(
                RussianCulture,
                "Максимум по серии: оптическая теорема {0:0.000000}%; |R_scat − T_scat| / I_plate {1:0.000000}%.",
                skinSnapshots.Max(item => item.OpticalBalanceErrorPercent),
                skinSnapshots.Max(item => item.FarFieldMismatchPercent))
            : "Серия по толщине δ не выполнялась; диагностика построена не будет.";

        var checkSummaries = new List<string>();
        if (skinDepthSeries)
        {
            checkSummaries.Add(string.Format(
                RussianCulture,
                "Серия δ: {0} точек; R_scat {1:0.000000}…{2:0.000000}; A_J {3:0.000000}…{4:0.000000}",
                skinSnapshots.Count,
                skinSnapshots.Min(item => item.ReflectedScattered),
                skinSnapshots.Max(item => item.ReflectedScattered),
                skinSnapshots.Min(item => item.Absorbed),
                skinSnapshots.Max(item => item.Absorbed)));
        }
        if (angleSeries)
        {
            checkSummaries.Add(string.Format(
                RussianCulture,
                "Серия θ (идеальный проводник): {0} точек; R_scat {1:0.000000}…{2:0.000000}",
                angleIdealSnapshots.Count,
                angleIdealSnapshots.Min(item => item.ReflectedScattered),
                angleIdealSnapshots.Max(item => item.ReflectedScattered)));
            checkSummaries.Add(string.Format(
                RussianCulture,
                "Серия θ (скин-слой δ={0:0.######}): {1} точек; R_scat {2:0.000000}…{3:0.000000}",
                parameters.SkinDepthMicrometers,
                angleSkinSnapshots.Count,
                angleSkinSnapshots.Min(item => item.ReflectedScattered),
                angleSkinSnapshots.Max(item => item.ReflectedScattered)));
        }
        checkSummaries.Add(caption);

        return new SeriesCalculation
        {
            SkinEnergyPlot = skinPlot,
            AngleEnergyPlot = anglePlot,
            DiagnosticsPlot = diagnosticsPlot,
            DiagnosticsCaption = caption,
            SkinDepthRows = BuildStudyRows(skinDepths, skinSnapshots),
            AngleIdealRows = BuildStudyRows(angles, angleIdealSnapshots),
            AngleSkinRows = BuildStudyRows(angles, angleSkinSnapshots),
            CheckSummaries = checkSummaries
        };
    }

    private static IReadOnlyList<EnergyStudyRow> BuildStudyRows(
        double[] arguments,
        IReadOnlyList<EnergySnapshot> snapshots)
    {
        if (arguments.Length != snapshots.Count)
            throw new ArgumentException("Число аргументов серии не совпадает с числом снимков энергии.");

        var rows = new List<EnergyStudyRow>(snapshots.Count);
        for (int i = 0; i < snapshots.Count; i++)
            rows.Add(new EnergyStudyRow(arguments[i], snapshots[i]));
        return rows;
    }

    private static PlotData BuildCurrentEnergyPlot(EnergySnapshot energy)
    {
        double x = energy.SkinDepth;
        return CreatePlot(
            "Толщина δ",
            "Сечение, мкм",
            includeZero: true,
            new PlotSeriesData("C_back", "#2563EB", OnePoint(x, energy.ReflectedCrossSection), showMarkers: true),
            new PlotSeriesData("C_forward", "#16A34A", OnePoint(x, energy.ForwardCrossSection), isDashed: true, showMarkers: true),
            new PlotSeriesData("C_abs", "#EA580C", OnePoint(x, energy.AbsorbedCrossSection), showMarkers: true),
            new PlotSeriesData("C_ext", "#374151", OnePoint(x, energy.ExtinctionCrossSection), showMarkers: true));
    }

    private static PlotData CreateEnergyPlot(
        string xAxisTitle,
        double[] xValues,
        IReadOnlyList<EnergySnapshot> snapshots)
    {
        bool showMarkers = xValues.Length == 1;
        return CreatePlot(
            xAxisTitle,
            "Сечение, мкм",
            includeZero: true,
            new PlotSeriesData(
                "C_back",
                "#2563EB",
                ToPoints(xValues, snapshots.Select(item => item.ReflectedCrossSection).ToArray()),
                showMarkers: showMarkers),
            new PlotSeriesData(
                "C_forward",
                "#16A34A",
                ToPoints(xValues, snapshots.Select(item => item.ForwardCrossSection).ToArray()),
                isDashed: true, showMarkers: showMarkers),
            new PlotSeriesData(
                "C_abs",
                "#EA580C",
                ToPoints(xValues, snapshots.Select(item => item.AbsorbedCrossSection).ToArray()),
                showMarkers: showMarkers),
            new PlotSeriesData(
                "C_ext",
                "#374151",
                ToPoints(xValues, snapshots.Select(item => item.ExtinctionCrossSection).ToArray()),
                showMarkers: showMarkers));
    }

    private static IReadOnlyList<DiagnosticRow> BuildDiagnostics(
        EnergySnapshot energy,
        double collocationBoundaryErrorPercent,
        double galerkinBoundaryErrorPercent,
        double helmholtzResidual,
        MethodComparison comparison,
        double collocationEdgeErrorPercent,
        double galerkinEdgeErrorPercent)
    {
        var rows = new List<DiagnosticRow>
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
                "Энергетика",
                "Оптическая теорема: |P_ext − P_scat − P_abs| / P_ext",
                FormatPercent(energy.OpticalBalanceErrorPercent),
                "≤ 2,00%",
                energy.HasGlobalBalance && energy.OpticalBalanceErrorPercent <= 2.0),
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
        if (energy.SkinDepth > 0)
        {
            rows.Add(Diagnostic("Граничное условие", "Коллокация: максимум у краёв и на концах",
                FormatPercent(collocationEdgeErrorPercent), "≤ 1,00%", collocationEdgeErrorPercent <= 1.0));
            rows.Add(Diagnostic("Граничное условие", "Галеркин: максимум у краёв и на концах",
                FormatPercent(galerkinEdgeErrorPercent), "≤ 1,00%", galerkinEdgeErrorPercent <= 1.0));
        }
        return rows;
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
            "max |a_col − a_gal|: {10:0.000E+00}\n" +
            "Экстинкция P_ext / I_plate (независимый интеграл): {11:0.000000}\n" +
            "Оптическая теорема: {12:0.000000}%\n" +
            "R_scat и T_scat нормированы на геометрическую проекцию, это не коэффициенты R и T.",
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
            comparison.MaximumCoefficientDifference,
            energy.Extinction,
            energy.OpticalBalanceErrorPercent);
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
            Compl field = solver.u(xValues[i], z);
            EnsureFinite("сечение поля", field.Re, field.Im);
            values[i] = field.Re;
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
            .ToArray();
        foreach (PlotPointData point in points)
            EnsureFinite("точки графика", point.X, point.Y);
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
        EnsureFinite("диапазон оси графика", minimum, maximum, range);
        double padding = Math.Max(Math.Abs(range) * 0.07, minimumPadding);
        if (range <= 1e-15)
            padding = Math.Max(Math.Abs(maximum) * 0.12, minimumPadding);
        double paddedMinimum = Math.Max(-double.MaxValue, minimum - padding);
        double paddedMaximum = Math.Min(double.MaxValue, maximum + padding);
        // Padding is cosmetic; keep the original finite axis if widening it would overflow.
        if (double.IsFinite(paddedMaximum - paddedMinimum))
        {
            minimum = paddedMinimum;
            maximum = paddedMaximum;
        }
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
            values[i] = start + (end - start) * (i / (count - 1.0));
        return values;
    }

    public static int GetAnglePointCount(CalculationParameters parameters) => BuildAngleValues(parameters).Length;

    private static double[] BuildAngleValues(CalculationParameters parameters)
    {
        if (!double.IsFinite(parameters.SeriesAngleStartDegrees) ||
            !double.IsFinite(parameters.SeriesAngleEndDegrees) ||
            !double.IsFinite(parameters.SeriesAngleStepDegrees) ||
            parameters.SeriesAngleStepDegrees <= 0 ||
            parameters.SeriesAngleStartDegrees > parameters.SeriesAngleEndDegrees)
            return Array.Empty<double>();

        var angles = new List<double>();
        double end = parameters.SeriesAngleEndDegrees;
        for (int i = 0; i <= 181; i++)
        {
            double angle = Math.FusedMultiplyAdd(i, parameters.SeriesAngleStepDegrees, parameters.SeriesAngleStartDegrees);
            if (!double.IsFinite(angle))
                break;
            if (angle > end)
            {
                // Permit one rounding step at an inclusive endpoint, without emitting a value beyond it.
                if (angle == Math.BitIncrement(end))
                    angle = end;
                else
                    break;
            }
            if (angles.Count == 181 || (angles.Count > 0 && angle <= angles[^1]))
                return Array.Empty<double>();
            angles.Add(angle);
            if (angle == end)
                break;
        }
        return angles.ToArray();
    }

    private static double Normalize(double value, double incident) => value / incident;
    private static void ValidateCoefficients(DifrOnLenta solver)
    {
        foreach (Compl coefficient in solver.y)
            EnsureFinite("коэффициенты решения", coefficient.Re, coefficient.Im);
    }

    private static void EnsureFinite(string quantity, params double[] values)
    {
        if (values.Any(value => !double.IsFinite(value)))
            throw new InvalidOperationException(
                $"Расчёт вернул нечисловое значение или бесконечность ({quantity}). " +
                "Проверьте параметры: возможны переполнение или неустойчивость численного решения.");
    }

    private static double DegreesToRadians(double angleDegrees) => angleDegrees * (Math.PI / 180.0);
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
            SkinEnergyPlot = PlotData.Empty("Толщина δ", "Сечение, мкм", "Запустите расчёт в режиме «Серия»"),
            AngleEnergyPlot = PlotData.Empty("Угол θ, °", "Сечение, мкм", "Запустите расчёт в режиме «Серия»"),
            DiagnosticsPlot = PlotData.Empty("Толщина δ", "Отклонение, %", "Запустите расчёт в режиме «Серия»"),
            DiagnosticsCaption = "Серийная диагностика ещё не рассчитана.",
            SkinDepthRows = Array.Empty<EnergyStudyRow>(),
            AngleIdealRows = Array.Empty<EnergyStudyRow>(),
            AngleSkinRows = Array.Empty<EnergyStudyRow>(),
            CheckSummaries = Array.Empty<string>()
        };

        public required PlotData SkinEnergyPlot { get; init; }
        public required PlotData AngleEnergyPlot { get; init; }
        public required PlotData DiagnosticsPlot { get; init; }
        public required string DiagnosticsCaption { get; init; }
        public required IReadOnlyList<EnergyStudyRow> SkinDepthRows { get; init; }
        public required IReadOnlyList<EnergyStudyRow> AngleIdealRows { get; init; }
        public required IReadOnlyList<EnergyStudyRow> AngleSkinRows { get; init; }
        public required IReadOnlyList<string> CheckSummaries { get; init; }
    }
}
