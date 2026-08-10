using System.Collections.ObjectModel;
using System.Windows;
using Kwy.MVVM.Core;

namespace Kwy.UI.WPF.Components.PropertyGrid;

/// <summary>
/// Dynamic property group used by <see cref="KwyPropertyGrid"/>.
/// </summary>
public sealed class PropertyGroupModel : BindableBase
{
    private string groupName = string.Empty;
    private string? groupNameKey;
    private double widthRatio = 1.0;
    private bool hasGroupHeader;

    public string GroupName
    {
        get => ResolveResource(groupNameKey, groupName);
        set => SetProperty(ref groupName, value);
    }

    public string? GroupNameKey
    {
        get => groupNameKey;
        set
        {
            if (SetProperty(ref groupNameKey, value))
            {
                OnPropertyChanged(nameof(GroupName));
            }
        }
    }

    public double WidthRatio
    {
        get => widthRatio;
        set => SetProperty(ref widthRatio, value);
    }

    public bool HasGroupHeader
    {
        get => hasGroupHeader;
        set => SetProperty(ref hasGroupHeader, value);
    }

    public ObservableCollection<DynamicPropertyItem> Properties { get; } = new();

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(GroupName));
        foreach (DynamicPropertyItem property in Properties)
        {
            property.RefreshLocalization();
        }
    }

    private static string ResolveResource(string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        object? value = Application.Current?.TryFindResource(key);
        string? text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }
}