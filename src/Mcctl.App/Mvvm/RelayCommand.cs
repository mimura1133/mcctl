using System.Windows.Input;

namespace Mcctl.App.Mvvm;

/// <summary>Synchronous command without a parameter (always enabled).</summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

public sealed class RelayCommand<T>(Action<T> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => parameter is T;

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            execute(value);
        }
    }
}

/// <summary>
/// Async command; while it runs it reports CanExecute = false, and errors are routed to
/// <see cref="UnhandledError"/> instead of crashing the app (ICommand.Execute is async void).
/// </summary>
public class AsyncCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    private bool _running;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public static event Action<Exception>? UnhandledError;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await execute(parameter);
        }
        catch (Exception ex)
        {
            Report(ex);
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    internal static void Report(Exception ex) => UnhandledError?.Invoke(ex);
}

/// <summary>Async command with a typed CommandParameter; other parameter types are ignored.</summary>
public sealed class AsyncCommand<T>(Func<T, Task> execute)
    : AsyncCommand(p => p is T value ? execute(value) : Task.CompletedTask, p => p is T);
