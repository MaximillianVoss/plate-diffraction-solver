using System.Windows.Controls;
using System.Windows;

namespace Diffraction.WpfPrototype.Views;

public partial class CalculationsView : UserControl
{
    private const double CompactBreakpoint = 900;
    private bool? _isCompact;

    public CalculationsView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout(ActualWidth);
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        bool isCompact = width < CompactBreakpoint;
        if (_isCompact == isCompact)
            return;

        _isCompact = isCompact;
        DesktopHeader.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        CompactHeader.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        DesktopParameterGrid.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        CompactParameterScroll.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        CompactResultSelector.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;
        ResultTabs.Style = isCompact ? (Style)FindResource("ContentOnlyTabControlStyle") : null;
        ParametersExpander.IsExpanded = !isCompact;
    }
}
