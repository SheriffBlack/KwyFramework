namespace Kwy.UI.Diagrams;

/// <summary>
/// 通用图示节点的外形。
/// </summary>
public enum DiagramNodeShape
{
    Rectangle,
    RoundedRectangle,
    Ellipse,
    SemiEllipse,
    Trapezoid,
    RegularPolygon
}

/// <summary>
/// 非对称节点外形的朝向。
/// </summary>
public enum DiagramNodeOrientation
{
    Top,
    Right,
    Bottom,
    Left
}
