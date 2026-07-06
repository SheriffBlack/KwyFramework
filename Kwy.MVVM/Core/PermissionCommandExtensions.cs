using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Input;

namespace Kwy.MVVM.Core;

/// <summary>
/// 权限检查模式
/// </summary>
public enum PermissionCheckMode
{
    /// <summary>
    /// 禁用模式：没有权限时 CanExecute 返回 false (按钮变灰)
    /// </summary>
    Disable,

    /// <summary>
    /// 提示模式：按钮可点，但执行时检查权限并弹窗提示 (主要用于 Command)
    /// </summary>
    Prompt,

    /// <summary>
    /// 隐藏模式：没有权限时控件完全隐藏
    /// </summary>
    Hide,

    /// <summary>
    /// 组合模式：同时禁用且隐藏
    /// </summary>
    Both
}

/// <summary>
/// 权限命令装饰器。
/// </summary>
public class PermissionCommandDecorator : ICommand, IDisposable
{
    private readonly ICommand innerCommand;
    private readonly string permissionCode;
    private readonly PermissionCheckMode mode;
    private readonly IPermissionService? permissionService;
    private bool disposed;

    private event EventHandler? canExecuteChanged;

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            innerCommand.CanExecuteChanged += value;
            canExecuteChanged += value;
        }
        remove
        {
            innerCommand.CanExecuteChanged -= value;
            canExecuteChanged -= value;
        }
    }

    public PermissionCommandDecorator(
        ICommand innerCommand,
        string permissionCode,
        PermissionCheckMode mode = PermissionCheckMode.Disable,
        IPermissionService? permissionService = null)
    {
        this.innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        this.permissionCode = permissionCode;
        this.mode = mode;
        this.permissionService = permissionService ?? ResolvePermissionService();

        if (this.permissionService != null)
        {
            this.permissionService.PermissionsChanged += OnPermissionsChanged;
        }
    }

    public bool CanExecute(object? parameter)
    {
        if (mode is PermissionCheckMode.Disable or PermissionCheckMode.Both
            && permissionService?.HasPermission(permissionCode) == false)
        {
            return false;
        }

        return innerCommand.CanExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        if (permissionService?.HasPermission(permissionCode) == false)
        {
            return;
        }

        innerCommand.Execute(parameter);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (permissionService != null)
        {
            permissionService.PermissionsChanged -= OnPermissionsChanged;
        }

        GC.SuppressFinalize(this);
    }

    private void OnPermissionsChanged(object? sender, PermissionChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PermissionCode)
            && !string.Equals(e.PermissionCode, permissionCode, StringComparison.Ordinal))
        {
            return;
        }

        if (innerCommand is IRelayCommand relayCommand)
        {
            relayCommand.NotifyCanExecuteChanged();
            return;
        }

        canExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IPermissionService? ResolvePermissionService()
    {
        var provider = KwyContainer.Current;
        if (provider == null)
        {
            return null;
        }

        return provider.GetService<IPermissionService>();
    }
}

public static class PermissionCommandExtensions
{
    public static ICommand WithPermission(
        this ICommand command,
        string permissionCode,
        PermissionCheckMode mode = PermissionCheckMode.Disable)
        => new PermissionCommandDecorator(command, permissionCode, mode);

    public static ICommand WithPermission(
        this ICommand command,
        IPermissionService permissionService,
        string permissionCode,
        PermissionCheckMode mode = PermissionCheckMode.Disable)
        => new PermissionCommandDecorator(command, permissionCode, mode, permissionService);
}
