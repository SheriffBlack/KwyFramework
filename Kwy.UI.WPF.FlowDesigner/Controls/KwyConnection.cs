using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.FlowDesigner.Controls;

public enum ConnectionStyle
{
    Bezier = 0,
    Straight = 1,
    Orthogonal = 2,
    Circuit = 3
}

/// <summary>
/// 连线控件，负责在两个锚点之间绘制路径。
/// </summary>
public class KwyConnection : Control
{
    static KwyConnection()
    {
        // 覆盖默认样式主键
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyConnection), new FrameworkPropertyMetadata(typeof(KwyConnection)));
    }

    public KwyConnection()
    {
        Loaded += (s, e) => UpdatePathGeometry();
    }

    // ── 端口方向 ──
    public static readonly DependencyProperty SourceSideProperty =
        DependencyProperty.Register("SourceSide", typeof(string), typeof(KwyConnection),
            new FrameworkPropertyMetadata("Right", FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public string SourceSide
    {
        get => (string)GetValue(SourceSideProperty);
        set => SetValue(SourceSideProperty, value);
    }

    public static readonly DependencyProperty TargetSideProperty =
        DependencyProperty.Register("TargetSide", typeof(string), typeof(KwyConnection),
            new FrameworkPropertyMetadata("Left", FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public string TargetSide
    {
        get => (string)GetValue(TargetSideProperty);
        set => SetValue(TargetSideProperty, value);
    }

    // ── 箭头与偏移参数 (MVVM 暴露给 Style) ──
    public static readonly DependencyProperty ArrowSizeProperty =
        DependencyProperty.Register("ArrowSize", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double ArrowSize { get => (double)GetValue(ArrowSizeProperty); set => SetValue(ArrowSizeProperty, value); }

    public static readonly DependencyProperty ArrowWidthProperty =
        DependencyProperty.Register("ArrowWidth", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double ArrowWidth { get => (double)GetValue(ArrowWidthProperty); set => SetValue(ArrowWidthProperty, value); }

    public static readonly DependencyProperty HubOffsetProperty =
        DependencyProperty.Register("HubOffset", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(5.5, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double HubOffset { get => (double)GetValue(HubOffsetProperty); set => SetValue(HubOffsetProperty, value); }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register("CornerRadius", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public static readonly DependencyProperty OrthogonalMarginProperty =
        DependencyProperty.Register("OrthogonalMargin", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double OrthogonalMargin { get => (double)GetValue(OrthogonalMarginProperty); set => SetValue(OrthogonalMarginProperty, value); }

    public static readonly DependencyProperty BezierDistanceProperty =
        DependencyProperty.Register("BezierDistance", typeof(double), typeof(KwyConnection),
            new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public double BezierDistance { get => (double)GetValue(BezierDistanceProperty); set => SetValue(BezierDistanceProperty, value); }

    public static readonly DependencyProperty AdjustedTargetProperty =
        DependencyProperty.Register("AdjustedTarget", typeof(Point), typeof(KwyConnection),
            new PropertyMetadata(default(Point)));

    public Point AdjustedTarget { get => (Point)GetValue(AdjustedTargetProperty); set => SetValue(AdjustedTargetProperty, value); }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyConnection conn)
        {
            conn.UpdatePathGeometry();
        }
    }

    public void UpdatePathGeometry()
    {
        var figure = new PathFigure { StartPoint = Source, IsClosed = false, IsFilled = false };

        bool isHorizontal = Direction == Orientation.Horizontal;

        double p1x = Source.X;
        double p1y = Source.Y;
        double p2x = Target.X;
        double p2y = Target.Y;

        string sSide = SourceSide?.ToString() ?? "Right";
        string tSide = TargetSide?.ToString() ?? "Left";

        // ─── 计算偏移 ───
        double hOff = HubOffset;
        Point adjustedTarget = Target;
        if (tSide.Equals("Left", StringComparison.OrdinalIgnoreCase)) adjustedTarget.X -= hOff;
        else if (tSide.Equals("Right", StringComparison.OrdinalIgnoreCase)) adjustedTarget.X += hOff;
        else if (tSide.Equals("Top", StringComparison.OrdinalIgnoreCase)) adjustedTarget.Y -= hOff;
        else if (tSide.Equals("Bottom", StringComparison.OrdinalIgnoreCase)) adjustedTarget.Y += hOff;

        switch (ConnectionStyle)
        {
            case ConnectionStyle.Straight:
                figure.Segments.Add(new LineSegment(adjustedTarget, true));
                break;

            case ConnectionStyle.Orthogonal:
                {
                    double margin = OrthogonalMargin;

                    Vector GetOutwardVector(string side)
                    {
                        if (side == "Left") return new Vector(-1, 0);
                        if (side == "Right") return new Vector(1, 0);
                        if (side == "Top") return new Vector(0, -1);
                        if (side == "Bottom") return new Vector(0, 1);
                        return new Vector(1, 0);
                    }

                    Vector v1 = GetOutwardVector(sSide);
                    Vector v2 = GetOutwardVector(tSide);

                    Point p1Out = new Point(p1x + v1.X * margin, p1y + v1.Y * margin);
                    Point p2Out = new Point(p2x + v2.X * margin, p2y + v2.Y * margin);

                    figure.Segments.Add(new LineSegment(p1Out, true));

                    bool isHoriz1 = sSide == "Left" || sSide == "Right";
                    bool isHoriz2 = tSide == "Left" || tSide == "Right";

                    if (isHoriz1 && isHoriz2)
                    {
                        double midX = (p1Out.X + p2Out.X) / 2.0;
                        if (sSide == "Right" && p1Out.X <= p2Out.X || sSide == "Left" && p1Out.X >= p2Out.X)
                        {
                            figure.Segments.Add(new LineSegment(new Point(midX, p1Out.Y), true));
                            figure.Segments.Add(new LineSegment(new Point(midX, p2Out.Y), true));
                        }
                        else
                        {
                            double midY = (p1Out.Y + p2Out.Y) / 2.0;
                            if (Math.Abs(p1Out.Y - p2Out.Y) < 60) midY = Math.Max(p1Out.Y, p2Out.Y) + 60;
                            figure.Segments.Add(new LineSegment(new Point(p1Out.X, midY), true));
                            figure.Segments.Add(new LineSegment(new Point(p2Out.X, midY), true));
                        }
                    }
                    else if (!isHoriz1 && !isHoriz2)
                    {
                        double midY = (p1Out.Y + p2Out.Y) / 2.0;
                        if (sSide == "Bottom" && p1Out.Y <= p2Out.Y || sSide == "Top" && p1Out.Y >= p2Out.Y)
                        {
                            figure.Segments.Add(new LineSegment(new Point(p1Out.X, midY), true));
                            figure.Segments.Add(new LineSegment(new Point(p2Out.X, midY), true));
                        }
                        else
                        {
                            double midX = (p1Out.X + p2Out.X) / 2.0;
                            if (Math.Abs(p1Out.X - p2Out.X) < 60) midX = Math.Max(p1Out.X, p2Out.X) + 60;
                            figure.Segments.Add(new LineSegment(new Point(midX, p1Out.Y), true));
                            figure.Segments.Add(new LineSegment(new Point(midX, p2Out.Y), true));
                        }
                    }
                    else
                    {
                        // 混合方向（一横一纵）：优先采用完美的 L 型或 U 型正交走线
                        // 彻底告别丑陋的“小闪电”折角

                        if (isHoriz1) // 横向出，纵向入 (例如 Right -> Top)
                        {
                            // 判断连线是否顺畅前进（目标没有跑到源的背后）
                            bool isForwardX = (p2Out.X - p1Out.X) * v1.X >= 0;
                            bool isForwardY = (p2Out.Y - p1Out.Y) * -v2.Y >= 0;

                            if (isForwardX && isForwardY)
                            {
                                // 顺畅！直接生成完美的单 L 型拐点
                                figure.Segments.Add(new LineSegment(new Point(p2Out.X, p1Out.Y), true));
                            }
                            else
                            {
                                // 需要绕行避让，采用另一个对角顶点，自然形成图2完美的 U 型包裹
                                figure.Segments.Add(new LineSegment(new Point(p1Out.X, p2Out.Y), true));
                            }
                        }
                        else // 纵向出，横向入 (例如图1中的 Bottom -> Left)
                        {
                            bool isForwardY = (p2Out.Y - p1Out.Y) * v1.Y >= 0;
                            bool isForwardX = (p2Out.X - p1Out.X) * -v2.X >= 0;

                            if (isForwardX && isForwardY)
                            {
                                // 顺畅！直接生成完美的单 L 型拐点
                                figure.Segments.Add(new LineSegment(new Point(p1Out.X, p2Out.Y), true));
                            }
                            else
                            {
                                // 需要绕行避让，采用另一个对角顶点，自然形成图2完美的 U 型包裹
                                figure.Segments.Add(new LineSegment(new Point(p2Out.X, p1Out.Y), true));
                            }
                        }
                    }

                    figure.Segments.Add(new LineSegment(p2Out, true));
                    figure.Segments.Add(new LineSegment(adjustedTarget, true));
                }
                break;

            case ConnectionStyle.Circuit:
                // 简单的带圆角的直角连线（电路风格）
                double corner = CornerRadius;
                if (isHorizontal)
                {
                    double midX = (p1x + p2x) / 2.0;
                    if (Math.Abs(p1x - p2x) > corner * 2 && Math.Abs(p1y - p2y) > corner * 2)
                    {
                        double dirY = p2y > p1y ? 1 : -1;
                        double dirX = p2x > p1x ? 1 : -1;

                        figure.Segments.Add(new LineSegment(new Point(midX - corner * dirX, p1y), true));
                        figure.Segments.Add(new ArcSegment(new Point(midX, p1y + corner * dirY), new Size(corner, corner), 0, false, dirX * dirY > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
                        figure.Segments.Add(new LineSegment(new Point(midX, p2y - corner * dirY), true));
                        figure.Segments.Add(new ArcSegment(new Point(midX + corner * dirX, p2y), new Size(corner, corner), 0, false, dirX * dirY < 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
                        figure.Segments.Add(new LineSegment(adjustedTarget, true));
                    }
                    else
                    {
                        figure.Segments.Add(new LineSegment(new Point(midX, p1y), true));
                        figure.Segments.Add(new LineSegment(new Point(midX, p2y), true));
                        figure.Segments.Add(new LineSegment(adjustedTarget, true));
                    }
                }
                else
                {
                    double midY = (p1y + p2y) / 2.0;
                    if (Math.Abs(p1x - p2x) > corner * 2 && Math.Abs(p1y - p2y) > corner * 2)
                    {
                        double dirY = p2y > p1y ? 1 : -1;
                        double dirX = p2x > p1x ? 1 : -1;

                        figure.Segments.Add(new LineSegment(new Point(p1x, midY - corner * dirY), true));
                        figure.Segments.Add(new ArcSegment(new Point(p1x + corner * dirX, midY), new Size(corner, corner), 0, false, dirX * dirY > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true));
                        figure.Segments.Add(new LineSegment(new Point(p2x - corner * dirX, midY), true));
                        figure.Segments.Add(new ArcSegment(new Point(p2x, midY + corner * dirY), new Size(corner, corner), 0, false, dirX * dirY < 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true));
                        figure.Segments.Add(new LineSegment(adjustedTarget, true));
                    }
                    else
                    {
                        figure.Segments.Add(new LineSegment(new Point(p1x, midY), true));
                        figure.Segments.Add(new LineSegment(new Point(p2x, midY), true));
                        figure.Segments.Add(new LineSegment(adjustedTarget, true));
                    }
                }
                break;

            case ConnectionStyle.Bezier:
            default:
                if (isHorizontal)
                {
                    double dist = Math.Max(Math.Abs(p2x - p1x) / 2.0, BezierDistance);
                    figure.Segments.Add(new BezierSegment(new Point(p1x + dist, p1y), new Point(p2x - dist, p2y), adjustedTarget, true));
                }
                else
                {
                    double dist = Math.Max(Math.Abs(p2y - p1y) / 2.0, BezierDistance);
                    figure.Segments.Add(new BezierSegment(new Point(p1x, p1y + dist), new Point(p2x, p2y - dist), adjustedTarget, true));
                }
                break;
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        PathData = geometry;
        AdjustedTarget = adjustedTarget;
    }

    // ── 连线几何数据 ──
    public static readonly DependencyProperty PathDataProperty =
        DependencyProperty.Register("PathData", typeof(Geometry), typeof(KwyConnection), new PropertyMetadata(null));

    public Geometry PathData
    {
        get => (Geometry)GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    // ── 起始点 ──
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register("Source", typeof(Point), typeof(KwyConnection),
            new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public Point Source
    {
        get => (Point)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // ── 终点 ──
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register("Target", typeof(Point), typeof(KwyConnection),
            new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public Point Target
    {
        get => (Point)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    // ── 连线颜色 ──
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register("Stroke", typeof(Brush), typeof(KwyConnection), new PropertyMetadata(Brushes.DodgerBlue));

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    // ── 连线粗细 ──
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register("StrokeThickness", typeof(double), typeof(KwyConnection), new PropertyMetadata(2.0));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    // ── 连线风格 ──
    public static readonly DependencyProperty ConnectionStyleProperty =
        DependencyProperty.Register("ConnectionStyle", typeof(ConnectionStyle), typeof(KwyConnection),
            new FrameworkPropertyMetadata(ConnectionStyle.Bezier, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public ConnectionStyle ConnectionStyle
    {
        get => (ConnectionStyle)GetValue(ConnectionStyleProperty);
        set => SetValue(ConnectionStyleProperty, value);
    }

    // ── 布局方向 ──
    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register("Direction", typeof(Orientation), typeof(KwyConnection),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public Orientation Direction
    {
        get => (Orientation)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    // ── 选中状态 ──
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(KwyConnection), new PropertyMetadata(false));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    #region Mouse Logic

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var editor = FindParent<KwyEditor>(this);
        if (editor != null)
        {
            // 仅通知编辑器选中项改变，由 ViewModel 统一维护 IsSelected 状态
            editor.Focus();
            editor.SelectedItem = DataContext;
            e.Handled = true;
        }
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = VisualTreeHelper.GetParent(child);
        while (parentObject != null)
        {
            if (parentObject is T parent) return parent;
            parentObject = VisualTreeHelper.GetParent(parentObject);
        }
        return null;
    }

    #endregion Mouse Logic
}