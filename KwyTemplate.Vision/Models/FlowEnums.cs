namespace KwyTemplate.Vision.Models;

/// <summary>
/// 端口方向：输入 / 输出
/// </summary>
public enum PortDirection
{
    Input,
    Output
}

/// <summary>
/// 布局方向：水平 / 垂直
/// </summary>
public enum FlowLayoutDirection
{
    Horizontal,
    Vertical
}

/// <summary>
/// 端口位置
/// </summary>
public enum PortSide
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// 端口类型
/// </summary>
public enum PortType
{
    Data,
    Execution
}

/// <summary>
/// 已知数据类型常量，用于端口类型约束与连线合法性校验
/// </summary>
public static class PortDataTypes
{
    public const string Any = "Any";
    public const string Image = "Image";
    public const string ImageList = "ImageList";
    public const string Number = "Number";
    public const string Boolean = "Boolean";
    public const string String = "String";
    public const string Blob = "Blob";
    public const string BlobList = "BlobList";
    public const string Region = "Region";
    public const string Point = "Point";
    public const string Line = "Line";
    public const string Circle = "Circle";
    public const string MatchResult = "MatchResult";
}
