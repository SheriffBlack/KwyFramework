using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// 弹窗显示期间，守住主窗口的焦点入口，防止用户点击主窗口时把弹窗弄成失焦状态。
/// </summary>
internal sealed class DialogOwnerFocusGuard : IDisposable
{
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivateAndEat = 4;

    private readonly Window dialog;
    private readonly IntPtr ownerHandle;
    private HwndSource? ownerSource;

    private DialogOwnerFocusGuard(Window dialog, IntPtr ownerHandle, HwndSource ownerSource)
    {
        this.dialog = dialog;
        this.ownerHandle = ownerHandle;
        this.ownerSource = ownerSource;
        ownerSource.AddHook(OwnerWindowProc);
        dialog.Activated += OnDialogActivated;
    }

    public static IDisposable? Attach(Window dialog)
    {
        Window? owner = dialog.Owner;
        if (owner == null)
        {
            return null;
        }

        IntPtr ownerHandle = new WindowInteropHelper(owner).Handle;
        if (ownerHandle == IntPtr.Zero || HwndSource.FromHwnd(ownerHandle) is not HwndSource ownerSource)
        {
            return null;
        }

        return new DialogOwnerFocusGuard(dialog, ownerHandle, ownerSource);
    }

    public void Dispose()
    {
        dialog.Activated -= OnDialogActivated;
        ownerSource?.RemoveHook(OwnerWindowProc);
        ownerSource = null;
    }

    private void OnDialogActivated(object? sender, EventArgs e)
        => _ = EnableWindow(ownerHandle, true);

    private IntPtr OwnerWindowProc(
        IntPtr windowHandle,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter,
        ref bool handled)
    {
        if (message != WmMouseActivate)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(MaNoActivateAndEat);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(
        IntPtr windowHandle,
        [MarshalAs(UnmanagedType.Bool)] bool enable);
}
