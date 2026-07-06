using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.ViewModels;
using KwyTemplate.Vision.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KwyTemplate.Vision.Services;

internal sealed class NodeDragPreviewService
{
    private readonly FrameworkElement resourceOwner;
    private NodeDragPreviewWindow? previewWindow;

    public NodeDragPreviewService(FrameworkElement resourceOwner)
    {
        this.resourceOwner = resourceOwner;
    }

    public NodeDragPayload Show(NodePaletteItemViewModel item)
    {
        Close();

        previewWindow = new NodeDragPreviewWindow
        {
            DataContext = item
        };

        UpdatePosition();
        previewWindow.Show();

        return new NodeDragPayload(
            item.NodeType,
            new Vector(previewWindow.Width / 2.0, previewWindow.Height / 2.0));
    }

    public void Attach(UIElement source)
    {
        source.GiveFeedback += OnGiveFeedback;
        source.QueryContinueDrag += OnQueryContinueDrag;
    }

    public void Detach(UIElement source)
    {
        source.GiveFeedback -= OnGiveFeedback;
        source.QueryContinueDrag -= OnQueryContinueDrag;
    }

    public void Close()
    {
        if (previewWindow == null)
        {
            return;
        }

        previewWindow.Close();
        previewWindow = null;
    }

    private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.Hand);
        UpdatePosition();
        e.Handled = true;
    }

    private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed || e.Action is DragAction.Cancel or DragAction.Drop)
        {
            Close();
        }
    }

    private void UpdatePosition()
    {
        if (previewWindow == null || !GetCursorPos(out var point))
        {
            return;
        }

        var dip = PointFromScreenPixels(point.X, point.Y);
        previewWindow.Left = dip.X - previewWindow.Width / 2.0;
        previewWindow.Top = dip.Y - previewWindow.Height / 2.0;
    }

    private Point PointFromScreenPixels(int x, int y)
    {
        var source = PresentationSource.FromVisual(resourceOwner);
        if (source?.CompositionTarget == null)
        {
            return new Point(x, y);
        }

        return source.CompositionTarget.TransformFromDevice.Transform(new Point(x, y));
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
