using Kwy.UI.WPF.FlowDesigner.Controls;
using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.ViewModels;
using System.Windows;

namespace KwyTemplate.Vision.Behaviors;

public static class FlowEditorNodeDropBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FlowEditorNodeDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.RegisterAttached(
            "ViewModel",
            typeof(FlowEditorViewModel),
            typeof(FlowEditorNodeDropBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsEnabledProperty, value);

    public static FlowEditorViewModel? GetViewModel(DependencyObject obj)
        => (FlowEditorViewModel?)obj.GetValue(ViewModelProperty);

    public static void SetViewModel(DependencyObject obj, FlowEditorViewModel? value)
        => obj.SetValue(ViewModelProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.DragOver += OnDragOver;
            element.Drop += OnDrop;
        }
        else
        {
            element.DragOver -= OnDragOver;
            element.Drop -= OnDrop;
        }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsNodeDragData(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (!TryGetNodeDragPayload(e.Data, out NodeDragPayload payload))
        {
            return;
        }

        FlowEditorViewModel? viewModel = sender is DependencyObject dependencyObject
            ? GetViewModel(dependencyObject)
            : null;

        if (viewModel == null)
        {
            return;
        }

        var position = e.GetPosition(sender as IInputElement);
        if (sender is KwyEditor editor)
        {
            position = editor.GetLogicalPosition(position);
        }

        position -= payload.AnchorOffset;
        viewModel.Palette.RequestAddNode(payload.NodeType, position);
        e.Handled = true;
    }

    private static bool TryGetNodeDragPayload(IDataObject data, out NodeDragPayload payload)
    {
        if (data.GetDataPresent(typeof(NodeDragPayload))
            && data.GetData(typeof(NodeDragPayload)) is NodeDragPayload typedPayload)
        {
            payload = typedPayload;
            return true;
        }

        if (data.GetDataPresent(typeof(string))
            && data.GetData(typeof(string)) is string nodeType
            && !string.IsNullOrWhiteSpace(nodeType))
        {
            payload = new NodeDragPayload(nodeType, default);
            return true;
        }

        payload = null!;
        return false;
    }

    private static bool IsNodeDragData(IDataObject data)
        => data.GetDataPresent(typeof(NodeDragPayload)) || data.GetDataPresent(typeof(string));
}
