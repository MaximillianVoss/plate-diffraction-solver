using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Diffraction.WpfPrototype.Controls;
using Diffraction.WpfPrototype.ViewModels;
using Diffraction.WpfPrototype.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class WpfIntegrationTests
{
    [TestMethod]
    [TestCategory("UIIntegration")]
    public void MainWindowCalculationUpdatesBoundPlotsTablesAndMaps()
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();

        var uiThread = new Thread(() =>
        {
            App? application = null;
            try
            {
                application = new App
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                application.InitializeComponent();

                var window = new MainWindow
                {
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    Left = -10000,
                    Top = -10000,
                    Width = 1200,
                    Height = 800
                };
                var viewModel = (MainViewModel)window.DataContext;
                viewModel.Parameters.HarmonicCount = 4;
                viewModel.Parameters.OutputLeft = -1.7;
                viewModel.Parameters.OutputRight = -0.3;
                viewModel.Parameters.OutputBottom = -0.8;
                viewModel.Parameters.OutputTop = 0.8;

                window.Loaded += async (_, _) =>
                {
                    try
                    {
                        CalculationsView calculationsView = Descendants<CalculationsView>(window).Single();
                        TextBox wavelengthInput = (TextBox)calculationsView.FindName("WavelengthInput");
                        wavelengthInput.Text = 1.2.ToString(
                            "0.000",
                            wavelengthInput.Language.GetSpecificCulture());
                        Assert.AreEqual(1.2, viewModel.Parameters.WavelengthMicrometers, 1e-12);

                        await viewModel.CalculateAsync();
                        window.UpdateLayout();

                        Assert.IsTrue(viewModel.Energy.IsAvailable);
                        Assert.AreEqual(1, viewModel.Runs.Count);
                        Assert.AreEqual(1.2, viewModel.Runs[0].Parameters.WavelengthMicrometers, 1e-12);
                        Assert.AreEqual(4, viewModel.Coefficients.Count);
                        Assert.IsTrue(viewModel.SlicePlot.HasData);
                        Assert.IsTrue(viewModel.SkinDifferencePlot.HasData);
                        Assert.IsTrue(viewModel.MethodPlot.HasData);
                        Assert.IsTrue(viewModel.MethodDifferencePlot.HasData);
                        Assert.IsTrue(viewModel.CurrentEnergyPlot.HasData);
                        Assert.IsTrue(Descendants<ScientificPlot>(window).Any(plot => plot.Data?.HasData == true));
                        Assert.IsTrue(Descendants<FieldMapView>(window).Count(map => map.Data is not null) >= 2);

                        wavelengthInput.Text = 1.3.ToString(
                            "0.000",
                            wavelengthInput.Language.GetSpecificCulture());
                        Assert.AreEqual("Параметры изменены", viewModel.StatusText);
                        Assert.AreEqual(1.2, viewModel.Runs[0].Parameters.WavelengthMicrometers, 1e-12);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        window.Close();
                        application.Shutdown();
                        completed.Set();
                    }
                };

                application.Run(window);
            }
            catch (Exception ex)
            {
                failure = ex;
                completed.Set();
                application?.Shutdown();
            }
        });

        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();

        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(30)), "WPF integration test timed out.");
        Assert.IsTrue(uiThread.Join(TimeSpan.FromSeconds(5)), "WPF UI thread did not stop.");
        if (failure is not null)
            throw new AssertFailedException("WPF integration failed: " + failure);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (T descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}
