using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;

namespace Kwy.MVVM.Core;

/// <summary>
/// 无参数的异步委托命令。
/// 提供异步执行、取消和状态通知能力。
/// 自带防并发执行机制（在执行 Task 期间，CanExecute 会自动返回 false），以及执行状态 (IsRunning)。
/// </summary>
public class AsyncDelegateCommand : IAsyncRelayCommand
{
    private readonly AsyncRelayCommand internalCommand;
    private readonly SynchronizationContext? synchronizationContext;

    public AsyncDelegateCommand(Func<Task> execute)
    {
        synchronizationContext = SynchronizationContext.Current;
        internalCommand = new AsyncRelayCommand(execute);
    }

    public AsyncDelegateCommand(Func<Task> execute, Func<bool> canExecute)
    {
        synchronizationContext = SynchronizationContext.Current;
        internalCommand = new AsyncRelayCommand(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged()
    {
        if (synchronizationContext == null || SynchronizationContext.Current == synchronizationContext)
        {
            internalCommand.NotifyCanExecuteChanged();
            return;
        }

        synchronizationContext.Post(static state => ((AsyncRelayCommand)state!).NotifyCanExecuteChanged(), internalCommand);
    }

    public bool CanExecute(object? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => internalCommand.Execute(parameter);

    public Task ExecuteAsync(object? parameter) => internalCommand.ExecuteAsync(parameter);

    public void Cancel() => internalCommand.Cancel();

    public Task? ExecutionTask => internalCommand.ExecutionTask;

    public bool CanBeCanceled => internalCommand.CanBeCanceled;

    public bool IsCancellationRequested => internalCommand.IsCancellationRequested;

    public bool IsRunning => internalCommand.IsRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => internalCommand.CanExecuteChanged += value;
        remove => internalCommand.CanExecuteChanged -= value;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => internalCommand.PropertyChanged += value;
        remove => internalCommand.PropertyChanged -= value;
    }
}

/// <summary>
/// 带参数的异步委托命令。
/// </summary>
/// <typeparam name="T">参数类型</typeparam>
public class AsyncDelegateCommand<T> : IAsyncRelayCommand<T>
{
    private readonly AsyncRelayCommand<T> internalCommand;
    private readonly SynchronizationContext? synchronizationContext;

    public AsyncDelegateCommand(Func<T?, Task> execute)
    {
        synchronizationContext = SynchronizationContext.Current;
        internalCommand = new AsyncRelayCommand<T>(execute);
    }

    public AsyncDelegateCommand(Func<T?, Task> execute, Predicate<T?> canExecute)
    {
        synchronizationContext = SynchronizationContext.Current;
        internalCommand = new AsyncRelayCommand<T>(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged()
    {
        if (synchronizationContext == null || SynchronizationContext.Current == synchronizationContext)
        {
            internalCommand.NotifyCanExecuteChanged();
            return;
        }

        synchronizationContext.Post(static state => ((AsyncRelayCommand<T>)state!).NotifyCanExecuteChanged(), internalCommand);
    }

    public bool CanExecute(T? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(T? parameter) => internalCommand.Execute(parameter);

    public bool CanExecute(object? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => internalCommand.Execute(parameter);

    public Task ExecuteAsync(T? parameter) => internalCommand.ExecuteAsync(parameter);

    public Task ExecuteAsync(object? parameter) => internalCommand.ExecuteAsync(parameter);

    public void Cancel() => internalCommand.Cancel();

    public Task? ExecutionTask => internalCommand.ExecutionTask;

    public bool CanBeCanceled => internalCommand.CanBeCanceled;

    public bool IsCancellationRequested => internalCommand.IsCancellationRequested;

    public bool IsRunning => internalCommand.IsRunning;

    public event EventHandler? CanExecuteChanged
    {
        add => internalCommand.CanExecuteChanged += value;
        remove => internalCommand.CanExecuteChanged -= value;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => internalCommand.PropertyChanged += value;
        remove => internalCommand.PropertyChanged -= value;
    }
}
