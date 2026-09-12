using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using Diffraction.WpfPrototype.Infrastructure;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;

namespace Diffraction.WpfPrototype.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _currentSection = "Calculations";
    private EnergySnapshot _energy = EnergySnapshot.Empty;
    private PlotData _slicePlot = PlotData.Empty("x", "Re u(x, λ/10)");
    private PlotData _methodPlot = PlotData.Empty("x", "Re u(x, λ/10)");
    private PlotData _methodDifferencePlot = PlotData.Empty("x", "|u_col − u_gal|");
    private PlotData _skinDifferencePlot = PlotData.Empty("x", "|u_skin − u_ideal|");
    private PlotData _currentEnergyPlot = PlotData.Empty("Толщина δ", "Доля падающей энергии");
    private PlotData _skinEnergyPlot = PlotData.Empty("Толщина δ", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»");
    private PlotData _angleEnergyPlot = PlotData.Empty("Угол θ, °", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»");
    private PlotData _seriesDiagnosticsPlot = PlotData.Empty("Толщина δ", "Отклонение, %", "Запустите расчёт в режиме «Серия»");
    private FieldMapData? _idealFieldMap;
    private FieldMapData? _skinFieldMap;
    private string _diagnosticsSummary = "Расчёт ещё не выполнен.";
    private string _seriesDiagnosticsCaption = "Серийная диагностика ещё не рассчитана.";
    private string _historySearch = string.Empty;
    private string _fluxSearch = string.Empty;
    private string _fluxCategory = "Все показатели";
    private string _coefficientSearch = string.Empty;
    private CalculationRun? _selectedRun;
    private bool _isBusy;
    private bool _isSingleMode = true;
    private string _selectedBackend = "Авто";
    private string _statusText = "Готов к расчёту";
    private string _statusDetail = "Задайте параметры и нажмите «Рассчитать»";
    private double _progress;
    private CancellationTokenSource? _calculationCancellation;
    private bool _isRestoringParameters;
    private bool _parametersModified;
    private string _errorMessage = string.Empty;
    private string _errorTitle = string.Empty;
    private readonly Dictionary<object, (string PropertyName, string Message)> _inputErrors = new();

    public MainViewModel()
    {
        Parameters = CalculationParameters.CreateDefault();
        SeriesCheckSummaries = new ObservableCollection<string>();
        Parameters.PropertyChanged += (_, _) => HandleParameterChanged();

        Runs = new ObservableCollection<CalculationRun>();
        FluxRows = new ObservableCollection<FluxRow>();
        Coefficients = new ObservableCollection<CoefficientRow>();
        Diagnostics = new ObservableCollection<DiagnosticRow>();
        JournalEntries = new ObservableCollection<string>
        {
            $"[{DateTime.Now:HH:mm:ss}] Программа готова. Введите параметры одной пластины и запустите расчёт."
        };

        RunsView = CollectionViewSource.GetDefaultView(Runs);
        RunsView.Filter = FilterRun;
        FluxView = CollectionViewSource.GetDefaultView(FluxRows);
        FluxView.Filter = FilterFlux;
        CoefficientsView = CollectionViewSource.GetDefaultView(Coefficients);
        CoefficientsView.Filter = FilterCoefficient;

        NavigateCommand = new RelayCommand(Navigate);
        RunCommand = new AsyncRelayCommand(CalculateAsync, ReportCalculationError, () => !IsBusy);
        DismissErrorCommand = new RelayCommand(_ => ClearError());
        CancelCommand = new RelayCommand(_ => CancelCalculation(), _ => IsBusy);
        NewRunCommand = new RelayCommand(_ => PrepareNewRun(), _ => !IsBusy);
        ExportCommand = new RelayCommand(_ => RegisterAction("Экспорт будет доступен после отдельной настройки формата файла."));
        CompareCommand = new RelayCommand(_ => RegisterAction("Графики сравнения находятся на вкладке «Галеркин/коллокация»."));
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

    public EnergySnapshot Energy
    {
        get => _energy;
        private set => SetProperty(ref _energy, value);
    }

    public PlotData SlicePlot
    {
        get => _slicePlot;
        private set => SetProperty(ref _slicePlot, value);
    }

    public PlotData MethodPlot
    {
        get => _methodPlot;
        private set => SetProperty(ref _methodPlot, value);
    }

    public PlotData MethodDifferencePlot
    {
        get => _methodDifferencePlot;
        private set => SetProperty(ref _methodDifferencePlot, value);
    }

    public PlotData SkinDifferencePlot
    {
        get => _skinDifferencePlot;
        private set => SetProperty(ref _skinDifferencePlot, value);
    }

    public PlotData CurrentEnergyPlot
    {
        get => _currentEnergyPlot;
        private set => SetProperty(ref _currentEnergyPlot, value);
    }

    public PlotData SkinEnergyPlot
    {
        get => _skinEnergyPlot;
        private set => SetProperty(ref _skinEnergyPlot, value);
    }

    public PlotData AngleEnergyPlot
    {
        get => _angleEnergyPlot;
        private set => SetProperty(ref _angleEnergyPlot, value);
    }

    public PlotData SeriesDiagnosticsPlot
    {
        get => _seriesDiagnosticsPlot;
        private set => SetProperty(ref _seriesDiagnosticsPlot, value);
    }

    public FieldMapData? IdealFieldMap
    {
        get => _idealFieldMap;
        private set => SetProperty(ref _idealFieldMap, value);
    }

    public FieldMapData? SkinFieldMap
    {
        get => _skinFieldMap;
        private set => SetProperty(ref _skinFieldMap, value);
    }

    public string DiagnosticsSummary
    {
        get => _diagnosticsSummary;
        private set => SetProperty(ref _diagnosticsSummary, value);
    }

    public string SeriesDiagnosticsCaption
    {
        get => _seriesDiagnosticsCaption;
        private set => SetProperty(ref _seriesDiagnosticsCaption, value);
    }

    public CalculationParameters Parameters { get; }

    public IReadOnlyList<string> Backends { get; } = new[] { "Авто", "CPU" };
    public IReadOnlyList<string> Modes { get; } = new[] { "Один расчёт", "Серия" };
    public IReadOnlyList<string> FluxCategories { get; } = new[] { "Все показатели", "Рассеяние", "Потоки", "Баланс" };

    public ICommand NavigateCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand DismissErrorCommand { get; }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
                return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(StatusIndicatorColor));
        }
    }

    public string ErrorTitle
    {
        get => _errorTitle;
        private set => SetProperty(ref _errorTitle, value);
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string StatusIndicatorColor => HasError ? "#B42318" : IsBusy ? "#2563EB" : "#667085";
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
            {
                OnPropertyChanged(nameof(StatusIndicatorColor));
                OnPropertyChanged(nameof(CanEditParameters));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanEditParameters => !IsBusy;

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

    public async Task CalculateAsync()
    {
        if (IsBusy)
            return;

        using var cancellation = new CancellationTokenSource();
        _calculationCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        try
        {
            CalculationParameters parameters = Parameters.Clone();
            bool includeSeries = IsSeriesMode;
            string? validationError = GetInputError(includeSeries) ??
                DiffractionCalculationService.GetValidationError(parameters, includeSeries);
            if (validationError is not null)
            {
                ReportError("Проверьте параметры", validationError);
                return;
            }

            ClearError();
            IsBusy = true;
            Progress = 0;
            DateTime startedAt = DateTime.Now;
            JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Запуск расчёта по параметрам: λ={parameters.WavelengthMicrometers:0.000}, θ={parameters.IncidenceAngleDegrees:0.###}°, δ={parameters.SkinDepthMicrometers:0.000000}, N={parameters.HarmonicCount}");
            await SetPhaseAsync(token, "Расчёт поля и диагностики...", 12);

            CalculationOutput output = await Task.Run(() =>
                DiffractionCalculationService.Calculate(parameters, includeSeries, token), token);

            await SetPhaseAsync(token, "Обновление таблиц и графиков...", 92);
            token.ThrowIfCancellationRequested();

            bool toleranceExceeded = output.Energy.LocalBalanceErrorPercent > 2.0;
            var completedRun = new CalculationRun
            {
                RunNumber = Runs.Count == 0 ? 1 : Runs.Max(run => run.RunNumber) + 1,
                DateLabel = $"Сегодня {DateTime.Now:HH:mm}",
                Parameters = parameters,
                IsSeries = includeSeries,
                Backend = "CPU",
                Status = toleranceExceeded ? "Предупреждение" : "В допуске",
                StatusKind = toleranceExceeded ? "Warning" : "Success",
                Output = output
            };

            // No await after publication: cancellation must preserve the previous complete result.
            Runs.Insert(0, completedRun);
            SelectedRun = completedRun;
            RunsView.Refresh();
            StatusText = "Расчёт завершён";
            StatusDetail = $"{output.BackendName}  •  {(DateTime.Now - startedAt).TotalSeconds:0.00} с";
            JournalEntries.Add(
                $"[{DateTime.Now:HH:mm:ss}] Расчёт завершён: A_J={output.Energy.Absorbed:0.000000}, ΔЗСЭ={output.Energy.LocalBalanceErrorPercent:0.000}%.");
            _parametersModified = false;
            NotifyParameterContextChanged();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusText = "Расчёт отменён";
            StatusDetail = "Результаты не изменены";
            JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Расчёт отменён пользователем.");
        }
        catch (Exception ex)
        {
            ReportCalculationError(ex);
        }
        finally
        {
            _calculationCancellation = null;
            IsBusy = false;
            Progress = 0;
        }
    }

    public void SetInputError(object source, string propertyName, string? message)
    {
        if (message is null)
        {
            if (_inputErrors.Remove(source, out var removed) &&
                ErrorMessage.StartsWith(removed.Message, StringComparison.Ordinal))
            {
                string? remainingError = GetInputError(IsSeriesMode);
                if (remainingError is null)
                    ClearError();
                else
                    ReportError("Проверьте ввод", remainingError);
            }
            return;
        }

        _inputErrors[source] = (propertyName, message);
        if (IsSeriesMode || !propertyName.StartsWith("Series", StringComparison.Ordinal))
            ReportError("Проверьте ввод", message);
    }

    private string? GetInputError(bool includeSeries) =>
        _inputErrors.Values
            .Where(error => includeSeries || !error.PropertyName.StartsWith("Series", StringComparison.Ordinal))
            .Select(error => error.Message)
            .FirstOrDefault();

    private void ReportCalculationError(Exception exception)
    {
        string message = exception is AggregateException aggregate
            ? string.Join(Environment.NewLine, aggregate.Flatten().InnerExceptions.Select(item => item.Message).Distinct())
            : exception.Message;
        ReportError("Ошибка расчёта", message, exception);
        IsBusy = false;
        Progress = 0;
    }

    private void ReportError(string title, string message, Exception? exception = null)
    {
        ErrorTitle = title;
        ErrorMessage = message + (Energy.IsAvailable
            ? " Показаны результаты предыдущего успешного расчёта."
            : string.Empty);
        StatusText = title;
        StatusDetail = message;
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] {title}: {exception?.ToString() ?? message}");
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        ErrorTitle = string.Empty;
    }

    private async Task SetPhaseAsync(CancellationToken token, string message, double progress)
    {
        token.ThrowIfCancellationRequested();
        StatusText = message;
        StatusDetail = $"{(SelectedBackend == "Авто" ? "CPU" : SelectedBackend)}  •  {progress:0}%";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        Progress = progress;
        await Task.Yield();
        token.ThrowIfCancellationRequested();
    }

    private void RefreshFluxRows(EnergySnapshot energy)
    {
        FluxRows.Clear();
        foreach (FluxRow row in CreateFluxRows(energy))
            FluxRows.Add(row);
    }

    private void RefreshDiagnostics(IEnumerable<DiagnosticRow> rows)
    {
        Diagnostics.Clear();
        foreach (DiagnosticRow row in rows)
            Diagnostics.Add(row);
    }

    private void ApplyCalculationOutput(CalculationOutput output)
    {
        Energy = output.Energy;
        SlicePlot = output.SlicePlot;
        MethodPlot = output.MethodPlot;
        MethodDifferencePlot = output.MethodDifferencePlot;
        SkinDifferencePlot = output.SkinDifferencePlot;
        CurrentEnergyPlot = output.CurrentEnergyPlot;
        SkinEnergyPlot = output.SkinEnergyPlot;
        AngleEnergyPlot = output.AngleEnergyPlot;
        SeriesDiagnosticsPlot = output.SeriesDiagnosticsPlot;
        IdealFieldMap = output.IdealFieldMap;
        SkinFieldMap = output.SkinFieldMap;
        DiagnosticsSummary = output.DiagnosticsSummary;
        SeriesDiagnosticsCaption = output.SeriesDiagnosticsCaption;

        RefreshFluxRows(output.Energy);
        RefreshDiagnostics(output.Diagnostics);

        Coefficients.Clear();
        foreach (CoefficientRow row in output.Coefficients)
            Coefficients.Add(row);
        CoefficientsView.Refresh();

        SeriesCheckSummaries.Clear();
        foreach (string summary in output.SeriesCheckSummaries)
            SeriesCheckSummaries.Add(summary);
    }

    private void CancelCalculation() => _calculationCancellation?.Cancel();

    private void PrepareNewRun()
    {
        ClearError();
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
        ClearCalculationOutput();
        NotifyParameterContextChanged();
        StatusText = "Новый расчёт";
        StatusDetail = "Проверьте параметры и нажмите «Рассчитать»";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Подготовлен новый набор параметров.");
    }

    private void RegisterAction(string message)
    {
        StatusText = message;
        StatusDetail = "Результаты расчёта не изменены";
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

    private static IEnumerable<FluxRow> CreateFluxRows(EnergySnapshot energy)
    {
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
                Status = BuildStatusText(energy.FarFieldMismatchPercent, 0.000001)
            },
            new FluxRow
            {
                Category = "Потоки",
                Metric = "У листа: сверху / снизу",
                Top = energy.SheetAbove,
                Bottom = energy.SheetBelow,
                DifferencePercent = energy.SheetMismatchPercent,
                TolerancePercent = 0.000001,
                Status = BuildStatusText(energy.SheetMismatchPercent, 0.000001)
            },
            new FluxRow
            {
                Category = "Баланс",
                Metric = "A_flux / A_J",
                Top = energy.FluxAbsorbed,
                Bottom = energy.Absorbed,
                DifferencePercent = energy.LocalBalanceErrorPercent,
                TolerancePercent = 2.0,
                Status = BuildStatusText(energy.LocalBalanceErrorPercent, 2.0)
            }
        };
    }

    private static string BuildStatusText(double differencePercent, double tolerancePercent) =>
        differencePercent <= tolerancePercent ? "В допуске" : "Проверить";

    private void RestoreRunParameters(CalculationRun run)
    {
        ClearError();
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

        if (run.Output is not null)
            ApplyCalculationOutput(run.Output);
        NotifyParameterContextChanged();
    }

    private void HandleParameterChanged()
    {
        MarkParameterContextModified();
        if (!_isRestoringParameters && Energy.IsAvailable)
        {
            StatusText = "Параметры изменены";
            StatusDetail = "Нажмите «Рассчитать», чтобы обновить результаты";
        }
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

    private void ClearCalculationOutput()
    {
        Energy = EnergySnapshot.Empty;
        SlicePlot = PlotData.Empty("x", "Re u(x, λ/10)");
        MethodPlot = PlotData.Empty("x", "Re u(x, λ/10)");
        MethodDifferencePlot = PlotData.Empty("x", "|u_col − u_gal|");
        SkinDifferencePlot = PlotData.Empty("x", "|u_skin − u_ideal|");
        CurrentEnergyPlot = PlotData.Empty("Толщина δ", "Доля падающей энергии");
        SkinEnergyPlot = PlotData.Empty("Толщина δ", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»");
        AngleEnergyPlot = PlotData.Empty("Угол θ, °", "Доля падающей энергии", "Запустите расчёт в режиме «Серия»");
        SeriesDiagnosticsPlot = PlotData.Empty("Толщина δ", "Отклонение, %", "Запустите расчёт в режиме «Серия»");
        IdealFieldMap = null;
        SkinFieldMap = null;
        DiagnosticsSummary = "Расчёт ещё не выполнен.";
        SeriesDiagnosticsCaption = "Серийная диагностика ещё не рассчитана.";

        FluxRows.Clear();
        Coefficients.Clear();
        Diagnostics.Clear();
        SeriesCheckSummaries.Clear();
    }

    private int GetAnglePointCount() => DiffractionCalculationService.GetAnglePointCount(Parameters);

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
