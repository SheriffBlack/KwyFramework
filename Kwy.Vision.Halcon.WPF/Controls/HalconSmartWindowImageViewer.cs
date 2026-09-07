using HalconDotNet;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Halcon.Images;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.Vision.Halcon.WPF.Controls;

public sealed class HalconSmartWindowImageViewer : ContentControl, IDisposable
{
    public static readonly DependencyProperty ImageProperty =
        DependencyProperty.Register(
            nameof(Image),
            typeof(IVisionImage),
            typeof(HalconSmartWindowImageViewer),
            new PropertyMetadata(null, OnImageChanged));

    public static readonly DependencyProperty AutoFitProperty =
        DependencyProperty.Register(
            nameof(AutoFit),
            typeof(bool),
            typeof(HalconSmartWindowImageViewer),
            new PropertyMetadata(true));

    private readonly HalconVisionImageConverter converter = new();
    private readonly object? smartWindowControl;
    private HalconImageLease? currentLease;
    private int renderVersion;
    private bool disposed;

    public HalconSmartWindowImageViewer()
    {
        smartWindowControl = CreateSmartWindowControl();
        Content = smartWindowControl as UIElement ?? CreateMissingControlPlaceholder();
    }

    public IVisionImage? Image
    {
        get => (IVisionImage?)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public bool AutoFit
    {
        get => (bool)GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        currentLease?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        currentLease = null;
    }

    private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (HalconSmartWindowImageViewer)d;
        _ = viewer.ShowImageAsync(e.NewValue as IVisionImage);
    }

    private async Task ShowImageAsync(IVisionImage? image)
    {
        int version = Interlocked.Increment(ref renderVersion);
        if (image == null || smartWindowControl == null)
        {
            await ClearCurrentImageAsync().ConfigureAwait(true);
            return;
        }

        HalconImageLease? lease = null;
        try
        {
            lease = await converter.AcquireAsync(image).ConfigureAwait(true);
            if (version != renderVersion || disposed)
            {
                await lease.DisposeAsync().ConfigureAwait(true);
                return;
            }

            await ClearCurrentImageAsync().ConfigureAwait(true);
            currentLease = lease;
            DisplayImage(lease.Image);
        }
        catch
        {
            if (lease != null)
            {
                await lease.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    private async Task ClearCurrentImageAsync()
    {
        HalconImageLease? lease = currentLease;
        currentLease = null;
        if (lease != null)
        {
            await lease.DisposeAsync().ConfigureAwait(true);
        }

        object? halconWindow = GetHalconWindow();
        InvokeIfExists(halconWindow, "ClearWindow");
    }

    private void DisplayImage(HImage image)
    {
        object? halconWindow = GetHalconWindow();
        if (halconWindow == null)
        {
            return;
        }

        if (AutoFit)
        {
            InvokeIfExists(smartWindowControl, "SetFullImagePart");
        }

        if (!InvokeIfExists(halconWindow, "DispObj", image))
        {
            InvokeIfExists(halconWindow, "DispImage", image);
        }
    }

    private object? GetHalconWindow()
        => smartWindowControl?.GetType().GetProperty("HalconWindow")?.GetValue(smartWindowControl);

    private static object? CreateSmartWindowControl()
    {
        Type? type = Type.GetType("HalconDotNet.HSmartWindowControlWPF, halcondotnet", throwOnError: false);
        if (type == null || !typeof(UIElement).IsAssignableFrom(type))
        {
            return null;
        }

        return Activator.CreateInstance(type);
    }

    private static bool InvokeIfExists(object? target, string methodName, params object[] args)
    {
        if (target == null)
        {
            return false;
        }

        var method = target.GetType().GetMethod(methodName, args.Select(x => x.GetType()).ToArray())
            ?? target.GetType().GetMethods().FirstOrDefault(method =>
                string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
                method.GetParameters().Length == args.Length);

        if (method == null)
        {
            return false;
        }

        method.Invoke(target, args);
        return true;
    }

    private static UIElement CreateMissingControlPlaceholder()
        => new Border
        {
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Gray,
                Text = "未找到 HALCON HSmartWindowControlWPF"
            }
        };
}
