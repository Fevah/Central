using System;
using System.Windows.Input;

namespace Central.Desktop.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Func<System.Threading.Tasks.Task>? _asyncAction;
    private readonly Action? _action;

    public RelayCommand(Action action)          => _action = action;
    public RelayCommand(Func<System.Threading.Tasks.Task> asyncAction) => _asyncAction = asyncAction;

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try
        {
            if (_asyncAction != null) await _asyncAction();
            else _action?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RelayCommand error: {ex.Message}");
            try { Central.Data.AppLogger.LogException("Command", ex, "RelayCommand.Execute"); } catch { }
        }
    }

    public event EventHandler? CanExecuteChanged;
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _action;
    public RelayCommand(Action<T?> action) => _action = action;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action(parameter is T t ? t : default);
    public event EventHandler? CanExecuteChanged;
}
