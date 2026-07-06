namespace Kwy.Device.Abstractions.IO;

/// <summary>
/// IO 物理点位模型
/// </summary>
public class IoPoint
{
    /// <summary>
    /// 逻辑名，例如 "DI_StartButton" 或 "DO_Cylinder_Push"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 归属硬件设备的 ID (如 "Googol_GTS_0")
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// 物理通道/引脚索引 (0-63)
    /// </summary>
    public byte Channel { get; set; }

    /// <summary>
    /// 是否极性反转
    /// 如果硬件是低电平触发（有信号时读到0），建议设为 true
    /// </summary>
    public bool Inverted { get; set; }

    /// <summary>
    /// 功能描述
    /// </summary>
    public required string Description { get; set; }
}