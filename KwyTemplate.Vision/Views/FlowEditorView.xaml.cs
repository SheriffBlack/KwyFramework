using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KwyTemplate.Vision.Views;

public partial class FlowEditorView : UserControl
{
    private Point projectGraphDragStartPoint;
    private FlowGraph? draggedProjectGraph;

    public FlowEditorView()
    {
        InitializeComponent();
    }

    private void OnProjectGraphListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        projectGraphDragStartPoint = e.GetPosition(null);
        draggedProjectGraph = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource)?.DataContext as FlowGraph;
    }

    private void OnProjectGraphListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedProjectGraph == null)
        {
            return;
        }

        Point currentPosition = e.GetPosition(null);
        if (Math.Abs(currentPosition.X - projectGraphDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - projectGraphDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(ProjectGraphListBox, draggedProjectGraph, DragDropEffects.Move);
    }

    private void OnProjectGraphListDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not FlowEditorViewModel viewmodel
            || e.Data.GetData(typeof(FlowGraph)) is not FlowGraph source)
        {
            return;
        }

        FlowGraph? target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource)?.DataContext as FlowGraph;
        int targetIndex = target == null
            ? viewmodel.ProjectGraphs.Count - 1
            : viewmodel.ProjectGraphs.IndexOf(target);

        viewmodel.MoveGraphToIndex(source, targetIndex);
        draggedProjectGraph = null;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
