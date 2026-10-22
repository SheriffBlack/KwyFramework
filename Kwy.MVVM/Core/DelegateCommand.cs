using CommunityToolkit.Mvvm.Input;

namespace Kwy.MVVM.Core;

/// <summary>
/// 无参数的委托命令，支持执行条件和状态刷新。
/// 对外提供统一的命令 API。
/// </summary>
public class DelegateCommand : IRelayCommand
{
    private readonly RelayCommand internalCommand;

    public DelegateCommand(Action execute)
    {
        internalCommand = new RelayCommand(execute);
    }

    public DelegateCommand(Action execute, Func<bool> canExecute)
    {
        internalCommand = new RelayCommand(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(object? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => internalCommand.Execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => internalCommand.CanExecuteChanged += value;
        remove => internalCommand.CanExecuteChanged -= value;
    }
}

/// <summary>
/// 带参数的委托命令。
/// </summary>
/// <typeparam name="T">参数类型</typeparam>
public class DelegateCommand<T> : IRelayCommand<T>
{
    private readonly RelayCommand<T> internalCommand;

    public DelegateCommand(Action<T?> execute)
    {
        internalCommand = new RelayCommand<T>(execute);
    }

    public DelegateCommand(Action<T?> execute, Predicate<T?> canExecute)
    {
        internalCommand = new RelayCommand<T>(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(T? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(T? parameter) => internalCommand.Execute(parameter);

    public bool CanExecute(object? parameter) => internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => internalCommand.Execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => internalCommand.CanExecuteChanged += value;
        remove => internalCommand.CanExecuteChanged -= value;
    }
}
