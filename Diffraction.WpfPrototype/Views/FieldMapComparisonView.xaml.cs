using System.Windows.Controls;
using Diffraction.WpfPrototype.Controls;

namespace Diffraction.WpfPrototype.Views;

public partial class FieldMapComparisonView : UserControl
{
    public FieldMapComparisonView()
    {
        InitializeComponent();
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
