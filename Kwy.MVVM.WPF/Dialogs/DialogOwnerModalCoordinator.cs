using System.Runtime.CompilerServices;
using System.Windows;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// 协调同一宿主窗口上的多个异步对话框，确保最后一个对话框关闭后才恢复宿主交互。
/// </summary>
internal static class DialogOwnerModalCoordinator
{
    private static readonly ConditionalWeakTable<Window, OwnerState> OwnerStates = new();

    public static IDisposable? Acquire(Window? owner, Window dialog)
    {
        if (owner == null || ReferenceEquals(owner, dialog))
        {
            return null;
        }

        return OwnerStates.GetValue(owner, static _ => new OwnerState()).Acquire(owner);
    }

    private sealed class OwnerState
    {
        private int activeDialogCount;
        private bool changedOwnerState;
        private bool hadLocalIsEnabledValue;
        private object? localIsEnabledValue;

        public IDisposable Acquire(Window owner)
        {
            if (activeDialogCount++ == 0 && owner.IsEnabled)
            {
                object localValue = owner.ReadLocalValue(UIElement.IsEnabledProperty);
                hadLocalIsEnabledValue = localValue != DependencyProperty.UnsetValue;
                localIsEnabledValue = hadLocalIsEnabledValue ? localValue : null;
                owner.IsEnabled = false;
                changedOwnerState = true;
            }

            return new Lease(this, owner);
        }

        private void Release(Window owner)
        {
            if (activeDialogCount == 0 || --activeDialogCount != 0 || !changedOwnerState)
            {
                return;
            }

            if (hadLocalIsEnabledValue)
            {
                owner.SetValue(UIElement.IsEnabledProperty, localIsEnabledValue);
            }
            else
            {
                owner.ClearValue(UIElement.IsEnabledProperty);
            }

            changedOwnerState = false;
            hadLocalIsEnabledValue = false;
            localIsEnabledValue = null;
        }

        private sealed class Lease : IDisposable
        {
            private OwnerState? ownerState;
            private Window? owner;

            public Lease(OwnerState ownerState, Window owner)
            {
                this.ownerState = ownerState;
                this.owner = owner;
            }

            public void Dispose()
            {
                OwnerState? currentState = Interlocked.Exchange(ref ownerState, null);
                Window? currentOwner = Interlocked.Exchange(ref owner, null);
                if (currentState != null && currentOwner != null)
                {
                    currentState.Release(currentOwner);
                }
            }
        }
    }
}
