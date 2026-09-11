using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KwyTemplate.Vision.Behaviors;

public static class ScrollViewerMouseWheelBehavior
{
    public static readonly DependencyProperty IsWheelScrollEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsWheelScrollEnabled",
            typeof(bool),
            typeof(ScrollViewerMouseWheelBehavior),
            new PropertyMetadata(false, OnIsWheelScrollEnabledChanged));

    public static bool GetIsWheelScrollEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsWheelScrollEnabledProperty);

    public static void SetIsWheelScrollEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsWheelScrollEnabledProperty, value);

    private static void OnIsWheelScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        double targetOffset = scrollViewer.VerticalOffset - e.Delta;
        targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableHeight));
        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }
}
