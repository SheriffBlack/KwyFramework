using System.Globalization;
using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Security;
using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authorization;

internal sealed class SecurityPermissionService : IPermissionService, IPermissionUsageNotifier, IDisposable
{
    private readonly ICurrentUserService currentUserService;
    private readonly ILocalizationService localizationService;
    private bool disposed;

    public SecurityPermissionService(ICurrentUserService currentUserService, ILocalizationService localizationService)
    {
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.currentUserService.CurrentUserChanged += OnCurrentUserChanged;
        this.localizationService.LanguageChanged += OnLanguageChanged;
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
        return TF("Security.Permission.Required", "当前操作需要 {0} 权限。", GetDisplayName(requiredLevel));
    }

    public void NotifyPermissionUsed(string permissionCode)
    {
        SecurityUserLevel requiredLevel = ParseRequiredLevel(permissionCode);
        if (requiredLevel > SecurityUserLevel.Operator && GetCurrentLevel() >= requiredLevel)
        {
            currentUserService.RefreshElevatedSession();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        currentUserService.CurrentUserChanged -= OnCurrentUserChanged;
        localizationService.LanguageChanged -= OnLanguageChanged;
    }

    private SecurityUserLevel GetCurrentLevel()
        => currentUserService.CurrentUser?.Level ?? SecurityUserLevel.Operator;

    private SecurityUserLevel ParseRequiredLevel(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return permissionCode switch
        {
            PermissionCodes.Operator => SecurityUserLevel.Operator,
            PermissionCodes.Engineer => SecurityUserLevel.Engineer,
            PermissionCodes.Admin => SecurityUserLevel.Admin,
            _ when Enum.TryParse(permissionCode, ignoreCase: true, out SecurityUserLevel level)
                && Enum.IsDefined(level) => level,
            _ => throw new InvalidOperationException(TF("Security.Permission.UnknownCode", "未知权限码：{0}。", permissionCode))
        };
    }

    private string GetDisplayName(SecurityUserLevel level)
        => level switch
        {
            SecurityUserLevel.Operator => T("Security.Role.Operator", "操作员"),
            SecurityUserLevel.Engineer => T("Security.Role.Engineer", "工程师"),
            SecurityUserLevel.Admin => T("Security.Role.Admin", "管理员"),
            _ => level.ToString()
        };

    private void OnCurrentUserChanged(object? sender, CurrentUser? user)
        => PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs());

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => PermissionsChanged?.Invoke(this, new PermissionChangedEventArgs());

    private string T(string key, string fallback)
    {
        string text = localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private string TF(string key, string fallback, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, T(key, fallback), args);
}
