namespace Kwy.MVVM.Modularity;

/// <summary>
/// 模块定义特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ModuleAttribute : Attribute
{
    // 使用 required 和 init 保证安全性，防止反射篡改.
    /// <summary>
    /// 模块名称
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// 是否按需加载
    /// </summary>
    public bool OnDemand { get; init; } = false;
}

/// <summary>
/// 模块依赖特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ModuleDependencyAttribute : Attribute
{
    /// <summary>
    /// 被依赖模块的名称
    /// </summary>
    public string ModuleName { get; }

    public ModuleDependencyAttribute(string moduleName)
    {
        ModuleName = moduleName;
    }
}
