using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kwy.MVVM.Core;

/// <summary>
/// MVVM 绑定基类，提供属性变更通知和生命周期释放能力。
/// 对外统一暴露 Kwy.MVVM 的基础绑定 API。
/// 适用于不使用源生成器，喜欢传统手写 SetProperty 模式的团队。
/// </summary>
public abstract class BindableBase : ObservableObject, IDisposable
{
    private CancellationTokenSource? destroyCts;
    private bool isDisposed;

    /// <summary>
    /// 获取当前 ViewModel 的销毁令牌。
    /// 当当前实例被释放时，该令牌会自动触发取消。
    /// </summary>
    protected CancellationToken DestroyToken
        => isDisposed ? new CancellationToken(canceled: true) : (destroyCts ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// 触发指定属性的变更通知。
    /// 可以主动触发指定属性的更新通知。
    /// </summary>
    /// <param name="propertyName">属性的名称。如为空，将自动推断为调用处的属性名。</param>
    public void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// 用于在属性设置器中更新字段并发出变更通知。
    /// 如果新旧值不同，则更新字段，并触发对应的属性变动事件。
    /// </summary>
    public new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        return base.SetProperty(ref storage, value, propertyName);
    }

    /// <summary>
    /// 增强版的 SetProperty，允许在属性发生实际变动后，自动执行特定的回调逻辑。
    /// 属性发生实际变化后调用指定回调。
    /// </summary>
    public bool SetProperty<T>(ref T storage, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
    {
        if (base.SetProperty(ref storage, value, propertyName))
        {
            onChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 由实例的生命周期所有者在销毁时调用。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 供子类重写（例如清理事件订阅），但必须调用 base.Dispose(disposing) 以确保取消令牌被释放。
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || isDisposed)
        {
            return;
        }

        isDisposed = true;
        destroyCts?.Cancel();
        destroyCts?.Dispose();
        destroyCts = null;
    }
}
