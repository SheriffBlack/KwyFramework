using CommunityToolkit.Mvvm.Input;

namespace Kwy.MVVM.Core;

/// <summary>
/// 无参数的委托命令，对标 Prism 的 DelegateCommand。
/// 内部组合包装了 CommunityToolkit.Mvvm 的 RelayCommand。
/// </summary>
public class DelegateCommand : IRelayCommand
{
    private readonly RelayCommand _internalCommand;

    public DelegateCommand(Action execute)
    {
        _internalCommand = new RelayCommand(execute);
    }

    public DelegateCommand(Action execute, Func<bool> canExecute)
    {
        _internalCommand = new RelayCommand(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(object? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _internalCommand.Execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => _internalCommand.CanExecuteChanged += value;
        remove => _internalCommand.CanExecuteChanged -= value;
    }
}

/// <summary>
/// 带参数的委托命令，对标 Prism 的 DelegateCommand&lt;T&gt;。
/// </summary>
/// <typeparam name="T">参数类型</typeparam>
public class DelegateCommand<T> : IRelayCommand<T>
{
    private readonly RelayCommand<T> _internalCommand;

    public DelegateCommand(Action<T?> execute)
    {
        _internalCommand = new RelayCommand<T>(execute);
    }

    public DelegateCommand(Action<T?> execute, Predicate<T?> canExecute)
    {
        _internalCommand = new RelayCommand<T>(execute, canExecute);
    }

    public void RaiseCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public void NotifyCanExecuteChanged() => _internalCommand.NotifyCanExecuteChanged();

    public bool CanExecute(T? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(T? parameter) => _internalCommand.Execute(parameter);

    public bool CanExecute(object? parameter) => _internalCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _internalCommand.Execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => _internalCommand.CanExecuteChanged += value;
        remove => _internalCommand.CanExecuteChanged -= value;
    }
}