using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.ViewModels;

namespace Diffraction.WpfPrototype;

public partial class MainWindow : Window
{
    private const double PhoneBreakpoint = 720;
    private const double TabletBreakpoint = 1100;

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
