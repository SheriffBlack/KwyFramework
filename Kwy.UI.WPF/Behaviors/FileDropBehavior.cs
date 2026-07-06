using Microsoft.Xaml.Behaviors;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 将拖入的文件路径写入目标控件数据上下文中的属性。
/// </summary>
public class FileDropBehavior : Behavior<FrameworkElement>
{
    public string? TargetPropertyPath
    {
        get => (string?)GetValue(TargetPropertyPathProperty);
        set => SetValue(TargetPropertyPathProperty, value);
    }

    public static readonly DependencyProperty TargetPropertyPathProperty =
        DependencyProperty.Register(
            nameof(TargetPropertyPath),
            typeof(string),
            typeof(FileDropBehavior),
            new PropertyMetadata(null));

    public string? AllowedExtensions
    {
        get => (string?)GetValue(AllowedExtensionsProperty);
        set => SetValue(AllowedExtensionsProperty, value);
    }

    public static readonly DependencyProperty AllowedExtensionsProperty =
        DependencyProperty.Register(
            nameof(AllowedExtensions),
            typeof(string),
            typeof(FileDropBehavior),
            new PropertyMetadata(null));

    public bool AllowMultipleFiles
    {
        get => (bool)GetValue(AllowMultipleFilesProperty);
        set => SetValue(AllowMultipleFilesProperty, value);
    }

    public static readonly DependencyProperty AllowMultipleFilesProperty =
        DependencyProperty.Register(
            nameof(AllowMultipleFiles),
            typeof(bool),
            typeof(FileDropBehavior),
            new PropertyMetadata(false));

    public bool AllowDirectories
    {
        get => (bool)GetValue(AllowDirectoriesProperty);
        set => SetValue(AllowDirectoriesProperty, value);
    }

    public static readonly DependencyProperty AllowDirectoriesProperty =
        DependencyProperty.Register(
            nameof(AllowDirectories),
            typeof(bool),
            typeof(FileDropBehavior),
            new PropertyMetadata(false));

    public string? DropEffectStyleKey
    {
        get => (string?)GetValue(DropEffectStyleKeyProperty);
        set => SetValue(DropEffectStyleKeyProperty, value);
    }

    public static readonly DependencyProperty DropEffectStyleKeyProperty =
        DependencyProperty.Register(
            nameof(DropEffectStyleKey),
            typeof(string),
            typeof(FileDropBehavior),
            new PropertyMetadata(null));

    private Style? originalStyle;
    private Brush? originalBackground;
    private Brush? originalBorderBrush;
    private Thickness originalBorderThickness;
    private bool originalAllowDrop;
    private bool visualFeedbackActive;

    protected override void OnAttached()
    {
        base.OnAttached();

        originalAllowDrop = AssociatedObject.AllowDrop;
        AssociatedObject.AllowDrop = true;
        AssociatedObject.PreviewDragEnter += OnDragEnter;
        AssociatedObject.PreviewDragOver += OnDragOver;
        AssociatedObject.PreviewDragLeave += OnDragLeave;
        AssociatedObject.PreviewDrop += OnDrop;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewDragEnter -= OnDragEnter;
        AssociatedObject.PreviewDragOver -= OnDragOver;
        AssociatedObject.PreviewDragLeave -= OnDragLeave;
        AssociatedObject.PreviewDrop -= OnDrop;
        UpdateVisualFeedback(false);
        AssociatedObject.AllowDrop = originalAllowDrop;

        base.OnDetaching();
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        SetDragEffects(e);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        SetDragEffects(e);
    }

    private void SetDragEffects(DragEventArgs e)
    {
        bool isValid = TryGetFiles(e, out _);
        e.Effects = isValid ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        UpdateVisualFeedback(isValid);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        UpdateVisualFeedback(false);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        UpdateVisualFeedback(false);
        e.Handled = true;

        if (!TryGetFiles(e, out var files))
        {
            return;
        }

        string filePath = AllowMultipleFiles
            ? string.Join(";", files)
            : files[0];

        SetTargetProperty(filePath);
    }

    private bool TryGetFiles(DragEventArgs e, out string[] files)
    {
        files = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] droppedFiles
            || droppedFiles.Length == 0)
        {
            return false;
        }

        files = AllowMultipleFiles ? droppedFiles : [droppedFiles[0]];
        var allowedExtensions = GetAllowedExtensions();

        foreach (string path in files)
        {
            bool isDirectory = Directory.Exists(path);
            if (isDirectory)
            {
                if (!AllowDirectories)
                {
                    return false;
                }

                continue;
            }

            if (!File.Exists(path))
            {
                return false;
            }

            if (allowedExtensions.Count > 0
                && !allowedExtensions.Contains(Path.GetExtension(path)))
            {
                return false;
            }
        }

        return true;
    }

    private HashSet<string> GetAllowedExtensions()
    {
        if (string.IsNullOrWhiteSpace(AllowedExtensions))
        {
            return [];
        }

        return AllowedExtensions
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension.Replace("*", string.Empty))
            .Where(extension => extension.Length > 0)
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void SetTargetProperty(string filePath)
    {
        if (string.IsNullOrWhiteSpace(TargetPropertyPath)
            || AssociatedObject.DataContext is not object dataContext)
        {
            return;
        }

        var property = dataContext.GetType().GetProperty(TargetPropertyPath);
        if (property?.CanWrite == true)
        {
            property.SetValue(dataContext, filePath);
        }
    }

    private void UpdateVisualFeedback(bool isDragging)
    {
        if (AssociatedObject is not Control control)
        {
            return;
        }

        if (isDragging)
        {
            if (!visualFeedbackActive)
            {
                originalStyle = control.Style;
                originalBackground = control.Background;
                originalBorderBrush = control.BorderBrush;
                originalBorderThickness = control.BorderThickness;
                visualFeedbackActive = true;
            }

            if (!string.IsNullOrWhiteSpace(DropEffectStyleKey)
                && (control.TryFindResource(DropEffectStyleKey) as Style
                    ?? Application.Current?.TryFindResource(DropEffectStyleKey) as Style) is Style style)
            {
                control.Style = style;
                return;
            }

            control.SetResourceReference(Control.BackgroundProperty, "DropTargetBackgroundBrush");
            control.SetResourceReference(Control.BorderBrushProperty, "DropTargetBorderBrush");
            control.BorderThickness = new Thickness(2);
            return;
        }

        if (!visualFeedbackActive)
        {
            return;
        }

        control.Style = originalStyle;
        control.Background = originalBackground;
        control.BorderBrush = originalBorderBrush;
        control.BorderThickness = originalBorderThickness;

        originalStyle = null;
        originalBackground = null;
        originalBorderBrush = null;
        originalBorderThickness = default;
        visualFeedbackActive = false;
    }
}
