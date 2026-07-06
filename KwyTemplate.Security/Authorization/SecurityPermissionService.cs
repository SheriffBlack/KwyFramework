using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Security;
using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authorization;

internal sealed class SecurityPermissionService : IPermissionService, IDisposable
{
    private readonly ICurrentUserService currentUserService;
    private bool disposed;

    public SecurityPermissionService(ICurrentUserService currentUserService)
    {
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        this.currentUserService.CurrentUserChanged += OnCurrentUserChanged;
    }

    public event EventHandler<PermissionChangedEventArgs>? PermissionsChanged;

    public bool HasPermission(string permissionCode)
    {
        SecurityUserLevel requiredLevel = ParseRequiredLevel(permissionCode);
        return GetCurrentLevel() >= requiredLevel;
    }

    public string GetNoPermissionMessage(string permissionCode)
    {
        SecurityUserLevel requiredLevel = ParseRequiredLevel(permissionCode);
        return $"当前操作需要 {GetDisplayName(requiredLevel)} 权限。";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        currentUserService.CurrentUserChanged -= OnCurrentUserChanged;
    }

    private SecurityUserLevel GetCurrentLevel()
        => currentUserService.CurrentUser?.Level ?? SecurityUserLevel.Operator;

    private static SecurityUserLevel ParseRequiredLevel(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return permissionCode switch
        {
            PermissionCodes.Operator => SecurityUserLevel.Operator,
            PermissionCodes.Engineer => SecurityUserLevel.Engineer,
            PermissionCodes.Admin => SecurityUserLevel.Admin,
            _ when Enum.TryParse(permissionCode, ignoreCase: true, out SecurityUserLevel level)
                && Enum.IsDefined(level) => level,
            _ => throw new InvalidOperationException($"未知权限码：{permissionCode}。")
        };
    }

    private static string GetDisplayName(SecurityUserLevel level)
        => level switch
        {
            SecurityUserLevel.Operator => "操作员",
            SecurityUserLevel.Engineer => "工程师",
            SecurityUserLevel.Admin => "管理员",
            _ => level.ToString()
        };

    private void OnCurrentUserChanged(object? sender, CurrentUser? user)
        => PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs());
}
