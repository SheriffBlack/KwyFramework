using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.Services;
using KwyTemplate.Vision.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KwyTemplate.Vision.Behaviors;

public static class NodePaletteDragBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(NodePaletteDragBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty DragStateProperty =
        DependencyProperty.RegisterAttached(
            "DragState",
            typeof(DragState),
            typeof(NodePaletteDragBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsEnabledProperty, value);

    private static DragState? GetDragState(DependencyObject obj)
        => (DragState?)obj.GetValue(DragStateProperty);

    private static void SetDragState(DependencyObject obj, DragState? value)
        => obj.SetValue(DragStateProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            SetDragState(listBox, new DragState(new NodeDragPreviewService(listBox)));
            listBox.MouseDoubleClick += OnMouseDoubleClick;
            listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove += OnPreviewMouseMove;
            listBox.SelectionChanged += OnSelectionChanged;
        }
        else
        {
            listBox.MouseDoubleClick -= OnMouseDoubleClick;
            listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove -= OnPreviewMouseMove;
            listBox.SelectionChanged -= OnSelectionChanged;
            GetDragState(listBox)?.Preview.Close();
            SetDragState(listBox, null);
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: NodePaletteItemViewModel item } &&
            FindPaletteViewModel(sender as DependencyObject) is { } viewModel)
        {
            viewModel.AddNodeCommand.Execute(item);
            e.Handled = true;
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: not null } listBox)
        {
            listBox.SelectedItem = null;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        DragState? state = GetDragState(listBox);
        if (state == null)
        {
            return;
        }

        state.StartPoint = e.GetPosition(null);
        state.DraggingItem = (e.OriginalSource as FrameworkElement)?.DataContext as NodePaletteItemViewModel;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        DragState? state = GetDragState(listBox);
        if (state?.DraggingItem == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point position = e.GetPosition(null);
        Vector diff = state.StartPoint - position;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        NodeDragPayload payload = state.Preview.Show(state.DraggingItem);
        var data = new DataObject();
        data.SetData(typeof(NodeDragPayload), payload);
        data.SetData(typeof(string), payload.NodeType);
        state.Preview.Attach(listBox);

        try
        {
            DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy);
        }
        finally
        {
            state.Preview.Detach(listBox);
            state.Preview.Close();
            state.DraggingItem = null;
        }
    }

    private static NodePaletteViewModel? FindPaletteViewModel(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement { DataContext: NodePaletteViewModel viewModel })
            {
                return viewModel;
            }

            source = LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private sealed class DragState
    {
        public DragState(NodeDragPreviewService preview)
        {
            Preview = preview;
        }

        public NodeDragPreviewService Preview { get; }

        public Point StartPoint { get; set; }

        public NodePaletteItemViewModel? DraggingItem { get; set; }
    }
}
