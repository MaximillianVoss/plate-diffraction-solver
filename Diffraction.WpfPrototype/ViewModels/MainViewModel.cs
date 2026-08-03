using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Diffraction.WpfPrototype.Infrastructure;
using Diffraction.WpfPrototype.Models;

namespace Diffraction.WpfPrototype.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _currentSection = "Calculations";
    private string _historySearch = string.Empty;
    private string _fluxSearch = string.Empty;
    private string _fluxCategory = "Все показатели";
    private string _coefficientSearch = string.Empty;
    private CalculationRun? _selectedRun;
    private bool _isBusy;
    private bool _isSingleMode = true;
    private string _selectedBackend = "Авто";
    private string _statusText = "Расчёт завершён";
    private string _statusDetail = "CPU  •  0,84 с";
    private double _progress;
    private CancellationTokenSource? _calculationCancellation;
    private bool _isRestoringParameters;
    private bool _parametersModified;

    public MainViewModel()
    {
        Parameters = CalculationParameters.CreateDefault();
        SeriesCheckSummaries = new ObservableCollection<string>();
        Parameters.PropertyChanged += (_, _) => HandleParameterChanged();

        Runs = new ObservableCollection<CalculationRun>(CreateRuns());
        FluxRows = new ObservableCollection<FluxRow>(CreateFluxRows());
        Coefficients = new ObservableCollection<CoefficientRow>(CreateCoefficients());
        Diagnostics = new ObservableCollection<DiagnosticRow>(CreateDiagnostics());
        JournalEntries = new ObservableCollection<string>
        {
            "[14:32:01] Инициализация параметров...",
            "[14:32:01] Построение сетки гармоник... (N = 30)",
            "[14:32:01] Проверка сходимости и баланса энергии...",
            "[14:32:01] R_scat и T_scat совпадают в пределах машинной точности.",
            "[14:32:01] Локальная невязка ЗСЭ: 0,047% — в допуске (2%).",
            "[14:32:01] Расчёт завершён успешно."
        };

        RunsView = CollectionViewSource.GetDefaultView(Runs);
        RunsView.Filter = FilterRun;
        FluxView = CollectionViewSource.GetDefaultView(FluxRows);
        FluxView.Filter = FilterFlux;
        CoefficientsView = CollectionViewSource.GetDefaultView(Coefficients);
        CoefficientsView.Filter = FilterCoefficient;

        SelectedRun = Runs.FirstOrDefault();

        NavigateCommand = new RelayCommand(Navigate);
        RunCommand = new AsyncRelayCommand(RunCalculationAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CancelCalculation(), _ => IsBusy);
        NewRunCommand = new RelayCommand(_ => PrepareNewRun(), _ => !IsBusy);
        ExportCommand = new RelayCommand(_ => RegisterAction("Подготовлен пакет экспорта: CSV, PNG и отчёт."));
        CompareCommand = new RelayCommand(_ => RegisterAction("Открыто сравнение с предыдущим расчётом."));
        ClearJournalCommand = new RelayCommand(_ => JournalEntries.Clear());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CalculationRun> Runs { get; }
    public ObservableCollection<FluxRow> FluxRows { get; }
    public ObservableCollection<CoefficientRow> Coefficients { get; }
    public ObservableCollection<DiagnosticRow> Diagnostics { get; }
    public ObservableCollection<string> JournalEntries { get; }
    public ObservableCollection<string> SeriesCheckSummaries { get; }

    public ICollectionView RunsView { get; }
    public ICollectionView FluxView { get; }
    public ICollectionView CoefficientsView { get; }

    public EnergyDemoSnapshot Energy { get; } = ValidatedEnergyDemo.Selected;
    public CalculationParameters Parameters { get; }

    public IReadOnlyList<string> Backends { get; } = new[] { "Авто", "CPU", "CUDA" };
    public IReadOnlyList<string> Modes { get; } = new[] { "Один расчёт", "Серия" };
    public IReadOnlyList<string> FluxCategories { get; } = new[] { "Все показатели", "Рассеяние", "Потоки", "Баланс" };

    public ICommand NavigateCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand NewRunCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ClearJournalCommand { get; }

    public string CurrentSection
    {
        get => _currentSection;
        private set => SetProperty(ref _currentSection, value);
    }

    public string HistorySearch
    {
        get => _historySearch;
        set
        {
            if (SetProperty(ref _historySearch, value))
                RunsView.Refresh();
        }
    }

    public string FluxSearch
    {
        get => _fluxSearch;
        set
        {
            if (SetProperty(ref _fluxSearch, value))
                FluxView.Refresh();
        }
    }

    public string FluxCategory
    {
        get => _fluxCategory;
        set
        {
            if (SetProperty(ref _fluxCategory, value))
                FluxView.Refresh();
        }
    }

    public string CoefficientSearch
    {
        get => _coefficientSearch;
        set
        {
            if (SetProperty(ref _coefficientSearch, value))
                CoefficientsView.Refresh();
        }
    }

    public CalculationRun? SelectedRun
    {
        get => _selectedRun;
        set
        {
            if (!SetProperty(ref _selectedRun, value))
                return;

            if (value is not null)
                RestoreRunParameters(value);
            else
                NotifyParameterContextChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsSingleMode
    {
        get => _isSingleMode;
        set
        {
            if (!SetProperty(ref _isSingleMode, value))
                return;
            OnPropertyChanged(nameof(IsSeriesMode));
            OnPropertyChanged(nameof(SelectedMode));
            MarkParameterContextModified();

            if (!_isRestoringParameters)
                CurrentSection = value ? "Calculations" : "Series";
        }
    }

    public bool IsSeriesMode
    {
        get => !IsSingleMode;
        set
        {
            if (value)
                IsSingleMode = false;
        }
    }

    public string SelectedMode
    {
        get => IsSingleMode ? Modes[0] : Modes[1];
        set
        {
            if (value == Modes[0])
                IsSingleMode = true;
            else if (value == Modes[1])
                IsSingleMode = false;
        }
    }

    public string SelectedBackend
    {
        get => _selectedBackend;
        set
        {
            if (SetProperty(ref _selectedBackend, value))
                MarkParameterContextModified();
        }
    }

    public string ContextTitle
    {
        get
        {
            if (SelectedRun is null)
                return IsSeriesMode ? "Новая серия — тонкая пластина" : "Новый расчёт — тонкая пластина";

            return _parametersModified
                ? $"{SelectedRun.Title} — параметры изменены"
                : SelectedRun.Title;
        }
    }

    public string ContextSummary => BuildContextSummary();

    public string SeriesPointTitle =>
        $"Выбранная точка: δ = {Format(Parameters.SkinDepthMicrometers, "0.000")}";

    public string SeriesAngleCaption =>
        $"Контрольная серия: δ={Format(Parameters.SkinDepthMicrometers, "0.000")}, N={Parameters.HarmonicCount}. " +
        $"Углы {Format(Parameters.SeriesAngleStartDegrees, "0.#")}…{Format(Parameters.SeriesAngleEndDegrees, "0.#")}° с шагом {Format(Parameters.SeriesAngleStepDegrees, "0.#")}°.";

    public string SeriesEstimateText =>
        $"{Parameters.SeriesPointCount} точек по δ  •  {GetAnglePointCount()} по θ";

    public string GeometryRegionSummary =>
        $"Пластина [{Format(Parameters.PlateStart, "0.000")}; {Format(Parameters.PlateEnd, "0.000")}]  •  " +
        $"x [{Format(Parameters.OutputLeft, "0.00")}; {Format(Parameters.OutputRight, "0.00")}], " +
        $"y [{Format(Parameters.OutputBottom, "0.00")}; {Format(Parameters.OutputTop, "0.00")}]";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    private void Navigate(object? parameter)
    {
        if (parameter is not string section)
            return;

        CurrentSection = section;
        if (section == "Calculations")
            IsSingleMode = true;
        else if (section == "Series")
            IsSingleMode = false;
    }

    public void OpenRun(CalculationRun run)
    {
        if (ReferenceEquals(SelectedRun, run))
            RestoreRunParameters(run);
        else
            SelectedRun = run;

        CurrentSection = run.IsSeries ? "Series" : "Calculations";
        StatusText = $"Открыт{(run.IsSeries ? "а серия" : " расчёт")} #{run.RunNumber:000}";
        StatusDetail = $"{run.DateLabel}  •  {run.Status}";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Параметры {(run.IsSeries ? "серии" : "расчёта")} #{run.RunNumber:000} восстановлены из истории.");
    }

    private async Task RunCalculationAsync()
    {
        _calculationCancellation?.Dispose();
        _calculationCancellation = new CancellationTokenSource();
        CancellationToken token = _calculationCancellation.Token;
        IsBusy = true;
        Progress = 0;

        var phases = new[]
        {
            ("Проверка параметров...", 12d),
            ("Решение без скин-слоя на CPU...", 34d),
            ("Решение со скин-слоем на CPU...", 58d),
            ("Расчёт энергетического баланса...", 76d),
            ("Обновление таблиц и графиков...", 92d),
            ("Диагностика завершена.", 100d)
        };

        try
        {
            foreach ((string phase, double progress) in phases)
            {
                token.ThrowIfCancellationRequested();
                StatusText = phase;
                StatusDetail = $"{SelectedBackend}  •  {progress:0}%";
                JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] {phase}");
                Progress = progress;
                await Task.Delay(330, token);
            }

            var completedRun = new CalculationRun
            {
                RunNumber = Runs.Count == 0 ? 1 : Runs.Max(run => run.RunNumber) + 1,
                DateLabel = $"Сегодня {DateTime.Now:HH:mm}",
                Parameters = Parameters.Clone(),
                IsSeries = IsSeriesMode || CurrentSection == "Series",
                Backend = SelectedBackend == "Авто" ? "CPU" : SelectedBackend,
                Status = "В допуске",
                StatusKind = "Success"
            };
            Runs.Insert(0, completedRun);
            SelectedRun = completedRun;
            RunsView.Refresh();
            StatusText = "Расчёт завершён";
            StatusDetail = $"{completedRun.Backend}  •  0,84 с";
            JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Расчёт завершён успешно.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Расчёт отменён";
            StatusDetail = "Результаты не изменены";
            JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Расчёт отменён пользователем.");
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    private void CancelCalculation() => _calculationCancellation?.Cancel();

    private void PrepareNewRun()
    {
        _isRestoringParameters = true;
        try
        {
            Parameters.CopyFrom(CalculationParameters.CreateDefault());
            SelectedBackend = "Авто";
            IsSingleMode = true;
            SelectedRun = null;
            _parametersModified = false;
        }
        finally
        {
            _isRestoringParameters = false;
        }

        CurrentSection = "Calculations";
        RefreshSeriesCheckSummaries();
        NotifyParameterContextChanged();
        StatusText = "Новый расчёт";
        StatusDetail = "Проверьте параметры и нажмите «Рассчитать»";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Подготовлен новый набор параметров.");
    }

    private void RegisterAction(string message)
    {
        StatusText = message;
        StatusDetail = "Черновой интерфейс";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private bool FilterRun(object item)
    {
        if (item is not CalculationRun run || string.IsNullOrWhiteSpace(HistorySearch))
            return true;

        string haystack = $"{run.RunNumber} {run.DateLabel} {run.SkinDepth} {run.N} {run.Backend} {run.Status}";
        return haystack.Contains(HistorySearch, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool FilterFlux(object item)
    {
        if (item is not FluxRow row)
            return false;

        bool categoryMatches = FluxCategory == "Все показатели" || row.Category == FluxCategory;
        bool textMatches = string.IsNullOrWhiteSpace(FluxSearch) ||
            row.Metric.Contains(FluxSearch, StringComparison.CurrentCultureIgnoreCase) ||
            row.Status.Contains(FluxSearch, StringComparison.CurrentCultureIgnoreCase);
        return categoryMatches && textMatches;
    }

    private bool FilterCoefficient(object item)
    {
        if (item is not CoefficientRow row || string.IsNullOrWhiteSpace(CoefficientSearch))
            return true;

        return row.Index.ToString().Contains(CoefficientSearch, StringComparison.Ordinal) ||
               row.CollocationRe.ToString("G5").Contains(CoefficientSearch, StringComparison.CurrentCultureIgnoreCase) ||
               row.GalerkinRe.ToString("G5").Contains(CoefficientSearch, StringComparison.CurrentCultureIgnoreCase);
    }

    private static IEnumerable<CalculationRun> CreateRuns()
    {
        return new[]
        {
            new CalculationRun
            {
                RunNumber = 24,
                DateLabel = "Сегодня 14:32",
                Parameters = CreateParameters(0.010, 30),
                IsSeries = false,
                Backend = "CPU",
                Status = "В допуске",
                StatusKind = "Success"
            },
            new CalculationRun
            {
                RunNumber = 23,
                DateLabel = "Сегодня 11:18",
                Parameters = CreateParameters(0.010, 20, wavelength: 1.200, angle: 30.0),
                IsSeries = false,
                Backend = "CPU",
                Status = "В допуске",
                StatusKind = "Success"
            },
            new CalculationRun
            {
                RunNumber = 22,
                DateLabel = "Вчера 16:47",
                Parameters = CreateParameters(
                    0.020,
                    30,
                    wavelength: 0.800,
                    angle: 30.0,
                    plateStart: -2.000,
                    plateEnd: -0.750,
                    outputLeft: -3.0,
                    outputRight: 3.0,
                    outputBottom: -4.0,
                    outputTop: 4.0,
                    seriesStart: 0.005,
                    seriesEnd: 0.080,
                    seriesPoints: 16,
                    angleStart: 15.0,
                    angleEnd: 75.0,
                    angleStep: 5.0),
                IsSeries = true,
                Backend = "CPU",
                Status = "Предупреждение",
                StatusKind = "Warning"
            },
            new CalculationRun
            {
                RunNumber = 21,
                DateLabel = "Вчера 10:05",
                Parameters = CreateParameters(0.010, 10, angle: 60.0),
                IsSeries = false,
                Backend = "CPU",
                Status = "В допуске",
                StatusKind = "Success"
            },
            new CalculationRun
            {
                RunNumber = 20,
                DateLabel = "12.05.2026 09:22",
                Parameters = CreateParameters(0.005, 30, seriesEnd: 0.050, seriesPoints: 11, angleStart: 20.0, angleEnd: 80.0, angleStep: 5.0),
                IsSeries = true,
                Backend = "CPU",
                Status = "В допуске",
                StatusKind = "Success"
            },
            new CalculationRun
            {
                RunNumber = 19,
                DateLabel = "11.05.2026 18:31",
                Parameters = CreateParameters(0.010, 30),
                IsSeries = false,
                Backend = "CUDA",
                Status = "Аномалия",
                StatusKind = "Error"
            },
            new CalculationRun
            {
                RunNumber = 18,
                DateLabel = "10.05.2026 15:09",
                Parameters = CreateParameters(0.015, 40, seriesStart: 0.005, seriesEnd: 0.125, seriesPoints: 25),
                IsSeries = true,
                Backend = "CPU",
                Status = "В допуске",
                StatusKind = "Success"
            }
        };
    }

    private static CalculationParameters CreateParameters(
        double skinDepth,
        int harmonicCount,
        double wavelength = 1.0,
        double angle = 45.0,
        double plateStart = -1.5,
        double plateEnd = -0.5,
        double outputLeft = -2.0,
        double outputRight = 2.0,
        double outputBottom = -3.0,
        double outputTop = 3.0,
        double seriesStart = 0.0,
        double seriesEnd = 0.1,
        int seriesPoints = 21,
        double angleStart = 10.0,
        double angleEnd = 90.0,
        double angleStep = 2.0)
    {
        return new CalculationParameters
        {
            WavelengthMicrometers = wavelength,
            IncidenceAngleDegrees = angle,
            PlateStart = plateStart,
            PlateEnd = plateEnd,
            HarmonicCount = harmonicCount,
            SkinDepthMicrometers = skinDepth,
            OutputLeft = outputLeft,
            OutputRight = outputRight,
            OutputBottom = outputBottom,
            OutputTop = outputTop,
            SeriesSkinDepthStart = seriesStart,
            SeriesSkinDepthEnd = seriesEnd,
            SeriesPointCount = seriesPoints,
            SeriesAngleStartDegrees = angleStart,
            SeriesAngleEndDegrees = angleEnd,
            SeriesAngleStepDegrees = angleStep
        };
    }

    private static IEnumerable<FluxRow> CreateFluxRows()
    {
        EnergyDemoSnapshot energy = ValidatedEnergyDemo.Selected;
        return new[]
        {
            new FluxRow
            {
                Category = "Рассеяние",
                Metric = "R_scat / T_scat",
                Top = energy.ReflectedScattered,
                Bottom = energy.ForwardScattered,
                DifferencePercent = energy.FarFieldMismatchPercent,
                TolerancePercent = 0.000001,
                Status = "Совпадают"
            },
            new FluxRow
            {
                Category = "Потоки",
                Metric = "У листа: сверху / снизу",
                Top = energy.SheetAbove,
                Bottom = energy.SheetBelow,
                DifferencePercent = energy.SheetMismatchPercent,
                TolerancePercent = 0.000001,
                Status = "Совпадают"
            },
            new FluxRow
            {
                Category = "Баланс",
                Metric = "A_flux / A_J",
                Top = energy.FluxAbsorbed,
                Bottom = energy.Absorbed,
                DifferencePercent = energy.LocalBalanceErrorPercent,
                TolerancePercent = 2.0,
                Status = "В допуске"
            }
        };
    }

    private static IEnumerable<CoefficientRow> CreateCoefficients()
    {
        for (int i = 0; i < 60; i++)
        {
            double decay = Math.Exp(-i / 7.5);
            double phase = i * 0.57;
            double collRe = decay * Math.Cos(phase);
            double collIm = decay * Math.Sin(phase);
            yield return new CoefficientRow
            {
                Index = i + 1,
                CollocationRe = collRe,
                CollocationIm = collIm,
                GalerkinRe = collRe * (1.0 - 0.004 * Math.Sin(i * 0.3)),
                GalerkinIm = collIm * (1.0 + 0.004 * Math.Cos(i * 0.4))
            };
        }
    }

    private static IEnumerable<DiagnosticRow> CreateDiagnostics()
    {
        return new[]
        {
            new DiagnosticRow { Group = "Граничное условие", Check = "Невязка на пластине", Value = "0,00%", Tolerance = "≤ 1,00%", Status = "В допуске" },
            new DiagnosticRow { Group = "Уравнение Гельмгольца", Check = "Относительная невязка", Value = "1,65E-04", Tolerance = "≤ 1,00E-03", Status = "В допуске" },
            new DiagnosticRow { Group = "Энергетика", Check = "Локальная невязка ЗСЭ", Value = "0,047%", Tolerance = "≤ 2,00%", Status = "В допуске" },
            new DiagnosticRow { Group = "Рассеяние", Check = "Разность R_scat/T_scat", Value = "< 1,0E-12%", Tolerance = "≤ 1,0E-06%", Status = "Совпадают" },
            new DiagnosticRow { Group = "Потоки", Check = "Разность сверху/снизу у листа", Value = "0,000000%", Tolerance = "≤ 1,0E-06%", Status = "Совпадают" },
            new DiagnosticRow { Group = "Методы", Check = "Коллокация / Галеркин", Value = "4,70E-02", Tolerance = "справочно", Status = "Проверить" }
        };
    }

    private void RestoreRunParameters(CalculationRun run)
    {
        _isRestoringParameters = true;
        try
        {
            Parameters.CopyFrom(run.Parameters);
            SelectedBackend = run.Backend;
            IsSingleMode = !run.IsSeries;
            _parametersModified = false;
        }
        finally
        {
            _isRestoringParameters = false;
        }

        RefreshSeriesCheckSummaries();
        NotifyParameterContextChanged();
    }

    private void HandleParameterChanged()
    {
        RefreshSeriesCheckSummaries();
        MarkParameterContextModified();
        OnPropertyChanged(nameof(ContextSummary));
        OnPropertyChanged(nameof(SeriesPointTitle));
        OnPropertyChanged(nameof(SeriesAngleCaption));
        OnPropertyChanged(nameof(SeriesEstimateText));
        OnPropertyChanged(nameof(GeometryRegionSummary));
    }

    private void MarkParameterContextModified()
    {
        if (_isRestoringParameters)
            return;

        _parametersModified = true;
        NotifyParameterContextChanged();
    }

    private void NotifyParameterContextChanged()
    {
        OnPropertyChanged(nameof(ContextTitle));
        OnPropertyChanged(nameof(ContextSummary));
        OnPropertyChanged(nameof(SeriesPointTitle));
        OnPropertyChanged(nameof(SeriesAngleCaption));
        OnPropertyChanged(nameof(SeriesEstimateText));
        OnPropertyChanged(nameof(GeometryRegionSummary));
    }

    private string BuildContextSummary()
    {
        string geometry =
            $"λ {Format(Parameters.WavelengthMicrometers, "0.000")} мкм  •  " +
            $"θ {Format(Parameters.IncidenceAngleDegrees, "0.0")}°  •  " +
            $"α₁ {Format(Parameters.PlateStart, "0.000")}  •  β₁ {Format(Parameters.PlateEnd, "0.000")}  •  " +
            $"N {Parameters.HarmonicCount}";

        if (IsSeriesMode)
        {
            return geometry +
                $"  •  δ {Format(Parameters.SeriesSkinDepthStart, "0.000")}…{Format(Parameters.SeriesSkinDepthEnd, "0.000")} ({Parameters.SeriesPointCount})" +
                $"  •  θ {Format(Parameters.SeriesAngleStartDegrees, "0.#")}…{Format(Parameters.SeriesAngleEndDegrees, "0.#")}° / {Format(Parameters.SeriesAngleStepDegrees, "0.#")}°" +
                $"  •  {SelectedBackend}";
        }

        return geometry +
            $"  •  δ {Format(Parameters.SkinDepthMicrometers, "0.000000")}" +
            $"  •  {SelectedBackend}";
    }

    private void RefreshSeriesCheckSummaries()
    {
        SeriesCheckSummaries.Clear();
        SeriesCheckSummaries.Add(
            $"Толщина δ {Format(Parameters.SeriesSkinDepthStart, "0.000")}…{Format(Parameters.SeriesSkinDepthEnd, "0.000")}  •  {Parameters.SeriesPointCount} точек");
        SeriesCheckSummaries.Add(
            $"Угол θ {Format(Parameters.SeriesAngleStartDegrees, "0.#")}…{Format(Parameters.SeriesAngleEndDegrees, "0.#")}°  •  шаг {Format(Parameters.SeriesAngleStepDegrees, "0.#")}°  •  R_scat ≈ T_scat");
    }

    private int GetAnglePointCount()
    {
        if (Parameters.SeriesAngleStepDegrees <= 0 ||
            Parameters.SeriesAngleEndDegrees < Parameters.SeriesAngleStartDegrees)
        {
            return 0;
        }

        return (int)Math.Floor(
            (Parameters.SeriesAngleEndDegrees - Parameters.SeriesAngleStartDegrees) /
            Parameters.SeriesAngleStepDegrees + 1e-9) + 1;
    }

    private static string Format(double value, string format) =>
        value.ToString(format, CultureInfo.GetCultureInfo("ru-RU"));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
