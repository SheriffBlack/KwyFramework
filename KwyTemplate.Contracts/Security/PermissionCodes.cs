namespace KwyTemplate.Contracts.Security;

/// <summary>
/// 模板项目统一使用的权限码。
/// 业务模块只依赖这些契约常量，具体权限规则由 Security 模块解释。
/// </summary>
public static class PermissionCodes
{
    public const string Operator = nameof(Operator);
    public const string Engineer = nameof(Engineer);
    public const string Admin = nameof(Admin);
}
