using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace Diffraction.WpfPrototype.Views;

public partial class EnergyView : UserControl
{
    public EnergyView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool useCompactSummary = width < 650;
        bool stackPanels = width < 900;

        DesktopSummary.Visibility = useCompactSummary ? Visibility.Collapsed : Visibility.Visible;
        CompactSummary.Visibility = useCompactSummary ? Visibility.Visible : Visibility.Collapsed;
        SummaryRow.Height = useCompactSummary ? GridLength.Auto : new GridLength(90);
        EnergyRoot.Margin = useCompactSummary ? new Thickness(4) : new Thickness(10);

        if (stackPanels)
        {
            GeometryColumn.Width = new GridLength(1, GridUnitType.Star);
            FirstGapColumn.Width = new GridLength(0);
            EnergyColumn.Width = new GridLength(0);
            SecondGapColumn.Width = new GridLength(0);
            FluxColumn.Width = new GridLength(0);
            ResultsGrid.MinHeight = 0;

            FirstGapRow.Height = new GridLength(8);
            EnergyMobileRow.Height = GridLength.Auto;
            SecondGapRow.Height = new GridLength(8);
            FluxMobileRow.Height = GridLength.Auto;

            Grid.SetColumn(GeometryPanel, 0);
            Grid.SetRow(GeometryPanel, 0);
            Grid.SetColumn(EnergyPanel, 0);
            Grid.SetRow(EnergyPanel, 2);
            Grid.SetColumn(FluxPanel, 0);
            Grid.SetRow(FluxPanel, 4);

            GeometryPanel.MinHeight = 420;
            EnergyPanel.MinHeight = 360;
            FluxPanel.MinHeight = 420;
        }
        else
        {
            GeometryColumn.Width = new GridLength(240);
            FirstGapColumn.Width = new GridLength(9);
            EnergyColumn.Width = new GridLength(1.05, GridUnitType.Star);
            SecondGapColumn.Width = new GridLength(9);
            FluxColumn.Width = new GridLength(1.45, GridUnitType.Star);
            ResultsGrid.MinHeight = 430;

            FirstGapRow.Height = new GridLength(0);
            EnergyMobileRow.Height = new GridLength(0);
            SecondGapRow.Height = new GridLength(0);
            FluxMobileRow.Height = new GridLength(0);

            Grid.SetColumn(GeometryPanel, 0);
            Grid.SetRow(GeometryPanel, 0);
            Grid.SetColumn(EnergyPanel, 2);
            Grid.SetRow(EnergyPanel, 0);
            Grid.SetColumn(FluxPanel, 4);
            Grid.SetRow(FluxPanel, 0);

            GeometryPanel.MinHeight = 0;
            EnergyPanel.MinHeight = 0;
            FluxPanel.MinHeight = 0;
        }
    }

    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is TabControl tabControl)
            {
                tabControl.SelectedIndex = 6;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }
}
