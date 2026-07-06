using System.Collections.ObjectModel;
using Kwy.MVVM.Core;

namespace Kwy.UI.WPF.Components.PropertyGrid;

/// <summary>
/// Dynamic property group used by <see cref="KwyPropertyGrid"/>.
/// </summary>
public sealed class PropertyGroupModel : BindableBase
{
    private string groupName = string.Empty;
    private double widthRatio = 1.0;

    public string GroupName
    {
        get => groupName;
        set => SetProperty(ref groupName, value);
    }

    public double WidthRatio
    {
        get => widthRatio;
        set => SetProperty(ref widthRatio, value);
    }

    public ObservableCollection<DynamicPropertyItem> Properties { get; } = new();
}
