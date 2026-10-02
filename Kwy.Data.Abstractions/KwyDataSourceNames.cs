namespace Kwy.Data.Abstractions;

/// <summary>
/// 提供数据源的通用逻辑名称。业务模块的 History、Mes 等名称应在各自模块中定义。
/// </summary>
public static class KwyDataSourceNames
{
    /// <summary>单数据源应用或应用主数据库使用的默认名称。</summary>
    public const string Default = "Default";
}
