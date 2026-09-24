using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// 管理由附加属性临时应用的样式，并在撤销时恢复宿主原有的本地值。
/// </summary>
internal static class StyleApplicationHelper
{
    private static readonly ConditionalWeakTable<FrameworkElement, AssignmentState> StyleAssignments = new();
    private static readonly ConditionalWeakTable<ItemsControl, AssignmentState> ItemContainerStyleAssignments = new();

    public static void ApplyStyle(FrameworkElement element, Style style)
        => Apply(element, FrameworkElement.StyleProperty, style, StyleAssignments);

    public static void RestoreStyle(FrameworkElement element)
        => Restore(element, FrameworkElement.StyleProperty, StyleAssignments);

    public static void ApplyItemContainerStyle(ItemsControl control, Style style)
        => Apply(control, ItemsControl.ItemContainerStyleProperty, style, ItemContainerStyleAssignments);

    public static void RestoreItemContainerStyle(ItemsControl control)
        => Restore(control, ItemsControl.ItemContainerStyleProperty, ItemContainerStyleAssignments);

    private static void Apply<T>(
        T element,
        DependencyProperty property,
        Style style,
        ConditionalWeakTable<T, AssignmentState> assignments)
        where T : DependencyObject
    {
        if (!assignments.TryGetValue(element, out AssignmentState? state)
            || !ReferenceEquals(element.GetValue(property), state.AppliedValue))
        {
            assignments.Remove(element);
            state = AssignmentState.Capture(element, property);
            assignments.Add(element, state);
        }

        element.SetValue(property, style);
        state.AppliedValue = style;
    }

    private static void Restore<T>(
        T element,
        DependencyProperty property,
        ConditionalWeakTable<T, AssignmentState> assignments)
        where T : DependencyObject
    {
        if (!assignments.TryGetValue(element, out AssignmentState? state))
        {
            return;
        }

        if (ReferenceEquals(element.GetValue(property), state.AppliedValue))
        {
            if (state.HasLocalValue)
            {
                element.SetValue(property, state.LocalValue);
            }
            else
            {
                element.ClearValue(property);
            }
        }

        assignments.Remove(element);
    }

    private sealed class AssignmentState
    {
        private AssignmentState(bool hasLocalValue, object? localValue)
        {
            HasLocalValue = hasLocalValue;
            LocalValue = localValue;
        }

        public bool HasLocalValue { get; }
        public object? LocalValue { get; }
        public object? AppliedValue { get; set; }

        public static AssignmentState Capture(DependencyObject element, DependencyProperty property)
        {
            object localValue = element.ReadLocalValue(property);
            return localValue == DependencyProperty.UnsetValue
                ? new AssignmentState(false, null)
                : new AssignmentState(true, localValue);
        }
    }
}
