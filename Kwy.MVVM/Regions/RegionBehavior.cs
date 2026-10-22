namespace Kwy.MVVM.Regions;

/// <summary>
/// 区域行为基类。
/// 推荐由区域管理器统一处理区域逻辑，
/// 此类保留用于兼容现有的区域行为扩展。
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
/// 区域接口。
/// </summary>
public interface IRegion
{
    string Name { get; set; }
}
