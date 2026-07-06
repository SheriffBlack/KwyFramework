using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Components.PropertyGrid;

public partial class KwyPropertyGrid : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(object),
            typeof(KwyPropertyGrid),
            new PropertyMetadata(null, OnSourceChanged));

    public KwyPropertyGrid()
    {
        Padding = new Thickness(20);
        InitializeComponent();
    }

    public object? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ObservableCollection<PropertyGroupModel> PropertyGroups { get; } = new();

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyPropertyGrid propertyGrid)
        {
            propertyGrid.Reload(e.NewValue);
        }
    }

    private void Reload(object? source)
    {
        PropertyGroups.Clear();
        foreach (var group in PropertyGridMetadataReader.CreateGroups(source))
        {
            PropertyGroups.Add(group);
        }
    }
}
