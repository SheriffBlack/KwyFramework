using KwyTemplate.Vision.Models;
using System.Windows.Media;

namespace KwyTemplate.Vision.Services;

/// <summary>
/// 数据类型颜色服务：为不同数据类型提供 LabVIEW 风格的颜色编码
/// </summary>
public class DataTypeColorService
{
    // LabVIEW 风格的颜色定义
    private readonly Dictionary<string, Color> typeColors = new()
    {
        [PortDataTypes.Any] = Color.FromRgb(128, 128, 128),  // 灰色 - 任意类型
        [PortDataTypes.Image] = Color.FromRgb(0, 128, 255),  // 蓝色 - 图像
        [PortDataTypes.ImageList] = Color.FromRgb(0, 170, 220),  // 青蓝色 - 图像集合
        [PortDataTypes.Number] = Color.FromRgb(0, 153, 204),    // 专业灰蓝 - 数值
        [PortDataTypes.Boolean] = Color.FromRgb(0, 255, 0),    // 绿色 - 布尔
        [PortDataTypes.String] = Color.FromRgb(255, 255, 0),    // 黄色 - 字符串
        [PortDataTypes.Blob] = Color.FromRgb(128, 0, 128),  // 紫色 - 二进制数据
    };

    /// <summary>
    /// 获取数据类型的颜色
    /// </summary>
    public Color GetColor(string dataType)
    {
        return typeColors.TryGetValue(dataType, out var color)
            ? color
            : typeColors[PortDataTypes.Any];
    }

    /// <summary>
    /// 获取数据类型的画刷
    /// </summary>
    public SolidColorBrush GetBrush(string dataType)
    {
        return new SolidColorBrush(GetColor(dataType));
    }

    /// <summary>
    /// 获取连线颜色（根据源端口的数据类型）
    /// </summary>
    public Color GetConnectionColor(string sourceDataType)
    {
        return GetColor(sourceDataType);
    }

    /// <summary>
    /// 获取连线画刷
    /// </summary>
    public SolidColorBrush GetConnectionBrush(string sourceDataType)
    {
        return GetBrush(sourceDataType);
    }

    /// <summary>
    /// 获取错误状态的连线颜色（红色，用于类型不匹配）
    /// </summary>
    public Color GetErrorColor()
    {
        return Color.FromRgb(255, 0, 0); // 红色
    }

    /// <summary>
    /// 获取错误状态的连线画刷
    /// </summary>
    public SolidColorBrush GetErrorBrush()
    {
        return new SolidColorBrush(GetErrorColor());
    }

    /// <summary>
    /// 获取端口边框颜色（用于端口标识）
    /// </summary>
    public Color GetPortBorderColor(string dataType)
    {
        var baseColor = GetColor(dataType);
        // 稍微变亮，用于边框
        return Color.FromRgb(
            (byte)Math.Min(255, baseColor.R + 40),
            (byte)Math.Min(255, baseColor.G + 40),
            (byte)Math.Min(255, baseColor.B + 40)
        );
    }

    /// <summary>
    /// 获取端口边框画刷
    /// </summary>
    public SolidColorBrush GetPortBorderBrush(string dataType)
    {
        return new SolidColorBrush(GetPortBorderColor(dataType));
    }
}
