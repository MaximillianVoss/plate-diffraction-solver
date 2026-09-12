using System.Windows.Input;

namespace Diffraction.WpfPrototype.Infrastructure;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Action<Exception> _onError;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Action<Exception> onError, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
        => await ExecuteAsync(parameter);

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter))
            return;

        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            _onError(ex);
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
