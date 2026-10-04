namespace Kwy.UI.Diagrams;

/// <summary>
/// 二维图逻辑坐标。
/// 坐标不绑定任何具体 UI 框架或屏幕像素。
/// </summary>
public readonly record struct DiagramPoint(double X, double Y);
