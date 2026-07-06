namespace Kwy.MVVM.Regions;

/// <summary>
/// 区域行为基类 (Prism 兼容层)。
/// 在 Kwy.MVVM 极简框架中，我们推荐直接在 RegionManager 中处理逻辑，
/// 但为了解决旧代码的编译引用，我们保留这个占位符。
/// </summary>
public abstract class RegionBehavior
{
    /// <summary>
    /// 行为所属的区域
    /// </summary>
    public IRegion? Region { get; set; }

    /// <summary>
    /// 当行为附加到区域时执行
    /// </summary>
    protected abstract void OnAttach();
}

/// <summary>
/// 区域接口 (Prism 兼容层)
/// </summary>
public interface IRegion
{
    string Name { get; set; }
}