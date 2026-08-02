using System.Collections.ObjectModel;
using System.ComponentModel;
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

    public MainViewModel()
    {
        Runs = new ObservableCollection<CalculationRun>(CreateRuns());
        FluxRows = new ObservableCollection<FluxRow>(CreateFluxRows());
        Coefficients = new ObservableCollection<CoefficientRow>(CreateCoefficients());
        Diagnostics = new ObservableCollection<DiagnosticRow>(CreateDiagnostics());
        JournalEntries = new ObservableCollection<string>
        {
            "[14:32:01] Инициализация параметров...",
            "[14:32:01] Построение сетки гармоник... (N = 30)",
            "[14:32:01] Проверка сходимости и баланса энергии...",
            "[14:32:01] Разность потоков: 0,64% — в допуске (1%).",
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

    public ICollectionView RunsView { get; }
    public ICollectionView FluxView { get; }
    public ICollectionView CoefficientsView { get; }

    public IReadOnlyList<string> Backends { get; } = new[] { "Авто", "CPU", "CUDA" };
    public IReadOnlyList<string> Modes { get; } = new[] { "Один расчёт", "Серия" };
    public IReadOnlyList<string> FluxCategories { get; } = new[] { "Все показатели", "Энергия", "Потоки", "Баланс" };

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
        set => SetProperty(ref _selectedRun, value);
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
        set => SetProperty(ref _selectedBackend, value);
    }

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
        if (parameter is string section)
            CurrentSection = section;
    }

    public void OpenRun(CalculationRun run)
    {
        SelectedRun = run;
        CurrentSection = "Calculations";
        SelectedBackend = run.Backend;
        StatusText = $"Открыт расчёт #{run.RunNumber:000}";
        StatusDetail = $"{run.DateLabel}  •  {run.Status}";
        JournalEntries.Add($"[{DateTime.Now:HH:mm:ss}] Открыт расчёт #{run.RunNumber:000} из истории.");
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
                SkinDepth = "δ 0,010000",
                N = 30,
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
        CurrentSection = "Calculations";
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
            new CalculationRun { RunNumber = 24, DateLabel = "Сегодня 14:32", SkinDepth = "δ 0,010000", N = 30, Backend = "CPU", Status = "В допуске", StatusKind = "Success" },
            new CalculationRun { RunNumber = 23, DateLabel = "Сегодня 11:18", SkinDepth = "δ 0,010000", N = 20, Backend = "CPU", Status = "В допуске", StatusKind = "Success" },
            new CalculationRun { RunNumber = 22, DateLabel = "Вчера 16:47", SkinDepth = "δ 0,020000", N = 30, Backend = "CPU", Status = "Предупреждение", StatusKind = "Warning" },
            new CalculationRun { RunNumber = 21, DateLabel = "Вчера 10:05", SkinDepth = "δ 0,010000", N = 10, Backend = "CPU", Status = "В допуске", StatusKind = "Success" },
            new CalculationRun { RunNumber = 20, DateLabel = "12.05.2026 09:22", SkinDepth = "δ 0,005000", N = 30, Backend = "CPU", Status = "В допуске", StatusKind = "Success" },
            new CalculationRun { RunNumber = 19, DateLabel = "11.05.2026 18:31", SkinDepth = "δ 0,010000", N = 30, Backend = "CUDA", Status = "Аномалия", StatusKind = "Error" },
            new CalculationRun { RunNumber = 18, DateLabel = "10.05.2026 15:09", SkinDepth = "δ 0,015000", N = 40, Backend = "CPU", Status = "В допуске", StatusKind = "Success" }
        };
    }

    private static IEnumerable<FluxRow> CreateFluxRows()
    {
        return new[]
        {
            new FluxRow { Category = "Энергия", Metric = "R_scat (обратно)", Top = 0.629, Bottom = 0.625, DifferencePercent = 0.64, TolerancePercent = 1.0, Status = "В допуске" },
            new FluxRow { Category = "Энергия", Metric = "T_scat (вперёд)", Top = 0.251, Bottom = 0.250, DifferencePercent = 0.40, TolerancePercent = 1.0, Status = "В допуске" },
            new FluxRow { Category = "Энергия", Metric = "A_J (пластина)", Top = 0.121, Bottom = 0.120, DifferencePercent = 0.83, TolerancePercent = 1.0, Status = "В допуске" },
            new FluxRow { Category = "Потоки", Metric = "Поток рассеяния", Top = 0.629, Bottom = 0.625, DifferencePercent = 0.64, TolerancePercent = 1.0, Status = "В допуске" },
            new FluxRow { Category = "Баланс", Metric = "Локальный баланс ЗСЭ", Top = 1.001, Bottom = 0.995, DifferencePercent = 0.60, TolerancePercent = 1.0, Status = "В допуске" }
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
            new DiagnosticRow { Group = "Энергетика", Check = "Невязка ЗСЭ", Value = "0,30%", Tolerance = "≤ 1,00%", Status = "В допуске" },
            new DiagnosticRow { Group = "Потоки", Check = "Разность сверху/снизу", Value = "0,64%", Tolerance = "≤ 1,00%", Status = "В допуске" },
            new DiagnosticRow { Group = "Методы", Check = "Коллокация / Галеркин", Value = "4,70E-02", Tolerance = "справочно", Status = "Проверить" }
        };
    }

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
