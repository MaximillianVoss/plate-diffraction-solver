using System.Windows;
using System.Threading.Tasks;

namespace Diffraction.WpfPrototype;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Любое исключение на UI-потоке показываем в адекватном окне, а не падаем.
        e.Handled = true;
        if (e.Exception is OperationCanceledException)
        {
            MessageBox.Show(
                "Операция отменена. Результаты предыдущего успешного расчёта сохранены.",
                "Отмена операции", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show(
            $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПриложение продолжает работу.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Незавершённые фоновые задачи не должны ронять процесс.
        e.SetObserved();
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Фоновый поток: терминировать процесс в любом случае поздно,
        // оставляем запись для диагностики через журнал событий/отладчик.
        System.Diagnostics.Trace.WriteLine($"Необработанное исключение: {e.ExceptionObject}");
    }
}
