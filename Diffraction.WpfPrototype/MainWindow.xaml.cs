using System.Windows;
using Diffraction.WpfPrototype.ViewModels;

namespace Diffraction.WpfPrototype;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
