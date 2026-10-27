using Kwy.Vision.Abstractions.Geometry;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kwy.Vision.WPF.Models;

public sealed class VisionRoiLayer : INotifyPropertyChanged
{
    private string name = "ROI";
    private IVisionRegion region = new RectangleRegion(new VisionRectangle(0, 0, 1, 1));
    private bool isVisible = true;
    private bool isLocked;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get => name;
        set => SetField(ref name, value);
    }

    public IVisionRegion Region
    {
        get => region;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetField(ref region, value))
            {
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public bool IsVisible
    {
        get => isVisible;
        set => SetField(ref isVisible, value);
    }

    public bool IsLocked
    {
        get => isLocked;
        set => SetField(ref isLocked, value);
    }

    public string Type => Region switch
    {
        RectangleRegion => "Rectangle",
        RotatedRectangleRegion => "Rotated Rectangle",
        CircleRegion => "Circle",
        EllipseRegion => "Ellipse",
        PolygonRegion => "Polygon",
        ContourRegion => "Contour",
        CompositeRegion => "Composite",
        _ => Region.GetType().Name
    };

    public string Summary => Region switch
    {
        RectangleRegion rectangle =>
            $"X={rectangle.Rectangle.X:F0}, Y={rectangle.Rectangle.Y:F0}, W={rectangle.Rectangle.Width:F0}, H={rectangle.Rectangle.Height:F0}",
        RotatedRectangleRegion rectangle =>
            $"X={rectangle.Rectangle.Center.X:F0}, Y={rectangle.Rectangle.Center.Y:F0}, W={rectangle.Rectangle.Width:F0}, H={rectangle.Rectangle.Height:F0}, A={rectangle.Rectangle.AngleRadians * 180 / Math.PI:F1}°",
        CircleRegion circle =>
            $"X={circle.Circle.Center.X:F0}, Y={circle.Circle.Center.Y:F0}, R={circle.Circle.Radius:F0}",
        _ => Type
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
