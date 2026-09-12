using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.ViewModels;

namespace Diffraction.WpfPrototype;

public partial class MainWindow : Window
{
    private const double PhoneBreakpoint = 720;
    private const double TabletBreakpoint = 1100;
    private readonly HashSet<BindingExpression> _invalidParameterBindings = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isPhone = width < PhoneBreakpoint;
        bool isTablet = !isPhone && width < TabletBreakpoint;

        MobileHeader.Visibility = isPhone ? Visibility.Visible : Visibility.Collapsed;
        MobileNavigation.Visibility = isPhone ? Visibility.Visible : Visibility.Collapsed;
        MobileHeaderRow.Height = isPhone ? GridLength.Auto : new GridLength(0);
        MobileNavigationRow.Height = isPhone ? new GridLength(58) : new GridLength(0);

        DesktopNavigation.Visibility = isPhone ? Visibility.Collapsed : Visibility.Visible;
        HistoryPanel.Visibility = isPhone || isTablet ? Visibility.Collapsed : Visibility.Visible;
        NavigationColumn.Width = isPhone ? new GridLength(0) : new GridLength(78);
        HistoryColumn.Width = isPhone || isTablet ? new GridLength(0) : new GridLength(260);
        ContentHost.Margin = isPhone ? new Thickness(5) : isTablet ? new Thickness(8) : new Thickness(10);

        StatusDetailText.Visibility = width >= 820 ? Visibility.Visible : Visibility.Collapsed;
        BuildCaption.Visibility = width >= TabletBreakpoint ? Visibility.Visible : Visibility.Collapsed;
        JournalHint.Visibility = isPhone ? Visibility.Collapsed : Visibility.Visible;
        MobileHistoryCard.Width = Math.Max(300, Math.Min(330, width - 16));
        MobileHistoryCard.Height = Math.Max(300, Math.Min(420, ActualHeight - 160));

        if (!isPhone)
            MobileHistoryPopup.IsOpen = false;
    }

    private void MobileHistoryButton_Click(object sender, RoutedEventArgs e)
        => MobileHistoryPopup.IsOpen = !MobileHistoryPopup.IsOpen;

    private void ParameterValidation_Error(object sender, ValidationErrorEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            e.Error.BindingInError is not BindingExpression binding ||
            binding.ParentBinding.Path?.Path is not string path ||
            !path.StartsWith("Parameters.", StringComparison.Ordinal))
            return;

        string propertyName = path["Parameters.".Length..];
        if (e.Action == ValidationErrorEventAction.Added)
            _invalidParameterBindings.Add(binding);
        else
            _invalidParameterBindings.Remove(binding);
        string label = propertyName switch
        {
            nameof(CalculationParameters.WavelengthMicrometers) => "Длина волны λ",
            nameof(CalculationParameters.IncidenceAngleDegrees) => "Угол θ",
            nameof(CalculationParameters.PlateStart) => "Левая граница пластины α₁",
            nameof(CalculationParameters.PlateEnd) => "Правая граница пластины β₁",
            nameof(CalculationParameters.HarmonicCount) => "Параметр N",
            nameof(CalculationParameters.SkinDepthMicrometers) => "Толщина скин-слоя δ",
            nameof(CalculationParameters.OutputLeft) => "Левая граница области xL",
            nameof(CalculationParameters.OutputRight) => "Правая граница области xR",
            nameof(CalculationParameters.OutputBottom) => "Нижняя граница области yDn",
            nameof(CalculationParameters.OutputTop) => "Верхняя граница области yUp",
            nameof(CalculationParameters.SeriesSkinDepthStart) => "Начало серии δ",
            nameof(CalculationParameters.SeriesSkinDepthEnd) => "Конец серии δ",
            nameof(CalculationParameters.SeriesPointCount) => "Число точек серии δ",
            nameof(CalculationParameters.SeriesAngleStartDegrees) => "Начальный угол серии",
            nameof(CalculationParameters.SeriesAngleEndDegrees) => "Конечный угол серии",
            nameof(CalculationParameters.SeriesAngleStepDegrees) => "Шаг угла",
            _ => "Числовой параметр"
        };
        bool integer = propertyName is nameof(CalculationParameters.HarmonicCount) or nameof(CalculationParameters.SeriesPointCount);
        string message = label + (integer ? ": введите целое число." : ": введите число.");
        viewModel.SetInputError(binding, propertyName,
            e.Action == ValidationErrorEventAction.Added ? message : null);
        e.Handled = true;
    }

    private void ParameterSourceUpdated(object sender, DataTransferEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            BindingOperations.GetBindingExpression(e.TargetObject, e.Property) is not BindingExpression updated ||
            updated.ParentBinding.Path?.Path is not string path ||
            !path.StartsWith("Parameters.", StringComparison.Ordinal))
            return;

        // A parameter has several editors (desktop, compact, series). Refresh only the
        // other invalid editors, so the active editor can keep partially typed decimals.
        foreach (BindingExpression invalid in _invalidParameterBindings.ToArray())
        {
            if (!ReferenceEquals(invalid, updated) && ReferenceEquals(invalid.DataItem, updated.DataItem) &&
                invalid.ParentBinding.Path?.Path == path && invalid.Status != BindingStatus.Detached)
                invalid.UpdateTarget();
        }
        viewModel.RefreshParameterError();
    }

    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: CalculationRun run } ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.OpenRun(run);
        MobileHistoryPopup.IsOpen = false;
    }
}
