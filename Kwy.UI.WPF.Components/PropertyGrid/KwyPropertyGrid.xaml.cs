using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Kwy.ComponentModel;

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

    private void OnLoaded(object sender, RoutedEventArgs e)
        => PropertyMetadataLocalization.Changed += OnPropertyMetadataLocalizationChanged;

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => PropertyMetadataLocalization.Changed -= OnPropertyMetadataLocalizationChanged;

    private void OnPropertyMetadataLocalizationChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnPropertyMetadataLocalizationChanged(sender, e));
            return;
        }

        foreach (PropertyGroupModel group in PropertyGroups)
        {
            group.RefreshLocalization();
        }
    }

    private void Reload(object? source)
    {
        PropertyGroups.Clear();
        foreach (var group in PropertyGridMetadataReader.CreateGroups(source))
        {
            foreach (DynamicPropertyItem property in group.Properties)
            {
                property.ValueChanged += OnPropertyValueChanged;
            }

            PropertyGroups.Add(group);
        }
    }

    private void OnPropertyValueChanged(object? sender, EventArgs e)
    {
        if (sender is not DynamicPropertyItem { RefreshesPropertyGrid: true })
        {
            return;
        }

        Dispatcher.BeginInvoke(() => Reload(Source));
    }
}
