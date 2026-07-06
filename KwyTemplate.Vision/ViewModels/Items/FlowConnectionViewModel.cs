using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.Services;
using Kwy.MVVM.Core;
using System.Windows;
using System.Windows.Media;

namespace KwyTemplate.Vision.ViewModels.Items;

/// <summary>
/// 连线 ViewModel，对应 Nodify 的 ConnectionItem
/// </summary>
public class FlowConnectionViewModel : BindableBase
{
    public Guid ConnectionId { get; }

    /// <summary>源端口（Output）ViewModel</summary>
    public PortViewModel Source { get; }

    /// <summary>目标端口（Input）ViewModel</summary>
    public PortViewModel Target { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RefreshVisuals();
            }
        }
    }

    /// <summary>连线是否有效（类型匹配）</summary>
    private bool _isValid = true;

    public bool IsValid
    {
        get => _isValid;
        set
        {
            if (SetProperty(ref _isValid, value))
                RefreshVisuals();
        }
    }

    private bool _hasBreakpoint;

    public bool HasBreakpoint
    {
        get => _hasBreakpoint;
        set
        {
            if (SetProperty(ref _hasBreakpoint, value))
                RefreshVisuals();
        }
    }

    private bool _hasProbe;

    public bool HasProbe
    {
        get => _hasProbe;
        set
        {
            if (SetProperty(ref _hasProbe, value))
            {
                RaisePropertyChanged(nameof(ShowProbeValue));
            }
        }
    }

    private object? _lastValue;

    public object? LastValue
    {
        get => _lastValue;
        set
        {
            if (SetProperty(ref _lastValue, value))
            {
                RaisePropertyChanged(nameof(ShowProbeValue));
                RaisePropertyChanged(nameof(DisplayValue));
            }
        }
    }

    public bool ShowProbeValue => HasProbe || LastValue is not null;

    public string DisplayValue => FlowValueDisplayFormatter.FormatValue(LastValue);

    /// <summary>连线的中点坐标（用于定位探针标题）</summary>
    public Point Midpoint => new Point(
        (Source.Anchor.X + Target.Anchor.X) / 2,
        (Source.Anchor.Y + Target.Anchor.Y) / 2
    );

    private void RefreshVisuals()
    {
        RaisePropertyChanged(nameof(StrokeBrush));
        RaisePropertyChanged(nameof(Thickness));
        RaisePropertyChanged(nameof(StrokeDash));
        RaisePropertyChanged(nameof(StrokeDash));
    }

    // --- WPF 前台直接绑定的独立视觉属性 ——

    private readonly DataTypeColorService _colorService;

    public SolidColorBrush StrokeBrush
    {
        get
        {
            if (!IsValid) return _colorService.GetErrorBrush();
            if (HasBreakpoint) return new SolidColorBrush(Color.FromRgb(243, 139, 168)); // AccentRed
            if (IsSelected) return new SolidColorBrush(Color.FromRgb(137, 180, 250));    // ConnColor
            return Source.ConnectionColor;
        }
    }

    public double Thickness
    {
        get
        {
            if (!IsValid) return 2.0;
            if (HasBreakpoint) return 4.0;
            return 2.0; // 选中连线不再加粗
        }
    }

    public DoubleCollection? StrokeDash
    {
        get
        {
            if (!IsValid) return new DoubleCollection(new double[] { 3, 3 });
            if (IsSelected) return new DoubleCollection(new double[] { 4, 2 });
            return null;
        }
    }

    // Nodify 通过 Source/Target Anchor 自动绘制贝塞尔连线
    // 无需手动管理坐标

    public FlowConnectionViewModel(Guid connectionId, PortViewModel source, PortViewModel target, DataTypeColorService colorService)
    {
        _colorService = colorService;
        ConnectionId = connectionId;
        Source = source;
        Target = target;

        // 更新连接状态
        UpdateConnectionState();
    }

    /// <summary>更新连接状态（连接计数、有效性等）</summary>
    public void UpdateConnectionState()
    {
        Source.IsConnected = true;
        Target.IsConnected = true;

        // 验证类型匹配
        IsValid = ValidateTypeMatch();

        // 更新连接计数（用于显示分支）
        UpdateConnectionCounts();
    }

    /// <summary>验证类型匹配</summary>
    private bool ValidateTypeMatch()
    {
        // Any 类型可以连接任何类型
        if (Source.DataType == PortDataTypes.Any || Target.DataType == PortDataTypes.Any)
            return true;

        // 类型必须完全匹配
        return Source.DataType == Target.DataType;
    }

    /// <summary>更新连接计数</summary>
    private void UpdateConnectionCounts()
    {
        // 输出端口的连接计数由外部管理（FlowEditorViewModel）
        // 这里只更新输入端口
        if (Target.Direction == PortDirection.Input)
        {
            // 连接计数会在 FlowEditorViewModel 中统一管理
        }
    }
}
