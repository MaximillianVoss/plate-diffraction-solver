using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using Diffraction.WpfPrototype.Models;
using Diffraction.WpfPrototype.Services;
using Diffraction.WpfPrototype.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Diffraction.WpfPrototype.Tests;

[TestClass]
public sealed class ExportResultSnapshotTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ChangingSkinAndAngleAfterCalculationKeepsOriginalExportParameters(bool includeSeries)
    {
        RunOnDispatcher(async () =>
        {
            MainViewModel model = CreateModel(includeSeries);
            await model.CalculateAsync();
            Assert.IsFalse(model.HasError, model.ErrorMessage);
            Assert.AreEqual(1, model.Runs.Count);
            CalculationRun run = model.Runs[0];
            CalculationParameters original = run.Parameters.Clone();
            string summary = "Параметры: " + run.FullParameterSummary;

            ChangeDraft(model);
            model.IsSingleMode = includeSeries;
            AssertExportsMatchResult(model, run, original, summary);

            model.Parameters.IncidenceAngleDegrees = 0;
            await model.CalculateAsync();
            Assert.IsTrue(model.HasError);
            Assert.AreSame(run.Output!.Energy, model.Energy);
            AssertExportsMatchResult(model, run, original, summary);

            model.SelectedRun = null;
            AssertExportsMatchResult(model, run, original, summary);

            var parametersOnlyRun = new CalculationRun
            {
                RunNumber = 99,
                DateLabel = "Parameters only",
                Parameters = model.Parameters.Clone(),
                IsSeries = !includeSeries,
                Backend = "CPU",
                Status = "No output",
                StatusKind = "Warning"
            };
            model.OpenRun(parametersOnlyRun);
            Assert.AreSame(run.Output.Energy, model.Energy);
            AssertExportsMatchResult(model, run, original, summary);

            model.NewRunCommand.Execute(null);
            Assert.IsFalse(model.ExportCommand.CanExecute(null));
            Assert.IsFalse(model.ExportPackageCommand.CanExecute(null));
            Assert.ThrowsException<InvalidOperationException>(() => Invoke(model, "BuildSingleRunExportCsv"));
            AssertEmptyPlotUnits(model);
        });
    }

    [TestMethod]
    public void ExportAfterReopeningHistoryUsesThatResultsSnapshotInsteadOfLatestRunOrDraft()
    {
        RunOnDispatcher(async () =>
        {
            MainViewModel model = CreateModel(includeSeries: true);
            await model.CalculateAsync();
            Assert.IsFalse(model.HasError, model.ErrorMessage);
            CalculationRun originalRun = model.Runs[0];
            CalculationParameters original = originalRun.Parameters.Clone();
            string summary = "Параметры: " + originalRun.FullParameterSummary;

            model.IsSingleMode = true;
            model.Parameters.SkinDepthMicrometers = 0.04;
            model.Parameters.IncidenceAngleDegrees = 65;
            await model.CalculateAsync();
            Assert.IsFalse(model.HasError, model.ErrorMessage);
            Assert.AreEqual(2, model.Runs.Count);
            CalculationRun latest = model.Runs[0];
            Assert.AreNotSame(originalRun.Output, latest.Output);

            model.OpenRun(originalRun);
            ChangeDraft(model);
            Assert.AreSame(originalRun.Output!.Energy, model.Energy);
            AssertExportsMatchResult(model, originalRun, original, summary);

            // The export owns a copy even if the mutable history parameter object changes later.
            originalRun.Parameters.SkinDepthMicrometers = 0.08;
            originalRun.Parameters.IncidenceAngleDegrees = 80;
            AssertExportsMatchResult(model, originalRun, original, summary);

            model.OpenRun(latest);
            ChangeDraft(model);
            AssertExportsMatchResult(model, latest, latest.Parameters.Clone(), "Параметры: " + latest.FullParameterSummary);
        });
    }

    [TestMethod]
    public void EmptyEnergyPlotsKeepDimensionalUnitsBeforeAndAfterReset()
    {
        var model = new MainViewModel();
        AssertEmptyPlotUnits(model);
        model.NewRunCommand.Execute(null);
        AssertEmptyPlotUnits(model);
    }

    [DataTestMethod]
    [DataRow(0.0, "T_n")]
    [DataRow(0.01, "P_n")]
    public void CoefficientBasisFollowsResultWhenDraftCrossesPecBoundary(double skinDepth, string basis)
    {
        RunOnDispatcher(async () =>
        {
            MainViewModel model = CreateModel(includeSeries: false);
            model.Parameters.SkinDepthMicrometers = skinDepth;
            await model.CalculateAsync();
            Assert.IsFalse(model.HasError, model.ErrorMessage);
            CalculationRun run = model.Runs[0];
            model.Parameters.SkinDepthMicrometers = skinDepth == 0 ? 0.01 : 0;
            StringAssert.Contains(model.Energy.CoefficientBasisDisplay, basis);
            AssertExportsMatchResult(model, run, run.Parameters.Clone(), "Параметры: " + run.FullParameterSummary);
            model.NewRunCommand.Execute(null);
            StringAssert.Contains(model.Energy.CoefficientBasisDisplay, "ещё не рассчитаны");
            model.OpenRun(run);
            StringAssert.Contains(model.Energy.CoefficientBasisDisplay, basis);
        });
    }

    [TestMethod]
    public void BoundaryDiagnosticWarningPreventsGreenHistoryDespitePassingEnergyBalance()
    {
        RunOnDispatcher(async () =>
        {
            MainViewModel model = CreateModel(includeSeries: false);
            model.Parameters.HarmonicCount = 10;
            model.Parameters.IncidenceAngleDegrees = 45;
            await model.CalculateAsync();
            Assert.IsFalse(model.HasError, model.ErrorMessage);
            Assert.IsTrue(model.Energy.IsWithinTolerance);
            Assert.IsTrue(model.Diagnostics.Any(row => row.Status == "Проверить"));
            Assert.AreEqual("Предупреждение", model.Runs[0].Status);
            Assert.AreEqual("Warning", model.Runs[0].StatusKind);
        });
    }

    private static MainViewModel CreateModel(bool includeSeries)
    {
        var model = new MainViewModel { IsSeriesMode = includeSeries, ExportIncludePng = false };
        model.Parameters.HarmonicCount = 6;
        model.Parameters.SkinDepthMicrometers = 0.01;
        model.Parameters.IncidenceAngleDegrees = 35;
        model.Parameters.SeriesSkinDepthStart = 0.002;
        model.Parameters.SeriesSkinDepthEnd = 0.014;
        model.Parameters.SeriesPointCount = 2;
        model.Parameters.SeriesAngleStartDegrees = 20;
        model.Parameters.SeriesAngleEndDegrees = 40;
        model.Parameters.SeriesAngleStepDegrees = 20;
        return model;
    }

    private static void ChangeDraft(MainViewModel model)
    {
        model.Parameters.SkinDepthMicrometers = 0.07;
        model.Parameters.IncidenceAngleDegrees = 77;
        model.Parameters.SeriesSkinDepthStart = 0.03;
        model.Parameters.SeriesSkinDepthEnd = 0.09;
        model.Parameters.SeriesPointCount = 4;
        model.Parameters.SeriesAngleStartDegrees = 60;
        model.Parameters.SeriesAngleEndDegrees = 80;
        model.Parameters.SeriesAngleStepDegrees = 5;
    }

    private static void AssertExportsMatchResult(
        MainViewModel model, CalculationRun run, CalculationParameters parameters, string summary)
    {
        CalculationOutput output = run.Output!;
        CultureInfo russian = CultureInfo.GetCultureInfo("ru-RU");
        Assert.AreEqual($"Выбранная точка: δ = {parameters.SkinDepthMicrometers.ToString("G6", russian)} мкм", model.SeriesPointTitle);
        string expectedAngleCaption = output.AngleStudySkinRows.Count > 0
            ? $"Контрольная серия: δ={parameters.SkinDepthMicrometers.ToString("G6", russian)} мкм, N={parameters.HarmonicCount}. " +
              $"Углы {parameters.SeriesAngleStartDegrees.ToString("G6", russian)}…{parameters.SeriesAngleEndDegrees.ToString("G6", russian)}° с шагом {parameters.SeriesAngleStepDegrees.ToString("G6", russian)}°."
            : "Серия по углу θ не рассчитана.";
        Assert.AreEqual(expectedAngleCaption, model.SeriesAngleCaption);
        string single = StudyCsvExporter.BuildSingleRunCsv(new EnergyStudyRow(output.Energy.SkinDepth, output.Energy), summary);
        Assert.AreEqual(single, Invoke(model, "BuildSingleRunExportCsv"));
        string? skin = null;
        string? angle = null;
        if (output.SkinDepthStudyRows.Count > 0)
        {
            skin = StudyCsvExporter.BuildSkinDepthStudyCsv(output.SkinDepthStudyRows, summary);
            Assert.AreEqual(skin, Invoke(model, "BuildSkinDepthStudyExportCsv"));
        }
        if (output.AngleStudySkinRows.Count > 0)
        {
            angle = StudyCsvExporter.BuildAngleStudyCsv(
                output.AngleStudyIdealRows, output.AngleStudySkinRows, parameters.SkinDepthMicrometers, summary);
            Assert.AreEqual(angle, Invoke(model, "BuildAngleStudyExportCsv"));
        }

        string path = Path.Combine(Path.GetTempPath(), "Diffraction.ExportResultTests-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            Invoke(model, "BuildExportPackage", path);
            using ZipArchive zip = ZipFile.OpenRead(path);
            Assert.AreEqual(single, ReadEntry(zip, "csv/single_run_energy.csv"));
            if (skin is not null)
                Assert.AreEqual(skin, ReadEntry(zip, "csv/skin_depth_study.csv"));
            else
                Assert.IsNull(zip.GetEntry("csv/skin_depth_study.csv"));
            if (angle is not null)
                Assert.AreEqual(angle, ReadEntry(zip, "csv/angle_study.csv"));
            else
                Assert.IsNull(zip.GetEntry("csv/angle_study.csv"));

            using JsonDocument actual = JsonDocument.Parse(ReadEntry(zip, "parameters.json"));
            using JsonDocument expected = JsonDocument.Parse(
                ExportPackageBuilder.BuildParametersJson(parameters, output.BackendName, DateTime.UnixEpoch));
            Assert.AreEqual(expected.RootElement.GetProperty("parameters").GetRawText(),
                actual.RootElement.GetProperty("parameters").GetRawText());
            Assert.AreEqual(expected.RootElement.GetProperty("solver").GetRawText(),
                actual.RootElement.GetProperty("solver").GetRawText());
            Assert.AreEqual(ExportPackageBuilder.BuildPlotCsv("current_energy", output.CurrentEnergyPlot),
                ReadEntry(zip, "csv/plots/current_energy.csv"));
            StringAssert.Contains(ReadEntry(zip, "svg/current_energy.svg"), "<circle");
            string report = ReadEntry(zip, "report.txt");
            StringAssert.Contains(report, "P_ext / I_plate (независимый интеграл)");
            StringAssert.Contains(report, "Оптическая теорема, % от P_ext");
            StringAssert.Contains(report, "C_back / C_forward / C_abs / C_ext, мкм");
            StringAssert.Contains(report, "Базис выбранного расчёта: " + output.Energy.CoefficientBasisDisplay);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string ReadEntry(ZipArchive zip, string name)
    {
        ZipArchiveEntry? entry = zip.GetEntry(name);
        Assert.IsNotNull(entry, name);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static object? Invoke(MainViewModel model, string methodName, params object[] arguments)
    {
        MethodInfo? method = typeof(MainViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        try
        {
            return method.Invoke(model, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void AssertEmptyPlotUnits(MainViewModel model)
    {
        foreach (PlotData plot in new[] { model.CurrentEnergyPlot, model.SkinEnergyPlot, model.AngleEnergyPlot })
        {
            Assert.IsFalse(plot.HasData);
            Assert.AreEqual("Сечение, мкм", plot.YAxisTitle);
        }
    }

    private static void RunOnDispatcher(Func<Task> scenario)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await scenario();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }));
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90)), "Export snapshot test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
