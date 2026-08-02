using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace Diffraction.WpfPrototype.Views;

public partial class EnergyView : UserControl
{
    public EnergyView()
    {
        InitializeComponent();
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
