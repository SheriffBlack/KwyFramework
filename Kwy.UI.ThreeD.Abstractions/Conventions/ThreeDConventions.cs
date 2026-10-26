namespace Kwy.UI.ThreeD.Abstractions.Conventions;

/// <summary>
/// 定义所有渲染后端都必须实现的可移植约定。
/// </summary>
public static class ThreeDConventions
{
    public const string CoordinateSystem = "Right-handed";

    public const string UpAxis = "+Y";

    public const string FrontFaceWinding = "Counter-clockwise";

    public const string AngleUnit = "Radians";

    /// <summary>
    /// 世界空间数值不绑定具体长度单位，渲染后端必须按原值一对一保留。
    /// 一个单位表示米、毫米或其他度量，由应用程序或模型导入器决定。
    /// </summary>
    public const string LengthUnit = "Application-defined";
}
