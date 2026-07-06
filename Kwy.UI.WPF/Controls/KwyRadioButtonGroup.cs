using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// 专属单选框组控件。
/// 不再继承自 ListBox，以避免 Selector 机制对 SelectedItem 的强制校验（Coercion）。
/// </summary>
public class KwyRadioButtonGroup : Control
{
    static KwyRadioButtonGroup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyRadioButtonGroup),
            new FrameworkPropertyMetadata(typeof(KwyRadioButtonGroup)));
    }

    // ── 属性 ──────────────────────────────────────────────────────────

    /// <summary>当前选中的值</summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(object), typeof(KwyRadioButtonGroup),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>原始数据源</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(KwyRadioButtonGroup),
            new PropertyMetadata(null, OnItemsSourceChanged));

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyRadioButtonGroup ctrl)
        {
            ctrl.ParseSource(e.NewValue as IEnumerable);
        }
    }

    /// <summary>自动生成的组名，确保矩阵分列模式下的全局单选互斥</summary>
    public string RadioButtonGroupName { get; } = "Group_" + System.Guid.NewGuid().ToString("N");

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(KwyRadioButtonGroup),
            new PropertyMetadata(double.NaN));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(KwyRadioButtonGroup),
            new PropertyMetadata(Orientation.Horizontal));

    /// <summary>解析后的列集合</summary>
    public IEnumerable? ParsedItemsSource
    {
        get => (IEnumerable?)GetValue(ParsedItemsSourceProperty);
        private set => SetValue(ParsedItemsSourcePropertyKey, value);
    }

    private static readonly DependencyPropertyKey ParsedItemsSourcePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ParsedItemsSource), typeof(IEnumerable),
            typeof(KwyRadioButtonGroup), new PropertyMetadata(null));

    public static readonly DependencyProperty ParsedItemsSourceProperty =
        ParsedItemsSourcePropertyKey.DependencyProperty;

    /// <summary>最终提供给 ItemsPresenter 使用的数据源</summary>
    public IEnumerable? EffectiveItemsSource
    {
        get => (IEnumerable?)GetValue(EffectiveItemsSourceProperty);
        private set => SetValue(EffectiveItemsSourcePropertyKey, value);
    }

    private static readonly DependencyPropertyKey EffectiveItemsSourcePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(EffectiveItemsSource), typeof(IEnumerable),
            typeof(KwyRadioButtonGroup), new PropertyMetadata(null));

    public static readonly DependencyProperty EffectiveItemsSourceProperty =
        EffectiveItemsSourcePropertyKey.DependencyProperty;

    /// <summary>是否为分列模式</summary>
    public bool HasColumns
    {
        get => (bool)GetValue(HasColumnsProperty);
        private set => SetValue(HasColumnsPropertyKey, value);
    }

    private static readonly DependencyPropertyKey HasColumnsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasColumns), typeof(bool),
            typeof(KwyRadioButtonGroup), new PropertyMetadata(false));

    public static readonly DependencyProperty HasColumnsProperty =
        HasColumnsPropertyKey.DependencyProperty;

    // ── 逻辑 ──────────────────────────────────────────────────────────

    private void ParseSource(IEnumerable? source)
    {
        if (source == null)
        {
            HasColumns = false;
            ParsedItemsSource = Array.Empty<object>();
            EffectiveItemsSource = Array.Empty<object>();
            return;
        }

        bool anyColumn = false;
        var flatList = new List<ColumnGroup>();

        foreach (var item in source)
        {
            if (item is string str && str.Contains('|'))
            {
                anyColumn = true;
                flatList.Add(new ColumnGroup(str.Split('|')));
            }
            else if (item is IEnumerable sub && item is not string)
            {
                anyColumn = true;
                var children = new List<string>();
                foreach (var s in sub) children.Add(s?.ToString() ?? string.Empty);
                flatList.Add(new ColumnGroup(children));
            }
            else
            {
                flatList.Add(new ColumnGroup(new[] { item?.ToString() ?? string.Empty }));
            }
        }

        HasColumns = anyColumn;
        ParsedItemsSource = flatList;
        EffectiveItemsSource = anyColumn ? flatList : source;
    }

    public sealed class ColumnGroup
    {
        public IReadOnlyList<string> Items { get; }
        public ColumnGroup(IEnumerable<string> items) => Items = new List<string>(items);
    }
}
