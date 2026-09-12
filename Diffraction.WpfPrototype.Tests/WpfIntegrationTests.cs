using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Diffraction.WpfPrototype.Controls;
using Diffraction.WpfPrototype.Infrastructure;
using Diffraction.WpfPrototype.ViewModels;
using Diffraction.WpfPrototype.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class WpfIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("UIIntegration")]
    public void MainWindowCalculationUpdatesBoundPlotsTablesAndMaps()
    {
        Exception? failure = null;
        bool scenarioCompleted = false;
        int scenarioStarts = 0;
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
                    Width = 1440,
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
                    if (Interlocked.Increment(ref scenarioStarts) != 1)
                        return;
                    try
                    {
                        CalculationsView calculationsView = Descendants<CalculationsView>(window).Single();
                        TextBox wavelengthInput = (TextBox)calculationsView.FindName("WavelengthInput");
                        wavelengthInput.Text = 1.2.ToString(
                            "0.000",
                            wavelengthInput.Language.GetSpecificCulture());
                        Assert.AreEqual(1.2, viewModel.Parameters.WavelengthMicrometers, 1e-12);

                        Task calculation = viewModel.CalculateAsync();
                        Assert.IsTrue(viewModel.IsBusy);
                        Assert.IsFalse(wavelengthInput.IsEnabled, "Parameters must be locked while a calculation is running.");
                        await calculation;
                        window.UpdateLayout();
                        Assert.IsTrue(wavelengthInput.IsEnabled);

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

                        var previousRun = viewModel.Runs[0];
                        var previousPlot = viewModel.SlicePlot;
                        var command = (AsyncRelayCommand)viewModel.RunCommand;
                        wavelengthInput.Text = "0";
                        await command.ExecuteAsync();
                        window.UpdateLayout();
                        StringAssert.Contains(viewModel.StatusDetail, "Длина волны должна быть положительным числом.");
                        Assert.IsTrue(((Border)window.FindName("ErrorBanner")).IsVisible);
                        StringAssert.Contains(((TextBlock)window.FindName("ErrorMessageText")).Text, "Длина волны");
                        SavePreview(window, "validation-desktop.png");
                        window.Width = 390;
                        window.UpdateLayout();
                        Assert.IsTrue(((Border)window.FindName("ErrorBanner")).IsVisible);
                        SavePreview(window, "validation-compact.png");
                        window.Width = 1440;
                        window.UpdateLayout();
                        Assert.AreSame(previousPlot, viewModel.SlicePlot);
                        Assert.AreEqual(1, viewModel.Runs.Count);
                        Assert.IsFalse(viewModel.IsBusy);

                        foreach (string invalidText in new[] { "", "abc" })
                        {
                            wavelengthInput.Text = invalidText;
                            Assert.IsTrue(Validation.GetHasError(wavelengthInput));
                            await command.ExecuteAsync();
                            StringAssert.Contains(viewModel.ErrorMessage, "введите число");
                            Assert.AreSame(previousRun, viewModel.Runs[0]);
                            Assert.AreSame(previousPlot, viewModel.SlicePlot);
                            Assert.AreEqual(1, viewModel.Runs.Count);
                        }

                        wavelengthInput.Text = "1,5";
                        await command.ExecuteAsync();
                        Assert.IsFalse(viewModel.HasError);
                        Assert.AreEqual(2, viewModel.Runs.Count);
                        Assert.AreEqual(1.5, viewModel.Runs[0].Parameters.WavelengthMicrometers, 1e-12);
                        Assert.AreNotSame(previousPlot, viewModel.SlicePlot);

                        previousPlot = viewModel.SlicePlot;
                        viewModel.Parameters.SkinDepthMicrometers = double.MaxValue;
                        await command.ExecuteAsync();
                        Assert.IsTrue(viewModel.HasError);
                        StringAssert.Contains(viewModel.ErrorMessage, "числ");
                        Assert.AreEqual(2, viewModel.Runs.Count);
                        Assert.AreSame(previousPlot, viewModel.SlicePlot);
                        Assert.IsTrue(command.CanExecute(null));
                        viewModel.Parameters.SkinDepthMicrometers = 0.01;
                        wavelengthInput.Text = "1,7";
                        void CancelBeforePublication(object? _, System.ComponentModel.PropertyChangedEventArgs args)
                        {
                            if (args.PropertyName == nameof(MainViewModel.StatusText) &&
                                viewModel.StatusText == "Обновление таблиц и графиков...")
                                viewModel.CancelCommand.Execute(null);
                        }
                        viewModel.PropertyChanged += CancelBeforePublication;
                        await command.ExecuteAsync();
                        viewModel.PropertyChanged -= CancelBeforePublication;
                        Assert.AreEqual("Расчёт отменён", viewModel.StatusText);
                        Assert.AreEqual(2, viewModel.Runs.Count);
                        Assert.AreSame(previousPlot, viewModel.SlicePlot);
                        Assert.IsFalse(viewModel.HasError);
                        Assert.IsTrue(command.CanExecute(null));

                        bool commandErrorReported = false;
                        var failingCommand = new AsyncRelayCommand(async () =>
                        {
                            await Task.Yield();
                            throw new InvalidOperationException("Async command failure");
                        }, _ => commandErrorReported = true);
                        failingCommand.Execute(null);
                        await window.Dispatcher.InvokeAsync(() => { },
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        Assert.IsTrue(commandErrorReported);
                        Assert.IsTrue(failingCommand.CanExecute(null));

                        viewModel.OpenRun(viewModel.Runs[0]);
                        wavelengthInput.Text = "abc";
                        Assert.IsTrue(viewModel.HasError);
                        viewModel.OpenRun(viewModel.Runs[0]);
                        window.UpdateLayout();
                        Assert.IsFalse(Validation.GetHasError(wavelengthInput), "Restoring the same values must also clear invalid input text.");
                        Assert.IsFalse(viewModel.HasError);
                        scenarioCompleted = true;
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
        Assert.IsTrue(scenarioCompleted, "The complete UI scenario must run before the test can pass.");
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

    private void SavePreview(Window window, string fileName)
    {
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "ui-artifacts");
        System.IO.Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, fileName);
        using (var stream = System.IO.File.Create(path))
            encoder.Save(stream);
        TestContext.AddResultFile(path);
        Assert.IsTrue(System.IO.File.Exists(path));
    }
}
