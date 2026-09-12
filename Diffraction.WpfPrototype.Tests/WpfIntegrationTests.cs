using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Diffraction.WpfPrototype.Controls;
using Diffraction.WpfPrototype.Infrastructure;
using Diffraction.WpfPrototype.Models;
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
                        await CheckAdditionalWorkflowsAsync(window);
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

        Assert.IsTrue(completed.Wait(TimeSpan.FromSeconds(90)), "WPF integration test timed out.");
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

    private async Task CheckAdditionalWorkflowsAsync(MainWindow window)
    {
        var failures = new List<Exception>();
        CalculationsView calculationsView = Descendants<CalculationsView>(window).Single();
        SeriesView seriesView = Descendants<SeriesView>(window).Single();
        var parametersExpander = (Expander)calculationsView.FindName("ParametersExpander");

        async Task Check(string name, Func<MainViewModel, Task> scenario)
        {
            var model = new MainViewModel();
            model.Parameters.HarmonicCount = 2;
            window.DataContext = model;
            window.Width = 1440;
            parametersExpander.IsExpanded = true;
            window.UpdateLayout();
            try
            {
                await scenario(model);
                TestContext.WriteLine("PASS: " + name);
            }
            catch (Exception error)
            {
                failures.Add(new InvalidOperationException(name, error));
            }
        }

        await Check("Editing the same value in another view clears an earlier parse error", model =>
        {
            FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers)).Text = "abc";
            Assert.IsTrue(model.HasError);
            model.IsSeriesMode = true;
            window.UpdateLayout();
            FindInput(seriesView, nameof(CalculationParameters.WavelengthMicrometers)).Text = "1,0000";
            Assert.IsFalse(model.HasError, "A corrected shared parameter must not retain an error from its hidden editor.");
            return Task.CompletedTask;
        });

        await Check("Editing after a resize clears an earlier parse error", model =>
        {
            FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers)).Text = "abc";
            window.Width = 390;
            parametersExpander.IsExpanded = true;
            window.UpdateLayout();
            FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers)).Text = "1,0000";
            Assert.IsFalse(model.HasError, "A hidden desktop input must not block the compact editor.");
            return Task.CompletedTask;
        });

        await Check("An inactive series input error does not remain on the single mode screen", model =>
        {
            model.IsSeriesMode = true;
            window.UpdateLayout();
            FindInput(seriesView, nameof(CalculationParameters.SeriesAngleStepDegrees)).Text = "abc";
            Assert.IsTrue(model.HasError);
            model.IsSingleMode = true;
            window.UpdateLayout();
            Assert.IsFalse(model.HasError, "An inactive series-only error must not describe the single calculation as invalid.");
            Assert.AreNotEqual("Проверьте ввод", model.StatusText, "The status bar must not keep an inactive input error.");
            model.IsSeriesMode = true;
            window.UpdateLayout();
            Assert.IsTrue(model.HasError, "Returning to the invalid series must restore its input error.");
            return Task.CompletedTask;
        });

        await Check("Two-point series is preserved by the slider", model =>
        {
            model.IsSeriesMode = true;
            model.Parameters.SeriesPointCount = 2;
            window.UpdateLayout();
            Assert.AreEqual(2, model.Parameters.SeriesPointCount);
            Slider slider = Descendants<Slider>(seriesView).Single();
            Assert.AreEqual(2.0, slider.Value, "The slider must represent every valid point count.");
            return Task.CompletedTask;
        });

        await Check("Series has a usable cancellation control at both widths", async model =>
        {
            model.IsSeriesMode = true;
            model.Parameters.SeriesPointCount = 2;
            model.Parameters.SeriesAngleStartDegrees = 40;
            model.Parameters.SeriesAngleEndDegrees = 50;
            model.Parameters.SeriesAngleStepDegrees = 10;
            window.UpdateLayout();
            Task calculation;
            SynchronizationContext? originalContext = SynchronizationContext.Current;
            try
            {
                // Keep the first calculation yield behind the UI checks, independent of CPU speed.
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(
                    window.Dispatcher, System.Windows.Threading.DispatcherPriority.SystemIdle));
                calculation = model.CalculateAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalContext);
            }
            try
            {
                foreach (double width in new[] { 1440.0, 390.0 })
                {
                    window.Width = width;
                    window.UpdateLayout();
                    await window.Dispatcher.InvokeAsync(() => { },
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Assert.IsTrue(Descendants<Button>(seriesView).Any(button => button.IsVisible &&
                        button.IsEnabled && ReferenceEquals(button.Command, model.CancelCommand)),
                        "Cancellation must be available on the running series screen at width " + width);
                    SavePreview(window, width > 1000 ? "series-running-desktop.png" : "series-running-compact.png");
                }
            }
            finally
            {
                model.CancelCommand.Execute(null);
                await calculation;
            }
            Assert.AreEqual("Расчёт отменён", model.StatusText);
            Assert.AreEqual(0, model.Runs.Count);
        });

        await Check("History cannot overwrite the active parameter set while running", async model =>
        {
            await model.CalculateAsync();
            var previousRun = model.Runs[0];
            model.Parameters.WavelengthMicrometers = 1.3;
            Task calculation = model.CalculateAsync();
            try
            {
                model.OpenRun(previousRun);
                Assert.AreEqual(1.3, model.Parameters.WavelengthMicrometers, 1e-12);
            }
            finally
            {
                model.CancelCommand.Execute(null);
                await calculation;
            }
            Assert.AreSame(previousRun, model.Runs[0]);
            Assert.AreEqual(1, model.Runs.Count);
        });

        await Check("Repeated commands start only one calculation", async model =>
        {
            var command = (AsyncRelayCommand)model.RunCommand;
            Task first = command.ExecuteAsync();
            Assert.IsFalse(command.CanExecute(null));
            await command.ExecuteAsync();
            await model.CalculateAsync();
            await first;
            Assert.AreEqual(1, model.Runs.Count);
            Assert.IsTrue(command.CanExecute(null));
        });

        await Check("History filtering does not detach the displayed result", async model =>
        {
            await model.CalculateAsync();
            model.HistorySearch = "no matching runs";
            model.Parameters.WavelengthMicrometers = 1.3;
            await model.CalculateAsync();
            Assert.AreEqual(2, model.Runs.Count);
            Assert.AreSame(model.Runs[0], model.SelectedRun);
            Assert.AreSame(model.SelectedRun!.Output!.SlicePlot, model.SlicePlot);
        });

        await Check("The calculation mode cannot change while its result is being computed", async model =>
        {
            Task calculation = model.CalculateAsync();
            try
            {
                model.IsSeriesMode = true;
                model.NavigateCommand.Execute("Series");
                Assert.IsTrue(model.IsSingleMode);
                Assert.AreEqual("Calculations", model.CurrentSection);
                model.NavigateCommand.Execute("Settings");
                Assert.AreEqual("Settings", model.CurrentSection);
                model.NavigateCommand.Execute("Calculations");
                Assert.AreEqual("Calculations", model.CurrentSection, "The user must be able to return to the running task and cancel it.");
            }
            finally
            {
                model.CancelCommand.Execute(null);
                await calculation;
            }
        });

        await Check("A new run clears invalid text and previous output", async model =>
        {
            await model.CalculateAsync();
            FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers)).Text = "abc";
            model.NewRunCommand.Execute(null);
            window.UpdateLayout();
            Assert.IsFalse(model.HasError);
            Assert.IsFalse(Validation.GetHasError(FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers))));
            Assert.IsFalse(model.Energy.IsAvailable);
            Assert.IsNull(model.SelectedRun);
            Assert.AreEqual(1, model.Runs.Count);
        });

        await Check("Input formatting does not hide valid small coordinates", model =>
        {
            model.Parameters.OutputLeft = 0.00001;
            window.UpdateLayout();
            TextBox input = FindInput(calculationsView, nameof(CalculationParameters.OutputLeft));
            Assert.AreEqual(model.Parameters.OutputLeft,
                double.Parse(input.Text, input.Language.GetSpecificCulture()), 1e-15);
            return Task.CompletedTask;
        });

        await Check("Typing decimal and exponent prefixes does not replace the active text", model =>
        {
            TextBox input = FindInput(calculationsView, nameof(CalculationParameters.WavelengthMicrometers));
            foreach (string text in new[] { "0", "0,", "0,0", "0,01", "1E", "1E-", "1E-2" })
            {
                input.Text = text;
                Assert.AreEqual(text, input.Text, "The user must be able to finish typing a number.");
            }
            Assert.IsFalse(model.HasError);
            Assert.AreEqual(0.01, model.Parameters.WavelengthMicrometers, 1e-15);
            return Task.CompletedTask;
        });

        if (failures.Count != 0)
            throw new AggregateException(failures);
    }

    private static TextBox FindInput(DependencyObject root, string propertyName) =>
        Descendants<TextBox>(root).First(input => input.IsVisible &&
            BindingOperations.GetBinding(input, TextBox.TextProperty)?.Path?.Path == "Parameters." + propertyName);

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
