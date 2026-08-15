namespace Kwy.MVVM.Core;

/// <summary>
/// 授权结果。
/// </summary>
public sealed record AuthorizationResult(bool Succeeded, string? FailureMessage = null)
{
    public static AuthorizationResult Success() => new(true);

    public static AuthorizationResult Failure(string? message = null) => new(false, message);
}

/// <summary>
/// 权限变更事件参数。
/// </summary>
public sealed class PermissionChangedEventArgs : EventArgs
{
    public PermissionChangedEventArgs(string? permissionCode = null)
    {
        PermissionCode = permissionCode;
    }

    /// <summary>
    /// 为空表示全量刷新。
    /// </summary>
    public string? PermissionCode { get; }
}

/// <summary>
/// 当前用户权限服务。业务实现应缓存当前用户权限，并在权限变化时触发 PermissionsChanged。
/// </summary>
public interface IPermissionService
{
    event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

    bool HasPermission(string permissionCode);

    string GetNoPermissionMessage(string permissionCode);
}

/// <summary>
/// Optional capability for permission services that track successful privileged-operation activity.
/// Permission queries must remain side-effect free; callers invoke this only after an operation
/// has been authorized for execution.
/// </summary>
public interface IPermissionUsageNotifier
{
    void NotifyPermissionUsed(string permissionCode);
}

/// <summary>
/// 通用授权服务。用于业务操作层的最终校验，可携带资源上下文。
/// </summary>
public interface IAuthorizationService
{
    ValueTask<AuthorizationResult> AuthorizeAsync(
        string policy,
        object? resource = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认授权服务，基于 IPermissionService 进行策略名校验。
/// </summary>
public sealed class PermissionAuthorizationService : IAuthorizationService
{
    private readonly IPermissionService permissionService;

    public PermissionAuthorizationService(IPermissionService permissionService)
    {
        this.permissionService = permissionService;
    }

    public ValueTask<AuthorizationResult> AuthorizeAsync(
        string policy,
        object? resource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);

        return ValueTask.FromResult(
            permissionService.HasPermission(policy)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failure(permissionService.GetNoPermissionMessage(policy)));
    }
}
