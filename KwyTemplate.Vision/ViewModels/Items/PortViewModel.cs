using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.Services;
using Kwy.MVVM.Core;
using System.Windows;
using System.Windows.Media;

namespace KwyTemplate.Vision.ViewModels.Items;

/// <summary>
/// 单个端口的 ViewModel，供 Nodify 的 NodeInput / NodeOutput 绑定
/// </summary>
public class PortViewModel : BindableBase
{
    // ── 数据源 ──────────────────────────────────────
    public Guid PortId { get; }

    private string name = string.Empty;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public PortDirection Direction { get; }
    public string DataType { get; }
    public PortSide Side { get; }
    public PortType Type { get; }

    /// <summary>是否允许多路连入（仅对 Input 端口有效）</summary>
    public bool AllowMultiple { get; }

    // ── Nodify 锚点（连线起止点，由 Nodify 自动更新）──
    private Point anchor;

    public Point Anchor
    {
        get => anchor;
        set => SetProperty(ref anchor, value);
    }

    private bool isConnected;

    public bool IsConnected
    {
        get => isConnected;
        set => SetProperty(ref isConnected, value);
    }

    /// <summary>连接到此端口的连线数量（用于显示分支）</summary>
    private int connectionCount;

    public int ConnectionCount
    {
        get => connectionCount;
        set => SetProperty(ref connectionCount, value);
    }

    private string? lastSide;

    /// <summary>最后一次交互或连接时的侧边位置</summary>
    public string? LastSide
    {
        get => lastSide;
        set => SetProperty(ref lastSide, value);
    }

    private readonly DataTypeColorService colorService;

    /// <summary>端口颜色（根据数据类型，LabVIEW 风格）</summary>
    public SolidColorBrush PortColor => colorService.GetPortBorderBrush(DataType);

    /// <summary>连线颜色（根据数据类型）</summary>
    public SolidColorBrush ConnectionColor => colorService.GetConnectionBrush(DataType);

    private bool hasProbe;

    /// <summary>是否开启端口探针</summary>
    public bool HasProbe
    {
        get => hasProbe;
        set
        {
            if (SetProperty(ref hasProbe, value))
            {
                RaisePropertyChanged(nameof(ShowProbeValue));
            }
        }
    }

    private object? lastValue;

    /// <summary>端口最后一次运行的数值</summary>
    public object? LastValue
    {
        get => lastValue;
        set
        {
            if (SetProperty(ref lastValue, value))
            {
                RaisePropertyChanged(nameof(ShowProbeValue));
                RaisePropertyChanged(nameof(DisplayValue));
            }
        }
    }

    /// <summary>是否应该显示探针内容</summary>
    public bool ShowProbeValue => HasProbe || LastValue is not null;

    public string DisplayValue => FlowValueDisplayFormatter.FormatValue(LastValue);

    /// <summary>所属的节点 ViewModel</summary>
    public FlowNodeViewModel Node { get; set; } = null!;

    public PortViewModel(FlowPort port, DataTypeColorService colorService, PortSide? sideOverride = null)
    {
        this.colorService = colorService;
        PortId = port.Id;
        Name = port.Name;
        Direction = port.Direction;
        DataType = port.DataType;
        AllowMultiple = port.AllowMultiple;
        Side = sideOverride ?? port.Side;
        Type = port.Type;
    }
}
