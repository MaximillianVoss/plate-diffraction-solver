using System.Windows.Controls;
using System.Windows;

namespace Diffraction.WpfPrototype.Views;

public partial class SeriesView : UserControl
{
    public SeriesView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isCompact = width < 900;
        DesktopSeriesHeader.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        CompactSeriesHeader.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        SeriesRoot.Margin = isCompact ? new Thickness(0) : new Thickness(0);

        if (isCompact)
        {
            SeriesParametersColumn.Width = new GridLength(1, GridUnitType.Star);
            SeriesFirstGapColumn.Width = new GridLength(0);
            SeriesChartsColumn.Width = new GridLength(0);
            SeriesSecondGapColumn.Width = new GridLength(0);
            SeriesResultsColumn.Width = new GridLength(0);
            SeriesContentGrid.MinHeight = 0;

            SeriesFirstGapRow.Height = new GridLength(8);
            SeriesChartsRow.Height = GridLength.Auto;
            SeriesSecondGapRow.Height = new GridLength(8);
            SeriesResultsRow.Height = GridLength.Auto;

            Grid.SetColumn(SeriesParametersPanel, 0);
            Grid.SetRow(SeriesParametersPanel, 0);
            Grid.SetColumn(SeriesChartsPanel, 0);
            Grid.SetRow(SeriesChartsPanel, 2);
            Grid.SetColumn(SeriesResultsPanel, 0);
            Grid.SetRow(SeriesResultsPanel, 4);
            SeriesParametersPanel.MinHeight = 500;
            SeriesChartsPanel.MinHeight = 720;
            SeriesResultsPanel.MinHeight = 430;
        }
        else
        {
            SeriesParametersColumn.Width = new GridLength(250);
            SeriesFirstGapColumn.Width = new GridLength(10);
            SeriesChartsColumn.Width = new GridLength(1, GridUnitType.Star);
            SeriesSecondGapColumn.Width = new GridLength(10);
            SeriesResultsColumn.Width = new GridLength(320);
            SeriesContentGrid.MinHeight = 620;

            SeriesFirstGapRow.Height = new GridLength(0);
            SeriesChartsRow.Height = new GridLength(0);
            SeriesSecondGapRow.Height = new GridLength(0);
            SeriesResultsRow.Height = new GridLength(0);

            Grid.SetColumn(SeriesParametersPanel, 0);
            Grid.SetRow(SeriesParametersPanel, 0);
            Grid.SetColumn(SeriesChartsPanel, 2);
            Grid.SetRow(SeriesChartsPanel, 0);
            Grid.SetColumn(SeriesResultsPanel, 4);
            Grid.SetRow(SeriesResultsPanel, 0);
            SeriesParametersPanel.MinHeight = 0;
            SeriesChartsPanel.MinHeight = 0;
            SeriesResultsPanel.MinHeight = 0;
        }
    }
}
