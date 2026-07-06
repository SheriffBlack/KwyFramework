using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.FlowDesigner.Controls;

/// <summary>
/// 节点控件，包含输入输出端口、主题色、选中状态等属性，支持自定义输入输出端口模板。
/// </summary>
public class KwyNode : HeaderedContentControl
{
    // ── 附加属性：用于标识端口所属的侧边 ──────────────────────

    public static readonly DependencyProperty PortSideProperty =
        DependencyProperty.RegisterAttached("PortSide", typeof(string), typeof(KwyNode), new PropertyMetadata(null));

    public static string GetPortSide(DependencyObject obj) => (string)obj.GetValue(PortSideProperty);

    public static void SetPortSide(DependencyObject obj, string value) => obj.SetValue(PortSideProperty, value);

    // ── 构造 ──────────────────────────────────────────
    static KwyNode()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyNode), new FrameworkPropertyMetadata(typeof(KwyNode)));
    }

    // --- 4-Side Port Support ---
    public static readonly DependencyProperty InputLeftProperty =
        DependencyProperty.Register("InputLeft", typeof(IEnumerable), typeof(KwyNode), new PropertyMetadata(null));

    public IEnumerable InputLeft { get => (IEnumerable)GetValue(InputLeftProperty); set => SetValue(InputLeftProperty, value); }

    public static readonly DependencyProperty InputTopProperty =
        DependencyProperty.Register("InputTop", typeof(IEnumerable), typeof(KwyNode), new PropertyMetadata(null));

    public IEnumerable InputTop { get => (IEnumerable)GetValue(InputTopProperty); set => SetValue(InputTopProperty, value); }

    public static readonly DependencyProperty OutputRightProperty =
        DependencyProperty.Register("OutputRight", typeof(IEnumerable), typeof(KwyNode), new PropertyMetadata(null));

    public IEnumerable OutputRight { get => (IEnumerable)GetValue(OutputRightProperty); set => SetValue(OutputRightProperty, value); }

    public static readonly DependencyProperty OutputBottomProperty =
        DependencyProperty.Register("OutputBottom", typeof(IEnumerable), typeof(KwyNode), new PropertyMetadata(null));

    public IEnumerable OutputBottom { get => (IEnumerable)GetValue(OutputBottomProperty); set => SetValue(OutputBottomProperty, value); }

    // 4. Input Connector Template
    public static readonly DependencyProperty InputConnectorTemplateProperty =
        DependencyProperty.Register("InputConnectorTemplate", typeof(DataTemplate), typeof(KwyNode), new PropertyMetadata(null));

    public DataTemplate InputConnectorTemplate
    {
        get => (DataTemplate)GetValue(InputConnectorTemplateProperty);
        set => SetValue(InputConnectorTemplateProperty, value);
    }

    // 5. Output Connector Template
    public static readonly DependencyProperty OutputConnectorTemplateProperty =
        DependencyProperty.Register("OutputConnectorTemplate", typeof(DataTemplate), typeof(KwyNode), new PropertyMetadata(null));

    public DataTemplate OutputConnectorTemplate
    {
        get => (DataTemplate)GetValue(OutputConnectorTemplateProperty);
        set => SetValue(OutputConnectorTemplateProperty, value);
    }

    // 6. ThemeColor
    public static readonly DependencyProperty ThemeColorProperty =
        DependencyProperty.Register("ThemeColor", typeof(Brush), typeof(KwyNode), new PropertyMetadata(Brushes.Gray));

    public Brush ThemeColor
    {
        get => (Brush)GetValue(ThemeColorProperty);
        set => SetValue(ThemeColorProperty, value);
    }

    // 7. Selection state
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(KwyNode), new PropertyMetadata(false));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // 8. Disabled state
    public static readonly DependencyProperty IsDisabledProperty =
        DependencyProperty.Register("IsDisabled", typeof(bool), typeof(KwyNode), new PropertyMetadata(false));

    public bool IsDisabled
    {
        get => (bool)GetValue(IsDisabledProperty);
        set => SetValue(IsDisabledProperty, value);
    }

    // 9. Node status (Running, Success, etc.)
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register("Status", typeof(object), typeof(KwyNode), new PropertyMetadata(null));

    public object Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    // 10. Header Icon
    public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register("HeaderIcon", typeof(Geometry), typeof(KwyNode), new PropertyMetadata(null));

    public Geometry HeaderIcon
    {
        get => (Geometry)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }
}