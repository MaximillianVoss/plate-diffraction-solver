using System.Windows.Controls;
using System.Windows;

namespace Diffraction.WpfPrototype.Views;

public partial class ExportView : UserControl
{
    public ExportView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isCompact = width < 760;
        ExportRoot.MaxWidth = isCompact ? double.PositiveInfinity : 1080;

        if (isCompact)
        {
            ExportOptionsColumn.Width = new GridLength(1, GridUnitType.Star);
            ExportGapColumn.Width = new GridLength(0);
            ExportPreviewColumn.Width = new GridLength(0);
            ExportGapRow.Height = new GridLength(8);
            ExportPreviewRow.Height = GridLength.Auto;
            ExportContentGrid.MinHeight = 0;
            Grid.SetColumn(ExportOptionsPanel, 0);
            Grid.SetRow(ExportOptionsPanel, 0);
            Grid.SetColumn(ExportPreviewPanel, 0);
            Grid.SetRow(ExportPreviewPanel, 2);
            ExportPreviewPanel.MinHeight = 620;
        }
        else
        {
            ExportOptionsColumn.Width = new GridLength(340);
            ExportGapColumn.Width = new GridLength(12);
            ExportPreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            ExportGapRow.Height = new GridLength(0);
            ExportPreviewRow.Height = new GridLength(0);
            ExportContentGrid.MinHeight = 560;
            Grid.SetColumn(ExportOptionsPanel, 0);
            Grid.SetRow(ExportOptionsPanel, 0);
            Grid.SetColumn(ExportPreviewPanel, 2);
            Grid.SetRow(ExportPreviewPanel, 0);
            ExportPreviewPanel.MinHeight = 0;
        }
    }
}
