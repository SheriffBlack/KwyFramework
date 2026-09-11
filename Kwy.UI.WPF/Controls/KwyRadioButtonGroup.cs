using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// A single-selection items control rendered as a group of radio buttons.
/// </summary>
public class KwyRadioButtonGroup : ListBox
{
    private bool isSynchronizingValue;

    static KwyRadioButtonGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KwyRadioButtonGroup),
            new FrameworkPropertyMetadata(typeof(KwyRadioButtonGroup)));
    }

    public KwyRadioButtonGroup()
    {
        SelectionMode = SelectionMode.Single;
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(object),
        typeof(KwyRadioButtonGroup),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(KwyRadioButtonGroup), new PropertyMetadata(double.NaN),
        static value => value is double width && (double.IsNaN(width) || (double.IsFinite(width) && width >= 0)));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(KwyRadioButtonGroup), new PropertyMetadata(Orientation.Horizontal),
        static value => value is Orientation orientation && Enum.IsDefined(orientation));

    protected override AutomationPeer OnCreateAutomationPeer()
        => new KwyRadioButtonGroupAutomationPeer(this);

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (isSynchronizingValue)
        {
            return;
        }

        isSynchronizingValue = true;
        try
        {
            SetCurrentValue(ValueProperty, SelectedValue);
        }
        finally
        {
            isSynchronizingValue = false;
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var group = (KwyRadioButtonGroup)d;
        if (group.isSynchronizingValue || Equals(group.SelectedValue, e.NewValue))
        {
            return;
        }

        group.isSynchronizingValue = true;
        try
        {
            group.SetCurrentValue(SelectedValueProperty, e.NewValue);
        }
        finally
        {
            group.isSynchronizingValue = false;
        }
    }
}
