using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kwy.UI.WPF.Controls;

namespace Kwy.UI.WPF.Input.Keyboard;

/// <summary>
/// Owns and reuses one keyboard dialog for a WPF dispatcher.
/// </summary>
internal sealed class SoftKeyboardCoordinator
{
    private readonly Dispatcher dispatcher;
    private DispatcherOperation? warmUpOperation;
    private DispatcherOperation? pendingOpen;
    private WeakReference<FrameworkElement>? pendingTarget;
    private WeakReference<FrameworkElement>? activeTarget;
    private KwyKeyboardInputDialog? dialog;
    private Window? dialogOwner;

    public SoftKeyboardCoordinator(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
    }

    public void WarmUp(FrameworkElement target)
    {
        if (dialog != null
            || warmUpOperation?.Status == DispatcherOperationStatus.Pending
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        var weakTarget = new WeakReference<FrameworkElement>(target);
        warmUpOperation = dispatcher.InvokeAsync(
            () =>
            {
                warmUpOperation = null;
                if (!weakTarget.TryGetTarget(out FrameworkElement? currentTarget)
                    || Window.GetWindow(currentTarget) is not Window owner)
                {
                    return;
                }

                KwyKeyboardInputDialog inputDialog = EnsureDialog();
                SetDialogOwner(owner);
                inputDialog.Prewarm();
            },
            DispatcherPriority.ContextIdle);
    }

    public bool RequestOpen(FrameworkElement target)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished || !CanOpen(target))
        {
            return false;
        }

        pendingTarget = new WeakReference<FrameworkElement>(target);
        if (pendingOpen?.Status != DispatcherOperationStatus.Pending)
        {
            pendingOpen = dispatcher.InvokeAsync(OpenPendingTarget, DispatcherPriority.Send);
        }

        return true;
    }

    public void CloseIfOwnedBy(FrameworkElement target)
    {
        if (pendingTarget?.TryGetTarget(out FrameworkElement? pending) == true
            && ReferenceEquals(pending, target))
        {
            pendingTarget = null;
        }

        if (activeTarget?.TryGetTarget(out FrameworkElement? active) == true
            && ReferenceEquals(active, target))
        {
            dialog?.CancelSession();
        }
    }

    private void OpenPendingTarget()
    {
        pendingOpen = null;
        WeakReference<FrameworkElement>? request = pendingTarget;
        pendingTarget = null;
        if (request == null
            || !request.TryGetTarget(out FrameworkElement? target)
            || target == null
            || !CanOpen(target)
            || !TryResolveEditor(target, out TextBox editor))
        {
            return;
        }

        KwyKeyboardMode mode = target is KwyNumberBox number
            ? number.IsInteger ? KwyKeyboardMode.Integer : KwyKeyboardMode.Numeric
            : SoftKeyboardService.GetMode(target);
        bool allowNegative = target is KwyNumberBox numberInput
            ? numberInput.Minimum is null or < 0
            : SoftKeyboardService.GetAllowNegative(target);
        NumericKeyboardOptions? numericOptions = mode == KwyKeyboardMode.Full
            ? null
            : new NumericKeyboardOptions(
                mode == KwyKeyboardMode.Numeric,
                allowNegative,
                target is KwyNumberBox numeric ? numeric.DecimalPlaces : SoftKeyboardService.GetDecimalPlaces(target),
                target is KwyNumberBox bounded ? bounded.Minimum : null,
                target is KwyNumberBox boundedMaximum ? boundedMaximum.Maximum : null);

        KwyKeyboardInputDialog inputDialog = EnsureDialog();
        SetDialogOwner(Window.GetWindow(target));
        inputDialog.Text = editor.Text;
        inputDialog.KeyboardLayout = SoftKeyboardService.GetLayout(target);
        inputDialog.Mode = mode;
        inputDialog.AllowNegative = allowNegative;
        inputDialog.KeyboardWidth = SoftKeyboardService.GetWidth(target);
        inputDialog.Prepare(numericOptions);

        activeTarget = new WeakReference<FrameworkElement>(target);
        try
        {
            inputDialog.ShowDialog();
        }
        finally
        {
            activeTarget = null;
        }

        if (inputDialog.IsConfirmed && editor.IsLoaded)
        {
            editor.Text = inputDialog.Text;
            editor.CaretIndex = editor.Text.Length;
            editor.SelectionLength = 0;
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }

    private KwyKeyboardInputDialog EnsureDialog()
        => dialog ??= new KwyKeyboardInputDialog();

    private void SetDialogOwner(Window? owner)
    {
        if (ReferenceEquals(dialogOwner, owner))
        {
            return;
        }

        if (dialogOwner != null)
        {
            dialogOwner.Closing -= OnDialogOwnerClosing;
        }

        dialogOwner = owner;
        if (dialog != null)
        {
            dialog.Owner = owner;
        }

        if (dialogOwner != null)
        {
            dialogOwner.Closing += OnDialogOwnerClosing;
        }
    }

    private void OnDialogOwnerClosing(object? sender, CancelEventArgs e) => DisposeDialog();

    private void OnDispatcherShutdownStarted(object? sender, EventArgs e) => DisposeDialog();

    private void DisposeDialog()
    {
        if (warmUpOperation?.Status == DispatcherOperationStatus.Pending)
        {
            warmUpOperation.Abort();
        }

        warmUpOperation = null;
        pendingTarget = null;
        if (dialogOwner != null)
        {
            dialogOwner.Closing -= OnDialogOwnerClosing;
            dialogOwner = null;
        }

        KwyKeyboardInputDialog? current = dialog;
        dialog = null;
        current?.DisposePermanently();
    }

    private static bool CanOpen(FrameworkElement target)
    {
        if (!SoftKeyboardService.GetIsEnabled(target) || !target.IsEnabled || !target.IsVisible)
        {
            return false;
        }

        return target switch
        {
            TextBox textBox => !textBox.IsReadOnly,
            KwyNumberBox numberBox => !numberBox.IsReadOnly,
            _ => false
        };
    }

    private static bool TryResolveEditor(FrameworkElement target, out TextBox editor)
    {
        if (target is TextBox textBox)
        {
            editor = textBox;
            return true;
        }

        if (target is KwyNumberBox numberBox)
        {
            if (numberBox.Editor == null)
            {
                numberBox.ApplyTemplate();
            }

            if (numberBox.Editor is TextBox numberEditor)
            {
                editor = numberEditor;
                return true;
            }
        }

        editor = null!;
        return false;
    }
}
