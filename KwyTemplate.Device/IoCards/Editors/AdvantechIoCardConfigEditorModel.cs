using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.IoCards.Advantech;

namespace KwyTemplate.Device.IoCards.Editors;

/// <summary>
/// Advantech IO 卡连接配置的 UI 元数据包装。
/// 底层配置保持纯设备模型，这里只负责 PropertyGrid 的显示名称和输入控件。
/// </summary>
public sealed class AdvantechIoCardConfigEditorModel
{
    private readonly AdvantechIoCardConfig config;

    public AdvantechIoCardConfigEditorModel(AdvantechIoCardConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    [Category("设备")]
    [CategoryKey("Connection.Category.Device")]
    [DisplayName("设备描述")]
    [DisplayNameKey("IoCard.DeviceDescription")]
    public string DeviceDescription
    {
        get => config.DeviceDescription;
        set => config.DeviceDescription = value;
    }

    [Category("设备")]
    [CategoryKey("Connection.Category.Device")]
    [DisplayName("板卡型号")]
    [DisplayNameKey("IoCard.Model")]
    public string Model
    {
        get => config.Model;
        set => config.Model = value;
    }

    [Category("端口")]
    [CategoryKey("Connection.Category.Port")]
    [DisplayName("DI端口数")]
    [DisplayNameKey("IoCard.DiPortCount")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, AdvantechIoCardConfig.MaxSupportedPorts, SmallChange = 1, DecimalPlaces = 0)]
    public int DiPortCount
    {
        get => config.DiPortCount;
        set => config.DiPortCount = value;
    }

    [Category("端口")]
    [CategoryKey("Connection.Category.Port")]
    [DisplayName("DO端口数")]
    [DisplayNameKey("IoCard.DoPortCount")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, AdvantechIoCardConfig.MaxSupportedPorts, SmallChange = 1, DecimalPlaces = 0)]
    public int DoPortCount
    {
        get => config.DoPortCount;
        set => config.DoPortCount = value;
    }

    [Category("中断")]
    [CategoryKey("Connection.Category.Interrupt")]
    [DisplayName("启用中断")]
    [DisplayNameKey("IoCard.EnableInterrupt")]
    [InputType(InputType.ToggleButton)]
    public bool EnableInterrupt
    {
        get => config.EnableInterrupt;
        set => config.EnableInterrupt = value;
    }

    [Category("中断")]
    [CategoryKey("Connection.Category.Interrupt")]
    [DisplayName("中断通道")]
    [DisplayNameKey("IoCard.InterruptChannel")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, AdvantechIoCardConfig.MaxSupportedChannels - 1, SmallChange = 1, DecimalPlaces = 0)]
    public int InterruptChannel
    {
        get => config.InterruptChannel;
        set => config.InterruptChannel = value;
    }

    [Category("中断")]
    [CategoryKey("Connection.Category.Interrupt")]
    [DisplayName("上升沿触发")]
    [DisplayNameKey("IoCard.InterruptRisingEdge")]
    [InputType(InputType.ToggleButton)]
    public bool InterruptRisingEdge
    {
        get => config.InterruptRisingEdge;
        set => config.InterruptRisingEdge = value;
    }
}
