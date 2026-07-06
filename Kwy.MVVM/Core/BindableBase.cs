using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kwy.MVVM.Core;

/// <summary>
/// MVVM 绑定的基类，对标 Prism 的 BindableBase。
/// 内部继承自 CommunityToolkit.Mvvm 的 ObservableObject，但在外部只暴露 Kwy.MVVM 的命名空间。
/// 适用于不使用源生成器，喜欢传统手写 SetProperty 模式的团队。
/// </summary>
public abstract class BindableBase : ObservableObject, IDisposable
{
    private CancellationTokenSource? destroyCts;
    private bool isDisposed;

    /// <summary>
    /// 获取当前 ViewModel 的销毁令牌。
    /// 当视图关闭并被 ViewCacheManager 物理回收时，该令牌会自动触发取消。
    /// </summary>
    protected CancellationToken DestroyToken
        => isDisposed ? new CancellationToken(canceled: true) : (destroyCts ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// 对标 Prism 的 RaisePropertyChanged，内部调用 Toolkit 的 OnPropertyChanged。
    /// 可以主动触发指定属性的更新通知。
    /// </summary>
    /// <param name="propertyName">属性的名称。如为空，将自动推断为调用处的属性名。</param>
    public void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// 提供与 Prism 一致的行为和名字，用于在属性 setter 中调用。
    /// 如果新旧值不同，则更新字段，并触发对应的属性变动事件。
    /// </summary>
    public new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        return base.SetProperty(ref storage, value, propertyName);
    }

    /// <summary>
    /// 增强版的 SetProperty，允许在属性发生实际变动后，自动执行特定的回调逻辑。
    /// 相当于 Prism 的 `SetProperty(ref storage, value, onChangedCallback);`
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
    /// 实现接口，由系统的容器或 ViewCacheManager 在销毁时显式调用。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 供子类重写（例如清理 Plot 的渲染事件），但必须调用 base.Dispose(disposing) 以确保线程取消。
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
