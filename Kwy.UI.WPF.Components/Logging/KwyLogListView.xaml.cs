using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Components.Logging;

public partial class KwyLogListView : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(KwyLogListView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.Register(
            nameof(AutoScroll),
            typeof(bool),
            typeof(KwyLogListView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowTimeProperty =
        DependencyProperty.Register(
            nameof(ShowTime),
            typeof(bool),
            typeof(KwyLogListView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowLevelProperty =
        DependencyProperty.Register(
            nameof(ShowLevel),
            typeof(bool),
            typeof(KwyLogListView),
            new PropertyMetadata(true));

    public static readonly DependencyProperty MessageTextWrappingProperty =
        DependencyProperty.Register(
            nameof(MessageTextWrapping),
            typeof(TextWrapping),
            typeof(KwyLogListView),
            new PropertyMetadata(TextWrapping.NoWrap));

    public KwyLogListView()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool AutoScroll
    {
        get => (bool)GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public bool ShowTime
    {
        get => (bool)GetValue(ShowTimeProperty);
        set => SetValue(ShowTimeProperty, value);
    }

    public bool ShowLevel
    {
        get => (bool)GetValue(ShowLevelProperty);
        set => SetValue(ShowLevelProperty, value);
    }

    public TextWrapping MessageTextWrapping
    {
        get => (TextWrapping)GetValue(MessageTextWrappingProperty);
        set => SetValue(MessageTextWrappingProperty, value);
    }
}
