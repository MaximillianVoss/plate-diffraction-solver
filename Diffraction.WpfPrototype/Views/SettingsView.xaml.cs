using System.Windows.Controls;
using System.Windows;

namespace Diffraction.WpfPrototype.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isCompact = width < 600;
        SettingsContent.MaxWidth = isCompact ? double.PositiveInfinity : 820;

        if (isCompact)
        {
            GeneralLabelColumn.Width = new GridLength(1, GridUnitType.Star);
            GeneralControlColumn.Width = new GridLength(0);
            GeneralControlRow1.Height = GridLength.Auto;
            GeneralLabelRow2.Height = GridLength.Auto;
            GeneralControlRow2.Height = GridLength.Auto;
            Position(BackendLabel, 0, 0, new Thickness(0));
            Position(BackendControl, 1, 0, new Thickness(0, 5, 0, 0));
            Position(MapLabel, 2, 0, new Thickness(0, 14, 0, 0));
            Position(MapControl, 3, 0, new Thickness(0, 6, 0, 0));
            Position(HistoryLabel, 4, 0, new Thickness(0, 14, 0, 0));
            Position(HistoryControl, 5, 0, new Thickness(0, 6, 0, 0));

            ToleranceLabelColumn.Width = new GridLength(1, GridUnitType.Star);
            ToleranceControlColumn.Width = new GridLength(0);
            ToleranceControlRow1.Height = GridLength.Auto;
            ToleranceLabelRow2.Height = GridLength.Auto;
            ToleranceControlRow2.Height = GridLength.Auto;
            Position(EnergyToleranceLabel, 0, 0, new Thickness(0));
            Position(EnergyToleranceControl, 1, 0, new Thickness(0, 5, 0, 0));
            Position(FluxToleranceLabel, 2, 0, new Thickness(0, 14, 0, 0));
            Position(FluxToleranceControl, 3, 0, new Thickness(0, 5, 0, 0));
            Position(NegativeEnergyLabel, 4, 0, new Thickness(0, 14, 0, 0));
            Position(NegativeEnergyControl, 5, 0, new Thickness(0, 6, 0, 0));
        }
        else
        {
            GeneralLabelColumn.Width = new GridLength(1, GridUnitType.Star);
            GeneralControlColumn.Width = new GridLength(220);
            GeneralControlRow1.Height = new GridLength(0);
            GeneralLabelRow2.Height = new GridLength(0);
            GeneralControlRow2.Height = new GridLength(0);
            Position(BackendLabel, 0, 0, new Thickness(0));
            Position(BackendControl, 0, 1, new Thickness(0));
            Position(MapLabel, 1, 0, new Thickness(0, 16, 0, 0));
            Position(MapControl, 1, 1, new Thickness(0, 16, 0, 0));
            Position(HistoryLabel, 2, 0, new Thickness(0, 16, 0, 0));
            Position(HistoryControl, 2, 1, new Thickness(0, 16, 0, 0));

            ToleranceLabelColumn.Width = new GridLength(1, GridUnitType.Star);
            ToleranceControlColumn.Width = new GridLength(140);
            ToleranceControlRow1.Height = new GridLength(0);
            ToleranceLabelRow2.Height = new GridLength(0);
            ToleranceControlRow2.Height = new GridLength(0);
            Position(EnergyToleranceLabel, 0, 0, new Thickness(0));
            Position(EnergyToleranceControl, 0, 1, new Thickness(0));
            Position(FluxToleranceLabel, 1, 0, new Thickness(0, 12, 0, 0));
            Position(FluxToleranceControl, 1, 1, new Thickness(0, 12, 0, 0));
            Position(NegativeEnergyLabel, 2, 0, new Thickness(0, 12, 0, 0));
            Position(NegativeEnergyControl, 2, 1, new Thickness(0, 12, 0, 0));
        }
    }

    private static void Position(FrameworkElement element, int row, int column, Thickness margin)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        element.Margin = margin;
    }
}
