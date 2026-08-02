using System.Windows.Controls;
using System.Windows;
using Diffraction.WpfPrototype.Controls;

namespace Diffraction.WpfPrototype.Views;

public partial class FieldMapComparisonView : UserControl
{
    public FieldMapComparisonView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isCompact = width < 760;
        ComparisonGrid.Margin = isCompact ? new Thickness(4) : new Thickness(12);
        ComparisonGrid.MinHeight = isCompact ? 1120 : 620;

        if (isCompact)
        {
            CompactHeaderControlRow.Height = GridLength.Auto;
            HeaderGrid.ColumnDefinitions[1].Width = new GridLength(0);
            Grid.SetRow(LinkCursorsCheckBox, 1);
            Grid.SetColumn(LinkCursorsCheckBox, 0);
            Grid.SetColumnSpan(LinkCursorsCheckBox, 2);
            LinkCursorsCheckBox.Margin = new Thickness(0, 7, 0, 0);
            LinkCursorsCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

            MapsMainRow.Height = GridLength.Auto;
            DifferenceRow.Height = GridLength.Auto;
            IdealMapColumn.Width = new GridLength(1, GridUnitType.Star);
            MapsGapColumn.Width = new GridLength(0);
            SkinMapColumn.Width = new GridLength(0);
            MapsGapRow.Height = new GridLength(8);
            SkinMapRow.Height = GridLength.Auto;
            Grid.SetColumn(IdealPanel, 0);
            Grid.SetRow(IdealPanel, 0);
            Grid.SetColumn(SkinPanel, 0);
            Grid.SetRow(SkinPanel, 2);
            IdealPanel.MinHeight = 330;
            SkinPanel.MinHeight = 330;
            DifferencePanel.MinHeight = 250;
        }
        else
        {
            CompactHeaderControlRow.Height = new GridLength(0);
            HeaderGrid.ColumnDefinitions[1].Width = GridLength.Auto;
            Grid.SetRow(LinkCursorsCheckBox, 0);
            Grid.SetColumn(LinkCursorsCheckBox, 1);
            Grid.SetColumnSpan(LinkCursorsCheckBox, 1);
            LinkCursorsCheckBox.Margin = new Thickness(0);
            LinkCursorsCheckBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            MapsMainRow.Height = new GridLength(2.2, GridUnitType.Star);
            DifferenceRow.Height = new GridLength(1, GridUnitType.Star);
            IdealMapColumn.Width = new GridLength(1, GridUnitType.Star);
            MapsGapColumn.Width = new GridLength(10);
            SkinMapColumn.Width = new GridLength(1, GridUnitType.Star);
            MapsGapRow.Height = new GridLength(0);
            SkinMapRow.Height = new GridLength(0);
            Grid.SetColumn(IdealPanel, 0);
            Grid.SetRow(IdealPanel, 0);
            Grid.SetColumn(SkinPanel, 2);
            Grid.SetRow(SkinPanel, 0);
            IdealPanel.MinHeight = 0;
            SkinPanel.MinHeight = 0;
            DifferencePanel.MinHeight = 0;
        }
    }

    private void IdealMap_CursorChanged(object sender, FieldCursorChangedEventArgs e)
    {
        if (LinkCursorsCheckBox.IsChecked == true)
            SkinMap.SetLinkedCursor(e.NormalizedPosition);
    }

    private void SkinMap_CursorChanged(object sender, FieldCursorChangedEventArgs e)
    {
        if (LinkCursorsCheckBox.IsChecked == true)
            IdealMap.SetLinkedCursor(e.NormalizedPosition);
    }
}
