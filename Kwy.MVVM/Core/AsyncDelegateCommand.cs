using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace Kwy.MVVM.Core;

/// <summary>
/// 无参数的异步委托命令。这是 Prism 中一直相对欠缺但在现代 MVVM 中极其重要的部分。
/// 内部组合包装了 CommunityToolkit.Mvvm 的 AsyncRelayCommand。
/// 自带防并发执行机制（在执行 Task 期间，CanExecute 会自动返回 false），以及执行状态 (IsRunning)。
/// </summary>
public class AsyncDelegateCommand : IAsyncRelayCommand
{
    private readonly AsyncRelayCommand _internalCommand;

    public AsyncDelegateCommand(Func<Task> execute)
    {
        _internalCommand = new AsyncRelayCommand(execute);
    }

    public AsyncDelegateCommand(Func<Task> execute, Func<bool> canExecute)
    {
        _internalCommand = new AsyncRelayCommand(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(object? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _internalCommand.Execute(parameter);

    public Task ExecuteAsync(object? parameter) => _internalCommand.ExecuteAsync(parameter);

    public void Cancel() => _internalCommand.Cancel();

    public Task? ExecutionTask => _internalCommand.ExecutionTask;

    public bool CanBeCanceled => _internalCommand.CanBeCanceled;

    public bool IsCancellationRequested => _internalCommand.IsCancellationRequested;

    public bool IsRunning => _internalCommand.IsRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => _internalCommand.CanExecuteChanged += value;
        remove => _internalCommand.CanExecuteChanged -= value;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _internalCommand.PropertyChanged += value;
        remove => _internalCommand.PropertyChanged -= value;
    }
}

/// <summary>
/// 带参数的异步委托命令。
/// </summary>
/// <typeparam name="T">参数类型</typeparam>
public class AsyncDelegateCommand<T> : IAsyncRelayCommand<T>
{
    private readonly AsyncRelayCommand<T> _internalCommand;

    public AsyncDelegateCommand(Func<T?, Task> execute)
    {
        _internalCommand = new AsyncRelayCommand<T>(execute);
    }

    public AsyncDelegateCommand(Func<T?, Task> execute, Predicate<T?> canExecute)
    {
        _internalCommand = new AsyncRelayCommand<T>(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(T? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(T? parameter) => _internalCommand.Execute(parameter);

    public bool CanExecute(object? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _internalCommand.Execute(parameter);

    public Task ExecuteAsync(T? parameter) => _internalCommand.ExecuteAsync(parameter);

    public Task ExecuteAsync(object? parameter) => _internalCommand.ExecuteAsync(parameter);

    public void Cancel() => _internalCommand.Cancel();

    public Task? ExecutionTask => _internalCommand.ExecutionTask;

    public bool CanBeCanceled => _internalCommand.CanBeCanceled;

    public bool IsCancellationRequested => _internalCommand.IsCancellationRequested;

    public bool IsRunning => _internalCommand.IsRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => _internalCommand.CanExecuteChanged += value;
        remove => _internalCommand.CanExecuteChanged -= value;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _internalCommand.PropertyChanged += value;
        remove => _internalCommand.PropertyChanged -= value;
    }
}