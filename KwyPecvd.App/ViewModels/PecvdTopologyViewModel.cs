using Kwy.MVVM.Core;
using Kwy.UI.Diagrams;
using System.Collections.ObjectModel;

namespace KwyPecvd.App.ViewModels;

/// <summary>
/// PECVD 设备拓扑的应用层组合模型。
/// </summary>
public class PecvdTopologyViewModel : BindableBase
{
    private const double ChamberRadius = 112d;
    private const double PumpHousingHeight = 88d;
    private DiagramNode? selectedNode;
    private int refreshVersion;
    private DelegateCommand<DiagramNode>? selectNodeCommand;
    private DelegateCommand? refreshDiagramCommand;

    public PecvdTopologyViewModel()
    {
        const double centerX = 360d;
        const double chamberCenterY = 230d;
        const double transferWidth = 600d;
        const double endModuleInset = 48d;
        const double loadPortPitch = 100d;
        double leftModuleX = centerX - transferWidth / 2d + endModuleInset;
        double rightModuleX = centerX + transferWidth / 2d - endModuleInset;
        var chamberCenter = new DiagramPoint(centerX, chamberCenterY);

        Nodes = [];

        // 先绘制主体和外壳，确保上层状态节点及标签始终清晰可见。
        Nodes.Add(CreateCenteredNode("transfer-chamber", new DiagramPoint(centerX, 512d), transferWidth, 144d, "", DiagramNodeShape.Rectangle, DiagramNodeState.Active, fillMode: DiagramNodeFillMode.SoftState));
        // 两侧负载锁定舱的固定轮廓由视图绘制，状态圆仍由节点集合驱动。
        Nodes.Add(CreateCenteredNode("chamber-shell", chamberCenter, 224d, 224d, "", DiagramNodeShape.RegularPolygon, DiagramNodeState.Active, polygonSides: 8, fillMode: DiagramNodeFillMode.SoftState));
        Nodes.Add(CreateCenteredNode("chamber-inner", chamberCenter, 206d, 206d, "", DiagramNodeShape.RegularPolygon,
            polygonSides: 8));
        // 机械手整体居中，底座位于工作区中心下方；与视图模板底座位置一致。
        Nodes.Add(CreateCenteredNode("vacuum-robot", new DiagramPoint(centerX, chamberCenterY + 30d), 38d, 38d, "", DiagramNodeShape.Ellipse,
            DiagramNodeState.Active, fillMode: DiagramNodeFillMode.SoftState));


        AddPump("pm-1", "PM1", 157.5d, DiagramNodeState.Warning, new DiagramPoint(128d, 300d), chamberCenter);
        AddPump("pm-2", "PM2", 202.5d, DiagramNodeState.Active, new DiagramPoint(128d, 170d), chamberCenter);
        AddPump("pm-3", "PM3", 247.5d, DiagramNodeState.Active, new DiagramPoint(213d, 22d), chamberCenter);
        AddPump("pm-4", "PM4", 292.5d, DiagramNodeState.Active, new DiagramPoint(507d, 22d), chamberCenter);
        AddPump("pm-5", "PM5", 337.5d, DiagramNodeState.Active, new DiagramPoint(592d, 170d), chamberCenter);
        AddPump("pm-6", "PM6", 22.5d, DiagramNodeState.Active, new DiagramPoint(592d, 300d), chamberCenter);

        AddLowerModule("la", "LA", centerX - 116d);
        AddLowerModule("lb", "LB", centerX - 40d);
        AddLowerModule("lc", "LC", centerX + 40d);
        AddLowerModule("ld", "LD", centerX + 116d);
        Nodes.Add(CreateCenteredNode("aligner-housing", new DiagramPoint(leftModuleX, 512d), 72d, 72d, "", DiagramNodeShape.Rectangle));
        Nodes.Add(CreateCenteredNode("aligner", new DiagramPoint(leftModuleX, 512d), 68d, 68d, "Aligner", DiagramNodeShape.Ellipse));
        Nodes.Add(CreateCenteredNode("cooling", new DiagramPoint(rightModuleX, 512d), 60d, 60d, "Cool", DiagramNodeShape.Ellipse));
        Nodes.Add(CreateCenteredNode("bf-left-2", new DiagramPoint(leftModuleX, 460d), 68d, 28d, "BF2", DiagramNodeShape.RoundedRectangle));
        Nodes.Add(CreateCenteredNode("bf-left-1", new DiagramPoint(leftModuleX, 564d), 68d, 28d, "BF1", DiagramNodeShape.RoundedRectangle));
        Nodes.Add(CreateCenteredNode("bf-right-3", new DiagramPoint(rightModuleX, 460d), 68d, 28d, "BF3", DiagramNodeShape.RoundedRectangle));
        Nodes.Add(CreateCenteredNode("bf-right-1", new DiagramPoint(rightModuleX, 564d), 68d, 28d, "BF1", DiagramNodeShape.RoundedRectangle));

        // 占用仅为示例数据；应用接入时由设备状态驱动。
        AddLoadPort("dummy", "Dummy", centerX - 2d * loadPortPitch, isActive: true);
        AddLoadPort("lp-1", "LP1", centerX - loadPortPitch, isActive: true);
        AddLoadPort("lp-2", "LP2", centerX, isActive: true);
        AddLoadPort("lp-3", "LP3", centerX + loadPortPitch, isActive: true);
        AddLoadPort("lp-4", "LP4", centerX + 2d * loadPortPitch, isActive: false);

        // PM 壳体底边直接贴合腔体边，不再叠加连接臂。
        Connections = [];

        DiagramOffset = new DiagramPoint(8d, 8d);
    }

    public ObservableCollection<DiagramNode> Nodes { get; }
    public ObservableCollection<DiagramConnection> Connections { get; }
    public DiagramPoint DiagramOffset { get; }

    public DiagramNode? SelectedNode
    {
        get => selectedNode;
        set
        {
            if (SetProperty(ref selectedNode, value))
            {
                RaisePropertyChanged(nameof(SelectedNodeText));
            }
        }
    }

    public string SelectedNodeText
        => SelectedNode is null ? "请选择图中的节点。" : $"{(string.IsNullOrWhiteSpace(SelectedNode.Label) ? SelectedNode.Id : SelectedNode.Label)}（{SelectedNode.State}）";

    public DelegateCommand<DiagramNode> SelectNodeCommand
        => selectNodeCommand ??= new DelegateCommand<DiagramNode>(node => SelectedNode = node);

    public DelegateCommand RefreshDiagramCommand
        => refreshDiagramCommand ??= new DelegateCommand(RefreshDiagram);

    private void RefreshDiagram()
    {
        refreshVersion++;
        DiagramNodeState state = (refreshVersion % 3) switch
        {
            0 => DiagramNodeState.Active,
            1 => DiagramNodeState.Warning,
            _ => DiagramNodeState.Error
        };

        int index = Nodes.IndexOf(Nodes.First(node => node.Id == "pm-4"));
        bool wasSelected = SelectedNode?.Id == "pm-4";
        Nodes[index] = Nodes[index] with { State = state };
        if (wasSelected)
        {
            SelectedNode = Nodes[index];
        }
    }

    private static DiagramNode CreatePump(string id, string label, DiagramPoint center, double size, DiagramNodeState state)
        => new(
            id,
            center.X - (size / 2d),
            center.Y - (size / 2d),
            size,
            size,
            label,
            state,
            DiagramNodeShape.Ellipse,
            UseStateFill: true);

    private void AddPump(
        string id,
        string label,
        double outwardAngle,
        DiagramNodeState state,
        DiagramPoint statusCenter,
        DiagramPoint chamberCenter)
    {
        // 正八边形的边中点位于内切圆上；外壳底边沿边法线向外排布。
        double radians = outwardAngle * Math.PI / 180d;
        double normalX = Math.Cos(radians);
        double normalY = Math.Sin(radians);
        double apothem = ChamberRadius * Math.Cos(Math.PI / 8d);
        double housingWidth = 2d * ChamberRadius * Math.Sin(Math.PI / 8d);
        double housingDistance = apothem + PumpHousingHeight / 2d;
        var center = new DiagramPoint(
            chamberCenter.X + normalX * housingDistance,
            chamberCenter.Y + normalY * housingDistance);
        // 圆形核心与外壳半圆共圆心，文字保持水平。
        double coreDistance = apothem + PumpHousingHeight - housingWidth / 2d;
        var coreCenter = new DiagramPoint(
            chamberCenter.X + normalX * coreDistance,
            chamberCenter.Y + normalY * coreDistance);
        double rotationAngle = outwardAngle + 90d;

        Nodes.Add(CreateCenteredNode(
            $"{id}-housing",
            center,
            housingWidth,
            PumpHousingHeight,
            "",
            DiagramNodeShape.SemiEllipse,
            DiagramNodeState.Active,
            rotationAngle: rotationAngle,
            fillMode: DiagramNodeFillMode.SoftState));
        Nodes.Add(CreatePump(id, label, coreCenter, 62d, state));
        Nodes.Add(CreateCenteredNode($"{id}-pressure", statusCenter, 94d, 24d, "1000mTorr", DiagramNodeShape.RoundedRectangle));
        Nodes.Add(CreateCenteredNode(
            $"{id}-engineer",
            new DiagramPoint(statusCenter.X, statusCenter.Y + 27d),
            90d,
            24d,
            "Engineer",
            DiagramNodeShape.RoundedRectangle,
            DiagramNodeState.Active,
            fillMode: DiagramNodeFillMode.SoftState));
    }

    private void AddLowerModule(string id, string label, double centerX)
    {
        Nodes.Add(CreatePump(id, label, new DiagramPoint(centerX, 400d), 58d, DiagramNodeState.Active));
    }

    private void AddLoadPort(string id, string label, double centerX, bool isActive)
    {
        DiagramNodeState state = isActive ? DiagramNodeState.Active : DiagramNodeState.Disabled;
        Nodes.Add(CreateCenteredNode($"{id}-housing", new DiagramPoint(centerX, 620d),
            80d, 72d, "", DiagramNodeShape.RoundedRectangle, state, fillMode: DiagramNodeFillMode.SoftState));
        Nodes.Add(CreateCenteredNode($"{id}-dock", new DiagramPoint(centerX, 587d),
            68d, 6d, "", DiagramNodeShape.Rectangle, state, useStateFill: true));
        Nodes.Add(CreateCenteredNode(id, new DiagramPoint(centerX, 623d), 60d, 60d,
            label, DiagramNodeShape.Ellipse, state,
            fillMode: isActive ? DiagramNodeFillMode.State : DiagramNodeFillMode.SoftState));
    }

    private static DiagramNode CreateCenteredNode(
        string id,
        DiagramPoint center,
        double width,
        double height,
        string label,
        DiagramNodeShape shape,
        DiagramNodeState state = DiagramNodeState.Normal,
        bool useStateFill = false,
        int polygonSides = 0,
        DiagramNodeOrientation orientation = DiagramNodeOrientation.Top,
        double rotationAngle = 0d,
        DiagramNodeFillMode fillMode = DiagramNodeFillMode.Default)
        => new(
            id,
            center.X - (width / 2d),
            center.Y - (height / 2d),
            width,
            height,
            label,
            state,
            shape,
            Orientation: orientation,
            PolygonSides: polygonSides,
            RotationAngle: rotationAngle,
            UseStateFill: useStateFill,
            FillMode: fillMode);
}
