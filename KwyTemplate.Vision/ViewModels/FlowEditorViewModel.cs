using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;
using KwyTemplate.Vision.Registries;
using KwyTemplate.Vision.Services;
using KwyTemplate.Vision.ViewModels.Items;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using Kwy.UI.WPF.Components;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Services.FileDialogs;
using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Results;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

using KwyTemplate.Vision.Executors;
using Kwy.Vision.WPF.Sources;
using KwyTemplate.Vision.Batch;

namespace KwyTemplate.Vision.ViewModels;

/// <summary>
/// 流程编辑器 ViewModel
/// </summary>
public class FlowEditorViewModel : BindableBase, INavigationAware
{
    private readonly FlowPersistenceService persistence;
    private readonly DataTypeColorService colorService;
    private readonly List<IFlowNodeDescriptor> descriptors;
    private readonly Dictionary<string, IFlowNodeDescriptor> descriptorMap;
    private readonly FlowNodeExecutorRegistry executorRegistry;
    private readonly Services.FlowExecutionService executionService;
    private readonly Services.FlowLayoutService layoutService;
    private readonly RecentProjectService recentProjectService;
    private readonly IDialogMessageService dialogMessageService;
    private readonly IFileDialogService fileDialogService;
    private readonly IVisionFrameSourceFactory frameSourceFactory;

    // ── 私有数据模型 ────────────────────────────────────────────────────────────────────────────
    private FlowProject project = new();

    private string? currentFilePath;

    public BulkObservableCollection<FlowGraph> ProjectGraphs { get; } = new();

    public BulkObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = new();

    public string ProjectName => project.Name;

    public string ProjectFileDisplay
        => string.IsNullOrWhiteSpace(currentFilePath)
            ? "未保存到文件"
            : currentFilePath;

    public string ProjectSaveStateText
        => IsDirty ? "未保存" : "已保存";

    public BulkObservableCollection<FlowRunLogEntry> ExecutionLogs { get; } = new();

    public BulkObservableCollection<FlowNodeRunRecord> LatestNodeResults { get; } = new();

    private FlowNodeRunRecord? selectedNodeRunRecord;

    public FlowNodeRunRecord? SelectedNodeRunRecord
    {
        get => selectedNodeRunRecord;
        set
        {
            if (SetProperty(ref selectedNodeRunRecord, value))
            {
                LocateNodeRunRecord(value);
            }
        }
    }

    public BulkObservableCollection<FlowPortValueSnapshot> RuntimeVariables { get; } = new();

    public BulkObservableCollection<VisionImagePanelItemViewModel> RuntimeImages { get; } = new();

    public BulkObservableCollection<VisionImagePanelItemViewModel> SelectedNodeImages { get; } = new();

    public BulkObservableCollection<RoiRegionViewModel> RoiRegions { get; } = new();

    public BulkObservableCollection<VisionBatchRunRecord> BatchRunRecords { get; } = new();

    private VisionBatchRunRecord? selectedBatchRunRecord;

    public VisionBatchRunRecord? SelectedBatchRunRecord
    {
        get => selectedBatchRunRecord;
        set
        {
            if (SetProperty(ref selectedBatchRunRecord, value))
            {
                ShowBatchRunRecordImage(value);
            }
        }
    }

    private VisionImagePanelItemViewModel? selectedImagePanelItem;

    public VisionImagePanelItemViewModel? SelectedImagePanelItem
    {
        get => selectedImagePanelItem;
        set => SetProperty(ref selectedImagePanelItem, value);
    }

    private Rect? currentRoi;
    private bool isLoadingRoiFromNode;
    private bool isSyncingRoiRegionSelection;

    public Rect? CurrentRoi
    {
        get => currentRoi;
        set
        {
            if (SetProperty(ref currentRoi, value) && !isLoadingRoiFromNode)
            {
                SyncCurrentRoiToSelectedNode();
                SyncRoiListFromCurrentRoi();
            }
        }
    }

    private RoiRegionViewModel? selectedRoiRegion;

    public RoiRegionViewModel? SelectedRoiRegion
    {
        get => selectedRoiRegion;
        set
        {
            if (SetProperty(ref selectedRoiRegion, value) && !isSyncingRoiRegionSelection && value != null)
            {
                CurrentRoi = value.Bounds;
            }
        }
    }

    /// <summary>当前打开的所有标签页</summary>
    public BulkObservableCollection<FlowGraphViewModel> OpenTabs { get; } = new();

    private FlowGraphViewModel? activeTab;

    public FlowGraphViewModel? ActiveTab
    {
        get => activeTab;
        set
        {
            if (SetProperty(ref activeTab, value))
            {
                if (value != null)
                {
                    // 同步到项目集合的选中状态，但不触发二次 Load
                    activeGraph = value.Graph;
                    RaisePropertyChanged(nameof(ActiveGraph));
                    RaisePropertyChanged(nameof(GraphName));
                    UpdateTitle();

                    // 切换标签时，同步属性面板
                PropertySettings.UpdateSelection(
                    value.SelectedItem as FlowNodeViewModel,
                    value.Connections.Where(c => c.IsSelected),
                    value.Name,
                    value);
                RefreshSelectedNodeImages(value.SelectedItem as FlowNodeViewModel);
                LoadCurrentRoiFromNode(value.SelectedItem as FlowNodeViewModel);
            }
        }
        }
    }

    private FlowGraph? activeGraph;

    public FlowGraph? ActiveGraph
    {
        get => activeGraph;
        set
        {
            if (SetProperty(ref activeGraph, value) && value != null)
            {
                // 如果该图已经在标签页中，则切换过去；否则新增一个标签页
                var existingTab = OpenTabs.FirstOrDefault(t => t.Graph == value);
                if (existingTab != null)
                {
                    ActiveTab = existingTab;
                }
                else
                {
                    LoadGraphInternal(value);
                }
            }
        }
    }

    public BulkObservableCollection<FlowNodeViewModel> Nodes => ActiveTab?.Nodes ?? new();
    public BulkObservableCollection<FlowConnectionViewModel> Connections => ActiveTab?.Connections ?? new();

    // ── 节点调色板 ───────────────────────────────────────────────────────────────────────────────
    public NodePaletteViewModel Palette { get; }

    /// <summary>属性面板 ViewModel</summary>
    public PropertySettingsViewModel PropertySettings { get; } = new();

    // ── 窗口标题状态 ─────────────────────────────────────────────────────────────────────────────
    private string title = "流程图";

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    private bool isDirty;

    public bool IsDirty
    {
        get => isDirty;
        set
        {
            if (SetProperty(ref isDirty, value))
            {
                UpdateTitle();
                RaisePropertyChanged(nameof(ProjectSaveStateText));
            }
        }
    }

    public string GraphName
    {
        get => activeGraph?.Name ?? "未命名流程";
        set
        {
            if (activeGraph != null && activeGraph.Name != value)
            {
                activeGraph.Name = value;
                if (ActiveTab != null) ActiveTab.Name = value;
                RaisePropertyChanged(nameof(GraphName));
                IsDirty = true;
                UpdateTitle();
                PropertySettings.GraphName = value;
            }
        }
    }

    private string runStatusText = "就绪";

    public string RunStatusText
    {
        get => runStatusText;
        set => SetProperty(ref runStatusText, value);
    }

    private bool isFlowRunning;
    private bool isPollingRun;

    public bool IsFlowRunning
    {
        get => isFlowRunning;
        set => SetProperty(ref isFlowRunning, value);
    }

    public bool IsPollingRun
    {
        get => isPollingRun;
        set => SetProperty(ref isPollingRun, value);
    }

    private FlowGraph? _renamingGraph;

    /// <summary>当前正在重命名的流程图（用于 UI 切换编辑状态）</summary>
    public FlowGraph? RenamingGraph
    {
        get => _renamingGraph;
        set => SetProperty(ref _renamingGraph, value);
    }

    // ── 选中对象（支持节点或连线，通过 KwyEditor.SelectedItem 绑定） ───────────────────────────
    public object? SelectedNode
    {
        get => ActiveTab?.SelectedItem;
        set
        {
            if (ActiveTab == null) return;

            if (value is FlowNodeViewModel node)
            {
                SelectNode(node, IsMultiSelectGesture());
            }
            else if (value is FlowConnectionViewModel conn)
            {
                SelectConnection(conn);
            }
            else if (value == null)
            {
                ClearSelection();
            }
        }
    }

    // ── 构造 ─────────────────────────────────────────────────────────────────────────────────────
    public FlowEditorViewModel(
        FlowNodeRegistry registry,
        FlowNodeExecutorRegistry executorRegistry,
        Services.FlowExecutionService executionService,
        Services.FlowLayoutService layoutService,
        FlowPersistenceService persistence,
        RecentProjectService recentProjectService,
        DataTypeColorService colorService,
        IVisionFrameSourceFactory frameSourceFactory,
        IDialogMessageService dialogMessageService,
        IFileDialogService fileDialogService)
    {
        this.descriptors = registry.All.ToList();
        descriptorMap = descriptors.ToDictionary(item => item.NodeType, StringComparer.OrdinalIgnoreCase);
        this.executorRegistry = executorRegistry;
        this.executionService = executionService;
        this.layoutService = layoutService;
        this.persistence = persistence;
        this.recentProjectService = recentProjectService;
        this.colorService = colorService;
        this.frameSourceFactory = frameSourceFactory;
        this.dialogMessageService = dialogMessageService;
        this.fileDialogService = fileDialogService;

        Palette = new NodePaletteViewModel(descriptors);
        Palette.OnNodeAddRequested = (nodeType, dropPoint) =>
        {
            var pos = dropPoint ?? new Point(100 + Nodes.Count * 20, 80 + Nodes.Count * 20);
            AddNodeAt(nodeType, pos);
        };

        // 订阅属性面板对全局参数的修改
        PropertySettings.GraphNameChanged += name => GraphName = name;

        // 程序启动时默认创建一个新项目/流程图
        NewGraphCore();
        RefreshRecentProjects();
    }

    private void SubscribeCollections()
    {
        Nodes.CollectionChanged += (_, _) => IsDirty = true;
        Connections.CollectionChanged += (_, _) => IsDirty = true;
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // Nodify v7 命令绑定
    // ────────────────────────────────────────────────────────────────────────────────────────────
    // NodifyEditor.ConnectionStartedCommand 当用户从端口开始拖拽时触发
    //   CommandParameter = 起始 connector 对象（此处为 PortViewModel）
    //
    // NodifyEditor.ConnectionCompletedCommand 当用户释放鼠标完成连线时触发
    //   CommandParameter = (source connector, target connector) 元组
    //
    // NodifyEditor.DisconnectConnectorCommand 当右键"删除连线"或 Ctrl+Click 端口时触发
    //   CommandParameter = 要断开连接的 connector 对象

    private DelegateCommand<object>? connectStartedCommand;

    public DelegateCommand<object> ConnectStartedCommand
        => connectStartedCommand ??= new DelegateCommand<object>(_ => { /* 暂未使用，预留事件 pending source */ });

    private DelegateCommand<object>? connectCompletedCommand;

    public DelegateCommand<object> ConnectCompletedCommand
        => connectCompletedCommand ??= new DelegateCommand<object>(OnConnectCompleted);

    private async void OnConnectCompleted(object? param)
    {
        // Nodify v7 CompletedCommand parameter: (source, target) 是 两个 connector 对象
        if (param is not ValueTuple<object, object> tuple) return;
        if (tuple.Item1 is not PortViewModel src) return;
        if (tuple.Item2 is not PortViewModel tgt) return;

        var result = TryConnect(src, tgt);
        if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            await dialogMessageService.ShowWarningAsync(result.ErrorMessage, "连接失败");
        }
    }

    private DelegateCommand<object>? disconnectConnectorCommand;

    public DelegateCommand<object> DisconnectConnectorCommand
        => disconnectConnectorCommand ??= new DelegateCommand<object>(OnDisconnectConnector);

    private void OnDisconnectConnector(object? param)
    {
        if (param is not PortViewModel port) return;
        var toRemove = Connections
            .Where(c => c.Source.PortId == port.PortId || c.Target.PortId == port.PortId)
            .ToList();
        foreach (var c in toRemove) RemoveConnection(c);
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // 文件操作命令
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private DelegateCommand? newCommand;
    public DelegateCommand NewCommand => newCommand ??= new DelegateCommand(async () => await NewGraphAsync());

    private DelegateCommand? openCommand;
    public DelegateCommand OpenCommand => openCommand ??= new DelegateCommand(async () => await OpenGraphAsync());

    private DelegateCommand? saveCommand;
    public DelegateCommand SaveCommand => saveCommand ??= new DelegateCommand(async () => await SaveGraphAsync());

    private DelegateCommand? saveAsCommand;
    public DelegateCommand SaveAsCommand => saveAsCommand ??= new DelegateCommand(async () => await SaveGraphAsAsync());

    private DelegateCommand<string>? openRecentProjectCommand;

    public DelegateCommand<string> OpenRecentProjectCommand
        => openRecentProjectCommand ??= new DelegateCommand<string>(async filePath => await OpenRecentProjectAsync(filePath));

    private DelegateCommand? clearRecentProjectsCommand;

    public DelegateCommand ClearRecentProjectsCommand
        => clearRecentProjectsCommand ??= new DelegateCommand(() =>
        {
            recentProjectService.Clear();
            RefreshRecentProjects();
        });

    private DelegateCommand? createYoloSampleFlowCommand;

    public DelegateCommand CreateYoloSampleFlowCommand
        => createYoloSampleFlowCommand ??= new DelegateCommand(async () => await CreateYoloSampleFlowAsync());

    private DelegateCommand? runCommand;
    public DelegateCommand RunCommand => runCommand ??= new DelegateCommand(async () => await ExecuteProjectAsync());

    private DelegateCommand? runActiveGraphCommand;

    public DelegateCommand RunActiveGraphCommand => runActiveGraphCommand ??= new DelegateCommand(async () => await ExecuteFlowAsync(false));

    private DelegateCommand<FlowNodeViewModel>? runToNodeCommand;

    public DelegateCommand<FlowNodeViewModel> RunToNodeCommand
        => runToNodeCommand ??= new DelegateCommand<FlowNodeViewModel>(async node => await ExecuteFlowAsync(false, node?.NodeId));

    private DelegateCommand<FlowConnectionViewModel>? debugToConnectionCommand;

    public DelegateCommand<FlowConnectionViewModel> DebugToConnectionCommand
        => debugToConnectionCommand ??= new DelegateCommand<FlowConnectionViewModel>(async connection => await ExecuteFlowAsync(true, connection?.Source.Node.NodeId));

    private DelegateCommand? pollingRunCommand;

    public DelegateCommand PollingRunCommand => pollingRunCommand ??= new DelegateCommand(async () => await ExecutePollingFlowAsync());

    private DelegateCommand? batchRunCommand;

    public DelegateCommand BatchRunCommand => batchRunCommand ??= new DelegateCommand(async () => await ExecuteBatchRunAsync());

    private DelegateCommand? exportRunReportCommand;

    public DelegateCommand ExportRunReportCommand => exportRunReportCommand ??= new DelegateCommand(async () => await ExportRunReportAsync());

    private DelegateCommand? debugCommand;
    public DelegateCommand DebugCommand => debugCommand ??= new DelegateCommand(async () => await ExecuteFlowAsync(true));

    private DelegateCommand? stopCommand;
    public DelegateCommand StopCommand => stopCommand ??= new DelegateCommand(() =>
    {
        IsPollingRun = false;
        executionService.Stop();
    });

    private DelegateCommand? continueCommand;
    public DelegateCommand ContinueCommand => continueCommand ??= new DelegateCommand(() => executionService.Continue());

    private DelegateCommand? stepCommand;
    public DelegateCommand StepCommand => stepCommand ??= new DelegateCommand(() => executionService.Step());

    private DelegateCommand? addRoiRegionCommand;

    public DelegateCommand AddRoiRegionCommand => addRoiRegionCommand ??= new DelegateCommand(AddRoiRegion);

    private DelegateCommand<RoiRegionViewModel>? duplicateRoiRegionCommand;

    public DelegateCommand<RoiRegionViewModel> DuplicateRoiRegionCommand
        => duplicateRoiRegionCommand ??= new DelegateCommand<RoiRegionViewModel>(DuplicateRoiRegion);

    private DelegateCommand<RoiRegionViewModel>? toggleRoiRegionCommand;

    public DelegateCommand<RoiRegionViewModel> ToggleRoiRegionCommand
        => toggleRoiRegionCommand ??= new DelegateCommand<RoiRegionViewModel>(ToggleRoiRegion);

    private DelegateCommand<RoiRegionViewModel>? deleteRoiRegionCommand;

    public DelegateCommand<RoiRegionViewModel> DeleteRoiRegionCommand
        => deleteRoiRegionCommand ??= new DelegateCommand<RoiRegionViewModel>(DeleteRoiRegion);

    private DelegateCommand? clearCommand;
    public DelegateCommand ClearCommand => clearCommand ??= new DelegateCommand(async () => await ClearCanvasAsync());

    private DelegateCommand? autoLayoutCommand;

    public DelegateCommand AutoLayoutCommand => autoLayoutCommand ??= new DelegateCommand(() =>
    {
        if (Nodes.Count == 0 || layoutService == null) return;

        // 不再传递全局唯一方向，由 LayoutService 内部对每个连通分量进行智能识别
        layoutService.AutoLayoutNodes(Nodes, Connections, null);
        FitToScreenTriggerCount++;
    });

    private int _fitToScreenTriggerCount;

    public int FitToScreenTriggerCount
    {
        get => _fitToScreenTriggerCount;
        set => SetProperty(ref _fitToScreenTriggerCount, value);
    }

    private DelegateCommand? fitToScreenCommand;

    public DelegateCommand FitToScreenCommand => fitToScreenCommand ??= new DelegateCommand(() =>
    {
        FitToScreenTriggerCount++;
    });

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // 节点删除和 ContextMenu 命令
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private DelegateCommand<FlowNodeViewModel>? deleteNodeCommand;

    public DelegateCommand<FlowNodeViewModel> DeleteNodeCommand
        => deleteNodeCommand ??= new DelegateCommand<FlowNodeViewModel>(node =>
        {
            if (node == null) return;

            if (node.IsSelected)
            {
                var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
                foreach (var n in selectedNodes) DeleteNode(n);
                ClearSelection();
            }
            else
            {
                DeleteNode(node);
            }
        });

    // 节点选择（选中节点）
    private DelegateCommand<FlowNodeViewModel>? selectNodeCommand;

    public DelegateCommand<FlowNodeViewModel> SelectNodeCommand
        => selectNodeCommand ??= new DelegateCommand<FlowNodeViewModel>(node => SelectNode(node, IsMultiSelectGesture()));

    // ── 选中连线（LabVIEW 风格：支持多选）──
    private DelegateCommand<FlowConnectionViewModel>? selectConnectionCommand;

    public DelegateCommand<FlowConnectionViewModel> SelectConnectionCommand
        => selectConnectionCommand ??= new DelegateCommand<FlowConnectionViewModel>(SelectConnection);

    /// <summary>
    /// 选择连线（LabVIEW 风格：支持 Ctrl+Click 多选）
    /// </summary>
    private void SelectConnection(FlowConnectionViewModel? conn)
    {
        if (conn == null) return;

        bool isMultiSelect = IsMultiSelectGesture();

        if (isMultiSelect)
        {
            conn.IsSelected = !conn.IsSelected;
        }
        else
        {
            foreach (var c in Connections)
            {
                if (c != conn) c.IsSelected = false;
            }
            conn.IsSelected = true;
        }

        foreach (var n in Nodes) n.IsSelected = false;

        var activeConnection = conn.IsSelected ? conn : SelectedConnections.FirstOrDefault();
        SetEditorSelectedItem(activeConnection);
        RaisePropertyChanged(nameof(SelectedConnections));
        UpdateSelectionDetails(null);
    }

    private DelegateCommand<FlowGraphViewModel>? closeTabCommand;

    public DelegateCommand<FlowGraphViewModel> CloseTabCommand => closeTabCommand ??= new DelegateCommand<FlowGraphViewModel>(tab =>
    {
        if (tab == null) return;
        OpenTabs.Remove(tab);
        if (ActiveTab == tab)
        {
            ActiveTab = OpenTabs.LastOrDefault();
        }
    });

    public void ClearSelection()
    {
        if (ActiveTab == null) return;
        foreach (var c in ActiveTab.Connections) c.IsSelected = false;
        foreach (var n in ActiveTab.Nodes) n.IsSelected = false;

        SetEditorSelectedItem(null);
        RaisePropertyChanged(nameof(SelectedConnections));
        UpdateSelectionDetails(null);
    }

    /// <summary>
    /// 仅清除连线的选中状态
    /// </summary>
    /// <param name="except">要保留选中状态的连线（可选）</param>
    public void ClearConnectionSelection(FlowConnectionViewModel? except = null)
    {
        foreach (var c in Connections)
        {
            if (c != except) c.IsSelected = false;
        }
        RaisePropertyChanged(nameof(SelectedConnections));
        UpdateSelectionDetails(SelectedNode as FlowNodeViewModel);
    }

    /// <summary>当前选中的连线集合（用于属性面板显示）</summary>
    public IEnumerable<FlowConnectionViewModel> SelectedConnections => Connections.Where(c => c.IsSelected);

    private static bool IsMultiSelectGesture()
        => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

    private void SelectNode(FlowNodeViewModel? node, bool isMultiSelect)
    {
        if (ActiveTab == null || node == null) return;

        if (isMultiSelect)
        {
            foreach (var c in ActiveTab.Connections) c.IsSelected = false;
            node.IsSelected = !node.IsSelected;

            var activeNode = node.IsSelected
                ? node
                : ActiveTab.Nodes.FirstOrDefault(n => n.IsSelected);

            SetEditorSelectedItem(activeNode);
            RaisePropertyChanged(nameof(SelectedConnections));
            UpdateSelectionDetails(activeNode);
            return;
        }

        foreach (var n in ActiveTab.Nodes)
        {
            n.IsSelected = n == node;
        }

        foreach (var c in ActiveTab.Connections) c.IsSelected = false;

        SetEditorSelectedItem(node);
        RaisePropertyChanged(nameof(SelectedConnections));
        UpdateSelectionDetails(node);
    }

    private void SetEditorSelectedItem(object? item)
    {
        if (ActiveTab == null) return;
        ActiveTab.SelectedItem = item;
        RaisePropertyChanged(nameof(SelectedNode));
    }

    private void UpdateSelectionDetails(FlowNodeViewModel? node)
    {
        PropertySettings.UpdateSelection(node, SelectedConnections, GraphName, ActiveTab);
        RefreshSelectedNodeImages(node);
        LoadCurrentRoiFromNode(node);
    }

    // 连线删除（ContextMenu 命令）
    private DelegateCommand<FlowConnectionViewModel>? disconnectCommand;

    public DelegateCommand<FlowConnectionViewModel> DisconnectCommand
        => disconnectCommand ??= new DelegateCommand<FlowConnectionViewModel>(conn =>
        {
            if (conn == null) return;

            // 如果点击的连线就在当前选中的集合里，则进行批量删除
            if (conn.IsSelected)
            {
                var selectedConns = Connections.Where(c => c.IsSelected).ToList();
                foreach (var c in selectedConns) RemoveConnection(c);
            }
            else
            {
                // 否则，仅删除被点击的这条连线
                RemoveConnection(conn);
            }
        });

    // ── LabVIEW 风格功能扩展：连线与节点的 ContextMenu 命令 ──
    private DelegateCommand<FlowConnectionViewModel>? toggleBreakpointCommand;

    public DelegateCommand<FlowConnectionViewModel> ToggleBreakpointCommand
        => toggleBreakpointCommand ??= new DelegateCommand<FlowConnectionViewModel>(conn =>
        {
            if (conn == null) return;
            conn.HasBreakpoint = !conn.HasBreakpoint;
            IsDirty = true;
        });

    private DelegateCommand<FlowConnectionViewModel>? toggleProbeCommand;

    public DelegateCommand<FlowConnectionViewModel> ToggleProbeCommand
        => toggleProbeCommand ??= new DelegateCommand<FlowConnectionViewModel>(conn =>
        {
            if (conn == null) return;
            conn.HasProbe = !conn.HasProbe;
            IsDirty = true;
        });

    private DelegateCommand<PortViewModel>? togglePortProbeCommand;

    public DelegateCommand<PortViewModel> TogglePortProbeCommand
        => togglePortProbeCommand ??= new DelegateCommand<PortViewModel>(p =>
        {
            if (p == null) return;
            p.HasProbe = !p.HasProbe;
            IsDirty = true;
        });

    private DelegateCommand<FlowNodeViewModel>? toggleNodeDisableCommand;

    public DelegateCommand<FlowNodeViewModel> ToggleNodeDisableCommand
        => toggleNodeDisableCommand ??= new DelegateCommand<FlowNodeViewModel>(node =>
        {
            if (node == null) return;
            var targets = node.IsSelected
                ? Nodes.Where(n => n.IsSelected).ToList()
                : new List<FlowNodeViewModel> { node };
            var disabled = !node.IsDisabled;
            foreach (var target in targets)
            {
                target.IsDisabled = disabled;
            }
            IsDirty = true;
        });

    private DelegateCommand<FlowNodeViewModel>? executeNodeCommand;

    public DelegateCommand<FlowNodeViewModel> ExecuteNodeCommand
        => executeNodeCommand ??= new DelegateCommand<FlowNodeViewModel>(async node =>
        {
            if (node == null) return;
            try
            {
                if (activeGraph != null)
                {
                    var result = await executionService.ExecuteNodeInternalAsync(node, Connections, activeGraph);
                    if (!result.Success)
                    {
                        await dialogMessageService.ShowErrorAsync(result.ErrorMessage ?? "节点执行失败。", "运行异常");
                    }
                }
            }
            catch (Exception ex)
            {
                await dialogMessageService.ShowErrorAsync($"执行出错: {ex.Message}", "运行异常");
            }
        });

    /// <summary>
    /// 全局运行/调试流程
    /// </summary>
    private async Task ExecuteProjectAsync()
    {
        if (IsFlowRunning)
        {
            RunStatusText = "流程正在运行中";
            AddExecutionLog("Warn", RunStatusText);
            return;
        }

        if (ProjectGraphs.Count == 0)
        {
            RunStatusText = "当前项目没有流程";
            AddExecutionLog("Warn", RunStatusText);
            return;
        }

        foreach (FlowGraph graph in ProjectGraphs)
        {
            graph.RunStatus = graph.IsDisabled ? FlowGraphRunStatus.Skipped : FlowGraphRunStatus.NotRun;
            graph.LastElapsed = null;
        }

        var runGraphs = GetProjectRunOrder().ToList();
        AddExecutionLog("Info", $"项目运行开始：项目={ProjectName}，流程={runGraphs.Count}/{ProjectGraphs.Count}");
        int completedCount = 0;
        int skippedCount = 0;

        foreach (FlowGraph graph in runGraphs)
        {
            if (graph.IsDisabled)
            {
                graph.RunStatus = FlowGraphRunStatus.Skipped;
                AddExecutionLog("Warn", $"跳过禁用流程：{graph.Name}");
                skippedCount++;
                continue;
            }

            ActiveGraph = graph;
            FlowExecutionResult? result = await ExecuteFlowAsync(false, null, "项目").ConfigureAwait(true);
            if (result == null)
            {
                break;
            }

            if (result.Status != FlowExecutionStatus.Completed)
            {
                RunStatusText = $"项目运行停止：{graph.Name} {result.Status}";
                AddExecutionLog(result.Status == FlowExecutionStatus.Failed ? "Error" : "Warn", RunStatusText);
                return;
            }

            completedCount++;
        }

        RunStatusText = $"项目运行完成：OK {completedCount}，跳过 {skippedCount}，共 {ProjectGraphs.Count} 个流程";
        AddExecutionLog("Info", RunStatusText);
    }

    private IEnumerable<FlowGraph> GetProjectRunOrder()
    {
        var graphs = ProjectGraphs.ToList();
        FlowGraph? entry = graphs.FirstOrDefault(graph => graph.IsProjectEntry)
            ?? graphs.FirstOrDefault(graph => graph.Id == project.EntryGraphId)
            ?? graphs.FirstOrDefault();

        if (entry == null)
        {
            return graphs;
        }

        int entryIndex = graphs.IndexOf(entry);
        return graphs.Skip(entryIndex).Concat(graphs.Take(entryIndex));
    }

    private async Task<FlowExecutionResult?> ExecuteFlowAsync(
        bool isDebug,
        Guid? stopAfterNodeId = null,
        string scopeName = "流程",
        IReadOnlyDictionary<string, object?>? contextItems = null)
    {
        if (IsFlowRunning)
        {
            RunStatusText = "流程正在运行中";
            AddExecutionLog("Warn", RunStatusText);
            return null;
        }

        FlowExecutionResult? result = null;
        try
        {
            IsFlowRunning = true;
            if (activeGraph != null)
            {
                activeGraph.RunStatus = FlowGraphRunStatus.Running;
                activeGraph.LastElapsed = null;
            }

            RunStatusText = isDebug ? $"{scopeName}调试运行中..." : $"{scopeName}运行中...";
            AddExecutionLog("Info", $"{RunStatusText} 流程={GraphName}，节点={Nodes.Count}，连线={Connections.Count}");

            // 在执行前，重置 VM 层级显示状态
            foreach (var n in Nodes) n.Status = NodeStatus.Idle;
            foreach (var c in Connections) c.LastValue = null;
            LatestNodeResults.Clear();
            RuntimeVariables.Clear();
            RuntimeImages.Clear();
            SelectedNodeImages.Clear();

            if (executionService == null)
            {
                RunStatusText = "执行服务未初始化";
                AddExecutionLog("Error", RunStatusText);
                return null;
            }

            if (activeGraph == null)
            {
                RunStatusText = "没有可运行的流程";
                AddExecutionLog("Warn", RunStatusText);
                return null;
            }

            if (Nodes.Count == 0)
            {
                RunStatusText = "当前流程没有节点";
                AddExecutionLog("Warn", RunStatusText);
                return null;
            }

            if (!await ValidateBeforeRunAsync(scopeName).ConfigureAwait(true))
            {
                return null;
            }

            result = await executionService.ExecuteFlowAsync(
                Nodes,
                Connections,
                isDebug,
                activeGraph,
                HandleRuntimeEvent,
                stopAfterNodeId,
                contextItems,
                DestroyToken);
            LatestNodeResults.Clear();
            foreach (var record in result.NodeRecords)
            {
                LatestNodeResults.Add(record);
            }
            foreach (var variable in result.Variables)
            {
                RuntimeVariables.Add(variable);
            }
            foreach (var image in result.Images)
            {
                RuntimeImages.Add(await VisionImagePanelItemViewModel.CreateAsync(image, DestroyToken));
            }
            RefreshSelectedNodeImages(SelectedNode as FlowNodeViewModel);
            RunStatusText = result.Status switch
            {
                FlowExecutionStatus.Completed => $"{scopeName}运行完成：{GraphName}，{result.ExecutedCount} 个节点，{result.Elapsed.TotalMilliseconds:F0} ms",
                FlowExecutionStatus.Cancelled => $"{scopeName}运行已停止：{GraphName}，已执行 {result.ExecutedCount} 个节点",
                FlowExecutionStatus.Failed => $"{scopeName}运行失败：{GraphName}，{result.ErrorMessage}",
                _ => $"{scopeName}运行结束"
            };

            if (activeGraph != null)
            {
                activeGraph.LastElapsed = result.Elapsed;
                activeGraph.RunStatus = result.Status switch
                {
                    FlowExecutionStatus.Completed => FlowGraphRunStatus.Ok,
                    FlowExecutionStatus.Cancelled => FlowGraphRunStatus.Skipped,
                    FlowExecutionStatus.Failed => FlowGraphRunStatus.Ng,
                    _ => FlowGraphRunStatus.NotRun
                };
            }

            if (result.Status == FlowExecutionStatus.Failed)
            {
                FocusExecutionErrorNode(result);
                AddExecutionLog("Error", RunStatusText);
                await dialogMessageService.ShowErrorAsync($"流程执行失败: {result.ErrorMessage}", "运行错误");
            }
            else
            {
                AddExecutionLog(result.Status == FlowExecutionStatus.Completed ? "Info" : "Warn", RunStatusText);
            }
        }
        catch (Exception ex)
        {
            RunStatusText = $"{scopeName}运行异常：{ex.Message}";
            AddExecutionLog("Error", RunStatusText);
            await dialogMessageService.ShowErrorAsync($"流程执行发生异常: {ex.Message}", "运行错误");
            if (activeGraph != null)
            {
                activeGraph.RunStatus = FlowGraphRunStatus.Ng;
                activeGraph.LastElapsed = null;
            }
            result = FlowExecutionResult.Failed(SelectedNode as FlowNodeViewModel, ex.Message, 0, TimeSpan.Zero);
        }
        finally
        {
            IsFlowRunning = false;
        }

        return result;
    }

    private void FocusExecutionErrorNode(FlowExecutionResult result)
    {
        FlowNodeViewModel? errorNode = result.ErrorNode;
        FlowNodeRunRecord? failedRecord = result.NodeRecords.LastOrDefault(record => !record.Success);
        if (errorNode == null && !string.IsNullOrWhiteSpace(failedRecord?.NodeId))
        {
            errorNode = Nodes.FirstOrDefault(node => string.Equals(node.NodeId.ToString("D"), failedRecord.NodeId, StringComparison.OrdinalIgnoreCase));
        }

        if (errorNode == null)
        {
            return;
        }

        SelectedNode = errorNode;
        errorNode.Status = NodeStatus.Failed;
        RefreshSelectedNodeImages(errorNode);
        AddExecutionLog("Error", $"定位失败节点：{errorNode.DisplayName}");
    }

    private void LocateNodeRunRecord(FlowNodeRunRecord? record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.NodeId))
        {
            return;
        }

        FlowNodeViewModel? node = Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId.ToString("D"), record.NodeId, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
            return;
        }

        SelectedNode = node;
        RefreshSelectedNodeImages(node);
    }

    private async Task<bool> ValidateBeforeRunAsync(string scopeName)
    {
        foreach (FlowNodeViewModel node in Nodes.Where(node => !node.IsDisabled))
        {
            NodeParameterViewModel? missing = node.Parameters.FirstOrDefault(parameter =>
                parameter.HasRequiredError);
            if (missing == null)
            {
                if (TryValidateParameterRelations(node, out string relationMessage, out NodeParameterViewModel? invalidParameter))
                {
                    continue;
                }

                SelectedNode = node;
                RunStatusText = $"{scopeName}运行前检查失败：{node.DisplayName} {relationMessage}";
                AddExecutionLog("Warn", RunStatusText);
                await dialogMessageService.ShowWarningAsync(
                    $"节点「{node.DisplayName}」参数配置不合法：{relationMessage}",
                    "运行前检查");
                return false;
            }

            SelectedNode = node;
            RunStatusText = $"{scopeName}运行前检查失败：{node.DisplayName} 缺少 {missing.DisplayName}";
            AddExecutionLog("Warn", RunStatusText);
            await dialogMessageService.ShowWarningAsync(
                $"节点「{node.DisplayName}」的必填参数「{missing.DisplayName}」不能为空。",
                "运行前检查");
            return false;
        }

        return true;
    }

    private static bool TryValidateParameterRelations(
        FlowNodeViewModel node,
        out string message,
        out NodeParameterViewModel? invalidParameter)
    {
        message = string.Empty;
        invalidParameter = null;

        if (node.NodeType == FlowNodeTypes.VisionThreshold)
        {
            return ValidateLessThanOrEqual(
                node,
                FlowParameterKeys.ThresholdLower,
                FlowParameterKeys.ThresholdUpper,
                "下限不能大于上限。",
                out message,
                out invalidParameter);
        }

        if (node.NodeType == FlowNodeTypes.VisionBlob)
        {
            return ValidateLessThanOrEqual(
                node,
                FlowParameterKeys.MinArea,
                FlowParameterKeys.MaxArea,
                "最小面积不能大于最大面积。",
                out message,
                out invalidParameter);
        }

        if (node.NodeType == FlowNodeTypes.LogicRangeJudgement)
        {
            return ValidateLessThanOrEqual(
                node,
                FlowParameterKeys.Minimum,
                FlowParameterKeys.Maximum,
                "最小值不能大于最大值。",
                out message,
                out invalidParameter);
        }

        return true;
    }

    private static bool ValidateLessThanOrEqual(
        FlowNodeViewModel node,
        string lowerKey,
        string upperKey,
        string errorMessage,
        out string message,
        out NodeParameterViewModel? invalidParameter)
    {
        message = string.Empty;
        invalidParameter = null;

        NodeParameterViewModel? lower = FindParameter(node, lowerKey);
        NodeParameterViewModel? upper = FindParameter(node, upperKey);
        if (lower == null || upper == null)
        {
            return true;
        }

        if (!TryReadDouble(lower.Value, out double lowerValue)
            || !TryReadDouble(upper.Value, out double upperValue)
            || lowerValue <= upperValue)
        {
            return true;
        }

        message = errorMessage;
        invalidParameter = lower;
        return false;
    }

    private static NodeParameterViewModel? FindParameter(FlowNodeViewModel node, string key)
        => node.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase));

    private static bool TryReadDouble(object? value, out double number)
    {
        number = 0;
        if (value == null)
        {
            return false;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out number);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
                if (value == null)
                {
                    return false;
                }
            }
        }

        try
        {
            number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return double.TryParse(
                value.ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture,
                out number);
        }
    }

    private void AddExecutionLog(string level, string message)
    {
        ExecutionLogs.Add(new FlowRunLogEntry
        {
            Level = level,
            Message = message
        });

        const int maxLogCount = 500;
        while (ExecutionLogs.Count > maxLogCount)
        {
            ExecutionLogs.RemoveAt(0);
        }
    }

    private void RefreshSelectedNodeImages(FlowNodeViewModel? node)
    {
        SelectedNodeImages.Clear();
        IEnumerable<VisionImagePanelItemViewModel> source = RuntimeImages;
        if (node != null)
        {
            string nodeId = node.NodeId.ToString("D");
            source = source.Where(item => item.NodeId == nodeId);
        }

        foreach (var image in source)
        {
            SelectedNodeImages.Add(image);
        }

        SelectedImagePanelItem = SelectedNodeImages.FirstOrDefault();
    }

    private void LoadCurrentRoiFromNode(FlowNodeViewModel? node)
    {
        isLoadingRoiFromNode = true;
        try
        {
            CurrentRoi = TryReadRoi(node, out Rect roi) ? roi : null;
        }
        finally
        {
            isLoadingRoiFromNode = false;
        }

        SyncRoiListFromCurrentRoi();
    }

    private async Task ExecutePollingFlowAsync()
    {
        if (IsFlowRunning)
        {
            RunStatusText = "流程正在运行中";
            AddExecutionLog("Warn", RunStatusText);
            return;
        }

        IsPollingRun = true;
        int runCount = 0;
        AddExecutionLog("Info", $"轮询运行开始：图={GraphName}");

        try
        {
            while (IsPollingRun && !DestroyToken.IsCancellationRequested)
            {
                runCount++;
                await ExecuteFlowAsync(false).ConfigureAwait(true);

                if (!IsPollingRun || DestroyToken.IsCancellationRequested)
                {
                    break;
                }

                RunStatusText = $"轮询运行中：第 {runCount} 次";
                await Task.Delay(50, DestroyToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Navigation or shutdown cancelled the polling loop.
        }
        finally
        {
            IsPollingRun = false;
            RunStatusText = $"轮询运行已停止：共 {runCount} 次";
            AddExecutionLog("Warn", RunStatusText);
        }
    }

    private async Task ExecuteBatchRunAsync()
    {
        if (IsFlowRunning)
        {
            RunStatusText = "流程正在运行中";
            AddExecutionLog("Warn", RunStatusText);
            return;
        }

        if (activeGraph == null)
        {
            RunStatusText = "没有可批处理运行的流程";
            AddExecutionLog("Warn", RunStatusText);
            return;
        }

        FlowNode? imageInputNode = activeGraph.Nodes.FirstOrDefault(node => node.NodeType == FlowNodeTypes.VisionLocalImage);
        if (imageInputNode == null
            || !imageInputNode.Parameters.TryGetValue(FlowParameterKeys.ImagePath, out object? pathValue)
            || string.IsNullOrWhiteSpace(pathValue?.ToString()))
        {
            await dialogMessageService.ShowInfoAsync("当前流程需要先添加本地图像节点，并选择单图、多图或文件夹。", "批处理");
            return;
        }

        IVisionFrameSource frameSource = frameSourceFactory.CreateLocalImageSource(pathValue.ToString());
        var frames = new List<VisionFrame>();
        await foreach (VisionFrame frame in frameSource.ReadAllFramesAsync(DestroyToken).ConfigureAwait(true))
        {
            frames.Add(frame);
        }

        if (frames.Count == 0)
        {
            await dialogMessageService.ShowInfoAsync("未找到可批处理的图片。", "批处理");
            return;
        }

        BatchRunRecords.Clear();
        AddExecutionLog("Info", $"批处理开始：流程={GraphName}，图像={frames.Count}");

        int okCount = 0;
        int ngCount = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            VisionFrame frame = frames[i];
            var contextItems = new Dictionary<string, object?>
            {
                [FlowExecutionContext.BatchCurrentImageKey] = frame.Image,
                [FlowExecutionContext.BatchCurrentSourceNameKey] = frame.SourceName
            };

            FlowExecutionResult? result = await ExecuteFlowAsync(false, null, "批处理", contextItems).ConfigureAwait(true);
            bool ok = result?.Status == FlowExecutionStatus.Completed;
            if (ok)
            {
                okCount++;
            }
            else
            {
                ngCount++;
            }

            BatchRunRecords.Add(new VisionBatchRunRecord
            {
                Index = i,
                Count = frames.Count,
                SourceName = frame.SourceName,
                Image = result?.Images.LastOrDefault(item => item.Overlays.Count > 0)?.Image ?? frame.Image,
                Overlays = result?.Images.LastOrDefault(item => item.Overlays.Count > 0)?.Overlays ?? Array.Empty<IVisionOverlayShape>(),
                GraphName = GraphName,
                Status = ok ? "OK" : "NG",
                ElapsedMs = result?.Elapsed.TotalMilliseconds ?? 0,
                Message = result?.ErrorMessage ?? string.Empty,
                ResultSummary = BuildResultSummary(result)
            });
        }

        RunStatusText = $"批处理完成：OK {okCount}，NG {ngCount}，共 {frames.Count} 张";
        AddExecutionLog(ngCount == 0 ? "Info" : "Warn", RunStatusText);
    }

    private static string BuildResultSummary(FlowExecutionResult? result)
    {
        if (result == null)
        {
            return string.Empty;
        }

        FlowPortValueSnapshot? detection = result.Variables.LastOrDefault(item => item.Value is ObjectDetectionResult);
        if (detection?.Value is ObjectDetectionResult detectionResult)
        {
            return FlowValueDisplayFormatter.FormatDetections(detectionResult);
        }

        FlowPortValueSnapshot? lastValue = result.Variables.LastOrDefault(item => item.HasValue && item.Value != null);
        return lastValue?.DisplayValue ?? string.Empty;
    }

    private void ShowBatchRunRecordImage(VisionBatchRunRecord? record)
    {
        if (record?.Image == null)
        {
            return;
        }

        var item = new VisionImagePanelItemViewModel
        {
            NodeName = "批处理图像",
            PortName = record.SourceName,
            Direction = "Input",
            Summary = record.SourceName,
            SequenceIndex = record.Index,
            SequenceCount = record.Count,
            Image = record.Image,
            Width = record.Image.Width,
            Height = record.Image.Height,
            Stride = record.Image.Stride,
            PixelFormat = record.Image.PixelFormat,
            OverlayCount = record.Overlays.Count,
            Overlays = record.Overlays
        };

        SelectedNodeImages.Clear();
        SelectedNodeImages.Add(item);
        SelectedImagePanelItem = item;
    }

    private async Task ExportRunReportAsync()
    {
        if (BatchRunRecords.Count == 0 && LatestNodeResults.Count == 0)
        {
            await dialogMessageService.ShowInfoAsync("当前没有可导出的运行结果。", "导出报告");
            return;
        }

        string? selectedPath = fileDialogService.SaveFile(new SaveFileDialogOptions
        {
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            Title = "导出运行报告",
            FileName = $"{ProjectName}_{DateTime.Now:yyyyMMdd_HHmmss}_Report.csv",
            DefaultExtension = ".csv"
        });
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        await File.WriteAllTextAsync(selectedPath, BuildRunReportCsv(), DestroyToken);
        AddExecutionLog("Info", $"运行报告已导出：{selectedPath}");
    }

    private async Task CreateYoloSampleFlowAsync()
    {
        if (ActiveTab == null || activeGraph == null)
        {
            return;
        }

        if ((Nodes.Count > 0 || Connections.Count > 0)
            && !await dialogMessageService.ShowConfirmAsync("创建示例流程会清空当前流程画布，是否继续？", "创建示例流程"))
        {
            return;
        }

        Connections.Clear();
        Nodes.Clear();
        activeGraph.Nodes.Clear();
        activeGraph.Connections.Clear();

        FlowNodeViewModel? imageNode = CreateNodeAt(FlowNodeTypes.VisionLocalImage, new Point(120, 160));
        FlowNodeViewModel? yoloNode = CreateNodeAt(FlowNodeTypes.VisionYoloObjectDetection, new Point(420, 160));
        if (imageNode == null || yoloNode == null)
        {
            await dialogMessageService.ShowErrorAsync("示例流程创建失败，未找到本地图像或 YOLO 节点描述。", "创建示例流程");
            return;
        }

        PortViewModel? imageOutput = imageNode.OutputPorts.FirstOrDefault(port => port.DataType == PortDataTypes.Image)
            ?? imageNode.OutputPorts.FirstOrDefault();
        PortViewModel? yoloInput = yoloNode.InputPorts.FirstOrDefault(port => port.DataType == PortDataTypes.Image)
            ?? yoloNode.InputPorts.FirstOrDefault();
        if (imageOutput != null && yoloInput != null)
        {
            TryConnect(imageOutput, yoloInput);
        }

        SelectedNode = imageNode;
        IsDirty = true;
        FitToScreenTriggerCount++;
        RunStatusText = "已创建示例流程：本地图像 → YOLO 目标检测";
        AddExecutionLog("Info", RunStatusText);
    }

    private string BuildRunReportCsv()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Section,Index,Count,Graph,Source,Status,ElapsedMs,ResultSummary,Message");

        foreach (VisionBatchRunRecord record in BatchRunRecords)
        {
            builder.AppendLine(string.Join(",",
                Csv("Batch"),
                record.Index + 1,
                record.Count,
                Csv(record.GraphName),
                Csv(record.SourceName),
                Csv(record.Status),
                record.ElapsedMs.ToString("F0"),
                Csv(record.ResultSummary),
                Csv(record.Message)));
        }

        foreach (FlowNodeRunRecord record in LatestNodeResults)
        {
            builder.AppendLine(string.Join(",",
                Csv("Node"),
                string.Empty,
                string.Empty,
                Csv(GraphName),
                Csv(record.NodeName),
                Csv(record.StatusText),
                record.Elapsed.TotalMilliseconds.ToString("F0"),
                string.Empty,
                Csv(record.Message ?? string.Empty)));
        }

        return builder.ToString();
    }

    private static string Csv(string value)
        => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private void AddRoiRegion()
    {
        Rect bounds = CreateDefaultRoiBounds();
        var item = new RoiRegionViewModel
        {
            Name = $"ROI {RoiRegions.Count + 1}",
            Bounds = bounds,
            IsEnabled = true
        };

        RoiRegions.Add(item);
        SelectedRoiRegion = item;
        CurrentRoi = bounds;
        IsDirty = true;
    }

    private void DuplicateRoiRegion(RoiRegionViewModel? source)
    {
        source ??= SelectedRoiRegion;
        if (source == null)
        {
            AddRoiRegion();
            return;
        }

        Rect bounds = source.Bounds;
        bounds.Offset(12, 12);
        var item = new RoiRegionViewModel
        {
            Name = $"{source.Name} Copy",
            Bounds = bounds,
            IsEnabled = source.IsEnabled
        };

        RoiRegions.Add(item);
        SelectedRoiRegion = item;
        CurrentRoi = item.IsEnabled ? bounds : null;
        IsDirty = true;
    }

    private void ToggleRoiRegion(RoiRegionViewModel? item)
    {
        item ??= SelectedRoiRegion;
        if (item == null)
        {
            return;
        }

        item.IsEnabled = !item.IsEnabled;
        if (SelectedRoiRegion == item)
        {
            CurrentRoi = item.IsEnabled ? item.Bounds : null;
        }

        IsDirty = true;
    }

    private void DeleteRoiRegion(RoiRegionViewModel? item)
    {
        item ??= SelectedRoiRegion;
        if (item == null)
        {
            return;
        }

        int index = RoiRegions.IndexOf(item);
        RoiRegions.Remove(item);

        RoiRegionViewModel? next = RoiRegions.Count == 0
            ? null
            : RoiRegions[Math.Clamp(index, 0, RoiRegions.Count - 1)];
        SelectedRoiRegion = next;
        CurrentRoi = next?.IsEnabled == true ? next.Bounds : null;
        IsDirty = true;
    }

    private Rect CreateDefaultRoiBounds()
    {
        double width = SelectedImagePanelItem?.Width > 0 ? SelectedImagePanelItem.Width : 640;
        double height = SelectedImagePanelItem?.Height > 0 ? SelectedImagePanelItem.Height : 480;
        double roiWidth = Math.Max(40, Math.Round(width * 0.25));
        double roiHeight = Math.Max(40, Math.Round(height * 0.25));
        double x = Math.Max(0, Math.Round((width - roiWidth) * 0.5));
        double y = Math.Max(0, Math.Round((height - roiHeight) * 0.5));

        return new Rect(x, y, roiWidth, roiHeight);
    }

    private void SyncCurrentRoiToSelectedNode()
    {
        if (SelectedNode is not FlowNodeViewModel node)
        {
            return;
        }

        if (CurrentRoi is not Rect roi)
        {
            bool changed = false;
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiX, 0.0);
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiY, 0.0);
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiWidth, 0.0);
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiHeight, 0.0);
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiAngle, 0.0);
            changed |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiRadius, 0.0);
            if (changed)
            {
                IsDirty = true;
            }

            return;
        }

        VisionRoiKind kind = TryGetRoiKind(node);
        bool usesCenter = kind is VisionRoiKind.Circle or VisionRoiKind.RotatedRectangle;
        double x = usesCenter ? roi.X + roi.Width / 2.0 : roi.X;
        double y = usesCenter ? roi.Y + roi.Height / 2.0 : roi.Y;

        bool updated = false;
        updated |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiX, Math.Round(x, 3));
        updated |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiY, Math.Round(y, 3));
        updated |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiWidth, Math.Round(roi.Width, 3));
        updated |= node.TrySetParameterValue(VisionRoiParameterDefinitions.RoiHeight, Math.Round(roi.Height, 3));
        if (kind == VisionRoiKind.Circle)
        {
            updated |= node.TrySetParameterValue(
                VisionRoiParameterDefinitions.RoiRadius,
                Math.Round(Math.Min(roi.Width, roi.Height) / 2.0, 3));
        }
        if (updated)
        {
            IsDirty = true;
        }
    }

    private void SyncRoiListFromCurrentRoi()
    {
        isSyncingRoiRegionSelection = true;
        try
        {
            if (CurrentRoi is not Rect roi || roi.Width <= 0 || roi.Height <= 0)
            {
                if (SelectedRoiRegion != null)
                {
                    RoiRegions.Remove(SelectedRoiRegion);
                    SelectedRoiRegion = RoiRegions.FirstOrDefault();
                }

                return;
            }

            RoiRegionViewModel item;
            if (SelectedRoiRegion != null && RoiRegions.Contains(SelectedRoiRegion))
            {
                item = SelectedRoiRegion;
            }
            else if (RoiRegions.Count == 0)
            {
                item = new RoiRegionViewModel();
                RoiRegions.Add(item);
            }
            else
            {
                item = RoiRegions[0];
            }

            item.Bounds = roi;
            item.IsEnabled = true;
            SelectedRoiRegion = item;
        }
        finally
        {
            isSyncingRoiRegionSelection = false;
        }
    }

    private static bool TryReadRoi(FlowNodeViewModel? node, out Rect roi)
    {
        roi = default;
        if (node == null
            || !TryGetDouble(node, VisionRoiParameterDefinitions.RoiX, out double x)
            || !TryGetDouble(node, VisionRoiParameterDefinitions.RoiY, out double y))
        {
            return false;
        }

        VisionRoiKind kind = TryGetRoiKind(node);
        double width = 0;
        double height = 0;
        if (kind == VisionRoiKind.Circle
            && TryGetDouble(node, VisionRoiParameterDefinitions.RoiRadius, out double radius)
            && radius > 0)
        {
            width = radius * 2.0;
            height = radius * 2.0;
        }
        else if (!TryGetDouble(node, VisionRoiParameterDefinitions.RoiWidth, out width)
            || !TryGetDouble(node, VisionRoiParameterDefinitions.RoiHeight, out height)
            || width <= 0
            || height <= 0)
        {
            return false;
        }

        roi = kind is VisionRoiKind.Circle or VisionRoiKind.RotatedRectangle
            ? new Rect(x - width / 2.0, y - height / 2.0, width, height)
            : new Rect(x, y, width, height);
        return true;
    }

    private static VisionRoiKind TryGetRoiKind(FlowNodeViewModel node)
    {
        if (!node.TryGetParameterValue(VisionRoiParameterDefinitions.RoiKind, out object? raw) || raw == null)
        {
            return VisionRoiKind.Rectangle;
        }

        if (raw is JsonElement element)
        {
            raw = element.ValueKind == JsonValueKind.String ? element.GetString() : raw.ToString();
        }

        string? text = raw?.ToString();
        return Enum.TryParse(text, ignoreCase: true, out VisionRoiKind kind)
            ? kind
            : VisionRoiKind.Rectangle;
    }

    private static bool TryGetDouble(FlowNodeViewModel node, string key, out double value)
    {
        value = 0;
        if (!node.TryGetParameterValue(key, out object? raw) || raw == null)
        {
            return false;
        }

        try
        {
            if (raw is JsonElement element)
            {
                raw = element.ValueKind switch
                {
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.String => element.GetString(),
                    _ => null
                };
            }

            if (raw == null)
            {
                return false;
            }

            value = Convert.ToDouble(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void HandleRuntimeEvent(FlowRuntimeEvent runtimeEvent)
    {
        string level = runtimeEvent.Kind switch
        {
            FlowRuntimeEventKind.FlowFailed or FlowRuntimeEventKind.NodeFailed => "Error",
            FlowRuntimeEventKind.FlowCancelled or FlowRuntimeEventKind.DebugPaused => "Warn",
            _ => "Info"
        };

        string message = runtimeEvent.Message;
        if (runtimeEvent.Node != null && !message.Contains(runtimeEvent.Node.DisplayName, StringComparison.Ordinal))
        {
            message = $"{runtimeEvent.Node.DisplayName}：{message}";
        }

        AddExecutionLog(level, message);
    }

    private DelegateCommand<FlowNodeViewModel>? showNodeHelpCommand;

    public DelegateCommand<FlowNodeViewModel> ShowNodeHelpCommand
        => showNodeHelpCommand ??= new DelegateCommand<FlowNodeViewModel>(async node =>
        {
            if (node == null) return;
            await dialogMessageService.ShowInfoAsync($"显示关于 '{node.NodeType}' 的帮助文档和详细描述。\n\n功能:\n{node.NodeDescription}", "节点帮助");
        });

    // ── AddNodeFromPaletteCommand（节点调色板双击/Enter 添加）
    private DelegateCommand<NodePaletteItemViewModel>? addNodeFromPaletteCommand;

    public DelegateCommand<NodePaletteItemViewModel> AddNodeFromPaletteCommand
        => addNodeFromPaletteCommand ??= new DelegateCommand<NodePaletteItemViewModel>(item =>
        {
            if (item == null) return;
            AddNodeAt(item.NodeType, new Point(100 + Nodes.Count * 20, 80 + Nodes.Count * 20));
        });

    // ── 删除选中项（与 Delete 键绑定）命令
    private DelegateCommand? deleteSelectionCommand;

    public DelegateCommand DeleteSelectionCommand
        => deleteSelectionCommand ??= new DelegateCommand(DeleteSelection);

    private void DeleteSelection()
    {
        var selectedConns = Connections.Where(c => c.IsSelected).ToList();
        foreach (var c in selectedConns) RemoveConnection(c);

        var selectedNodes = Nodes.Where(n => n.IsSelected).ToList();
        foreach (var n in selectedNodes) DeleteNode(n);

        ClearSelection();
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────
    // 内部业务逻辑
    // ────────────────────────────────────────────────────────────────────────────────────────────

    private void AddNodeAt(string nodeType, Point pos)
        => CreateNodeAt(nodeType, pos);

    private FlowNodeViewModel? CreateNodeAt(string nodeType, Point pos)
    {
        if (!descriptorMap.TryGetValue(nodeType, out var desc)) return null;

        var model = desc.CreateDefaultNode();
        model.X = pos.X;
        model.Y = pos.Y;
        activeGraph?.Nodes.Add(model);

        var viewmodel = new FlowNodeViewModel(model, colorService, desc.Parameters, fileDialogService);
        viewmodel.NodeDescription = desc.Description;
        Nodes.Add(viewmodel);
        IsDirty = true;

        Application.Current.Dispatcher.InvokeAsync(
            () => SelectedNode = viewmodel,
            System.Windows.Threading.DispatcherPriority.Background);

        return viewmodel;
    }

    /// <summary>
    /// 尝试连接两个端口（LabVIEW 风格：支持分支连接）
    /// </summary>
    /// <returns>连接结果，包含是否成功和错误信息</returns>
    private (bool Success, string? ErrorMessage) TryConnect(PortViewModel src, PortViewModel tgt)
    {
        // ── 1. 方向校验（允许 Input、Output 顺序颠倒）──
        if (src.Direction == PortDirection.Input && tgt.Direction == PortDirection.Output)
            (src, tgt) = (tgt, src);

        if (src.Direction != PortDirection.Output || tgt.Direction != PortDirection.Input)
            return (false, "只能从输出端口连接到输入端口");

        // ── 2. 防止重复连线 ──
        if (Connections.Any(c => c.Source.PortId == src.PortId && c.Target.PortId == tgt.PortId))
            return (false, "该连线已存在");

        // ── 3. 检查输入端口是否允许多路连接（LabVIEW 风格：一个输出可以连接多个输入）──
        if (!tgt.AllowMultiple)
        {
            // 检查该输入端口是否已有连接
            var existingConnections = Connections.Count(c => c.Target.PortId == tgt.PortId);
            if (existingConnections > 0)
                return (false, $"端口 \"{tgt.Name}\" 不允许多路连接");
        }

        // ── 4. 类型检查（LabVIEW 风格：严格类型匹配）──
        var typeCheckResult = ValidateConnectionTypes(src, tgt);
        if (!typeCheckResult.IsValid)
            return (false, typeCheckResult.ErrorMessage);

        // ── 5. 创建连接 ──
        var connModel = new FlowConnection { SourcePortId = src.PortId, TargetPortId = tgt.PortId };
        activeGraph?.Connections.Add(connModel);

        var connVm = new FlowConnectionViewModel(connModel.Id, src, tgt, colorService);
        connVm.IsValid = typeCheckResult.IsValid;
        Connections.Add(connVm);

        // ── 6. 更新连接状态和计数 ──
        UpdateConnectionStates();
        IsDirty = true;
        return (true, null);
    }

    /// <summary>
    /// 验证连接类型匹配（LabVIEW 风格的类型检查）
    /// </summary>
    private (bool IsValid, string? ErrorMessage) ValidateConnectionTypes(PortViewModel src, PortViewModel tgt)
    {
        // 1. 检查端口大类 (Data vs Execution)
        if (src.Type != tgt.Type)
        {
            return (false, $"类型大类不匹配：无法将 {src.Type} 端口连接到 {tgt.Type} 端口");
        }

        // 2. 对于 Execution 类型，无需进一步检查 DataType
        if (src.Type == PortType.Execution)
        {
            return (true, null);
        }

        // 3. 对于 Data 类型，由 DataType 决定
        // Any 类型可以连接 any 类型
        if (src.DataType == PortDataTypes.Any || tgt.DataType == PortDataTypes.Any)
            return (true, null);

        // 类型必须完全匹配
        if (src.DataType != tgt.DataType)
        {
            return (false, $"数据类型不匹配：无法将 {src.DataType} 类型连接到 {tgt.DataType} 类型");
        }

        return (true, null);
    }

    /// <summary>
    /// 更新端口的连接状态和计数 (优化版：支持批处理)
    /// </summary>
    private void UpdateConnectionStates(IEnumerable<FlowNodeViewModel> nodes, IEnumerable<FlowConnectionViewModel> connections)
    {
        // 1. 预先重置
        foreach (var node in nodes)
        {
            foreach (var port in node.InputPorts.Concat(node.OutputPorts).Concat(node.GetVisiblePorts()))
            {
                port.IsConnected = false;
                port.ConnectionCount = 0;
            }
        }

        // 2. 批量计算
        foreach (var conn in connections)
        {
            conn.Source.IsConnected = true;
            conn.Target.IsConnected = true;
            conn.Source.ConnectionCount++;
            conn.Target.ConnectionCount++;
        }
    }

    private void UpdateConnectionStates() => UpdateConnectionStates(Nodes, Connections);

    private void RemoveConnection(FlowConnectionViewModel conn)
    {
        if (activeGraph == null) return;
        Connections.Remove(conn);
        var model = activeGraph.Connections.FirstOrDefault(c => c.Id == conn.ConnectionId);
        if (model != null) activeGraph.Connections.Remove(model);

        // 更新连接状态和计数
        UpdateConnectionStates();
        IsDirty = true;
    }

    private void DeleteNode(FlowNodeViewModel nodeVm)
    {
        if (activeGraph == null) return;
        var toRemove = Connections
            .Where(c => nodeVm.InputPorts.Any(p => p.PortId == c.Target.PortId)
                     || nodeVm.OutputPorts.Any(p => p.PortId == c.Source.PortId))
            .ToList();
        foreach (var c in toRemove) RemoveConnection(c);

        Nodes.Remove(nodeVm);
        var model = activeGraph.Nodes.FirstOrDefault(n => n.Id == nodeVm.NodeId);
        if (model != null) activeGraph.Nodes.Remove(model);
        if (SelectedNode == nodeVm) SetEditorSelectedItem(null);
        IsDirty = true;
    }

    private async Task NewGraphAsync()
    {
        if (IsDirty && !await ConfirmDiscardAsync()) return;
        NewGraphCore();
    }

    private void NewGraphCore()
    {
        OpenTabs.Clear(); // 清空旧的标签页
        project = new FlowProject();
        currentFilePath = null;

        var firstGraph = new FlowGraph { Name = "Main", IsProjectEntry = true };
        project.Graphs.Add(firstGraph);
        project.ActiveGraphId = firstGraph.Id;
        project.EntryGraphId = firstGraph.Id;

        ProjectGraphs.Clear();
        ProjectGraphs.Add(firstGraph);
        ActiveGraph = firstGraph;

        IsDirty = false;
        RaisePropertyChanged(nameof(ProjectName));
        RaisePropertyChanged(nameof(ProjectFileDisplay));
        UpdateTitle();
    }

    private DelegateCommand? addGraphCommand;

    public DelegateCommand AddGraphCommand => addGraphCommand ??= new DelegateCommand(() =>
    {
        var newG = new FlowGraph { Name = $"流程图_{ProjectGraphs.Count + 1}" };
        project.Graphs.Add(newG);
        ProjectGraphs.Add(newG);
        ActiveGraph = newG;
        IsDirty = true;
        RaisePropertyChanged(nameof(ProjectName));

        // 新建后立即开启重命名状态
        newG.IsRenaming = true;
        RenamingGraph = newG;
    });

    // ── 流程图管理命令 (VS2022 风格) ──

    private DelegateCommand<FlowGraph>? setEntryGraphCommand;

    public DelegateCommand<FlowGraph> SetEntryGraphCommand => setEntryGraphCommand ??= new DelegateCommand<FlowGraph>(graph =>
    {
        if (graph == null) return;
        SetProjectEntry(graph);
        IsDirty = true;
    });

    private DelegateCommand<FlowGraph>? toggleGraphDisableCommand;

    public DelegateCommand<FlowGraph> ToggleGraphDisableCommand => toggleGraphDisableCommand ??= new DelegateCommand<FlowGraph>(graph =>
    {
        if (graph == null) return;
        graph.IsDisabled = !graph.IsDisabled;
        graph.RunStatus = graph.IsDisabled ? FlowGraphRunStatus.Skipped : FlowGraphRunStatus.NotRun;
        IsDirty = true;
    });

    private DelegateCommand<FlowGraph>? duplicateGraphCommand;

    public DelegateCommand<FlowGraph> DuplicateGraphCommand => duplicateGraphCommand ??= new DelegateCommand<FlowGraph>(graph =>
    {
        if (graph == null) return;
        FlowGraph copy = CloneGraph(graph, GetCopyGraphName(graph.Name));
        int index = project.Graphs.IndexOf(graph);
        int insertIndex = index < 0 ? project.Graphs.Count : index + 1;
        project.Graphs.Insert(insertIndex, copy);
        ProjectGraphs.Insert(insertIndex, copy);
        ActiveGraph = copy;
        copy.IsRenaming = true;
        RenamingGraph = copy;
        IsDirty = true;
        RaisePropertyChanged(nameof(ProjectName));
    });

    private DelegateCommand<FlowGraph>? moveGraphUpCommand;

    public DelegateCommand<FlowGraph> MoveGraphUpCommand => moveGraphUpCommand ??= new DelegateCommand<FlowGraph>(graph =>
    {
        MoveGraph(graph, -1);
    });

    private DelegateCommand<FlowGraph>? moveGraphDownCommand;

    public DelegateCommand<FlowGraph> MoveGraphDownCommand => moveGraphDownCommand ??= new DelegateCommand<FlowGraph>(graph =>
    {
        MoveGraph(graph, 1);
    });

    private DelegateCommand<FlowGraph>? deleteGraphCommand;

    public DelegateCommand<FlowGraph> DeleteGraphCommand => deleteGraphCommand ??= new DelegateCommand<FlowGraph>(async g =>
    {
        if (g == null) return;
        if (ProjectGraphs.Count <= 1)
        {
            await dialogMessageService.ShowInfoAsync("项目中至少需要保留一个流程图。", "提示");
            return;
        }
        if (!await dialogMessageService.ShowConfirmAsync($"确定要从项目中删除流程图 \"{g.Name}\" 吗？", "确认删除")) return;

        // 关闭关联的标签页
        var tab = OpenTabs.FirstOrDefault(t => t.Graph == g);
        if (tab != null) OpenTabs.Remove(tab);

        ProjectGraphs.Remove(g);
        project.Graphs.Remove(g);

        if (project.EntryGraphId == g.Id)
        {
            SetProjectEntry(ProjectGraphs.FirstOrDefault());
        }

        if (ActiveGraph == g)
        {
            ActiveGraph = ProjectGraphs.FirstOrDefault();
        }
        IsDirty = true;
        RaisePropertyChanged(nameof(ProjectName));
    });

    private DelegateCommand<FlowGraph>? startRenameGraphCommand;

    public DelegateCommand<FlowGraph> StartRenameGraphCommand => startRenameGraphCommand ??= new DelegateCommand<FlowGraph>(g =>
    {
        if (g != null) g.IsRenaming = true;
        RenamingGraph = g;
    });

    private DelegateCommand<string>? endRenameGraphCommand;

    public DelegateCommand<string> EndRenameGraphCommand => endRenameGraphCommand ??= new DelegateCommand<string>(newName =>
    {
        if (RenamingGraph != null)
        {
            if (!string.IsNullOrWhiteSpace(newName) && RenamingGraph.Name != newName)
            {
                RenamingGraph.Name = newName;

                // 同步更新已打开的标签页标题
                var tab = OpenTabs.FirstOrDefault(t => t.Graph == RenamingGraph);
                if (tab != null) tab.Name = newName;

                IsDirty = true;
                UpdateTitle();
                if (ActiveGraph == RenamingGraph) RaisePropertyChanged(nameof(GraphName));
            }
            RenamingGraph.IsRenaming = false;
        }
        RenamingGraph = null;
    });

    private void SetProjectEntry(FlowGraph? graph)
    {
        if (graph == null)
        {
            project.EntryGraphId = Guid.Empty;
            foreach (FlowGraph item in ProjectGraphs)
            {
                item.IsProjectEntry = false;
            }
            return;
        }

        project.EntryGraphId = graph.Id;
        foreach (FlowGraph item in ProjectGraphs)
        {
            item.IsProjectEntry = item.Id == graph.Id;
        }
    }

    private void MoveGraph(FlowGraph? graph, int offset)
    {
        if (graph == null) return;
        int oldIndex = ProjectGraphs.IndexOf(graph);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= ProjectGraphs.Count)
        {
            return;
        }

        ProjectGraphs.Move(oldIndex, newIndex);
        project.Graphs.Remove(graph);
        project.Graphs.Insert(newIndex, graph);
        IsDirty = true;
    }

    public void MoveGraphToIndex(FlowGraph? graph, int targetIndex)
    {
        if (graph == null) return;
        int oldIndex = ProjectGraphs.IndexOf(graph);
        if (oldIndex < 0 || targetIndex < 0 || targetIndex >= ProjectGraphs.Count || oldIndex == targetIndex)
        {
            return;
        }

        ProjectGraphs.Move(oldIndex, targetIndex);
        project.Graphs.Remove(graph);
        project.Graphs.Insert(targetIndex, graph);
        IsDirty = true;
    }

    private string GetCopyGraphName(string sourceName)
    {
        string baseName = $"{sourceName}_Copy";
        string name = baseName;
        int index = 2;
        while (ProjectGraphs.Any(graph => string.Equals(graph.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName}{index++}";
        }

        return name;
    }

    private static FlowGraph CloneGraph(FlowGraph source, string name)
    {
        string json = JsonSerializer.Serialize(source);
        FlowGraph clone = JsonSerializer.Deserialize<FlowGraph>(json) ?? new FlowGraph();
        clone.Id = Guid.NewGuid();
        clone.Name = name;
        clone.IsProjectEntry = false;
        clone.IsDisabled = false;
        clone.IsRenaming = false;
        clone.RunStatus = FlowGraphRunStatus.NotRun;
        clone.LastElapsed = null;
        clone.CreatedAt = DateTime.Now;
        clone.UpdatedAt = DateTime.Now;

        var portMap = new Dictionary<Guid, Guid>();
        foreach (FlowNode node in clone.Nodes)
        {
            node.Id = Guid.NewGuid();
            foreach (FlowPort port in node.InputPorts.Concat(node.OutputPorts))
            {
                Guid oldId = port.Id;
                port.Id = Guid.NewGuid();
                portMap[oldId] = port.Id;
            }
        }

        foreach (FlowConnection connection in clone.Connections.ToList())
        {
            if (portMap.TryGetValue(connection.SourcePortId, out Guid sourcePortId)
                && portMap.TryGetValue(connection.TargetPortId, out Guid targetPortId))
            {
                connection.Id = Guid.NewGuid();
                connection.SourcePortId = sourcePortId;
                connection.TargetPortId = targetPortId;
            }
            else
            {
                clone.Connections.Remove(connection);
            }
        }

        return clone;
    }

    private async Task OpenGraphAsync()
    {
        if (IsDirty && !await ConfirmDiscardAsync()) return;
        string? filePath = fileDialogService.OpenFile(new OpenFileDialogOptions
        {
            Filter = "流程项目文件 (*.kproj;*.kflow)|*.kproj;*.kflow|所有文件 (*.*)|*.*",
            Title = "打开流程项目"
        });
        if (string.IsNullOrWhiteSpace(filePath)) return;

        await LoadProjectFromPathAsync(filePath);
    }

    private async Task OpenRecentProjectAsync(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (IsDirty && !await ConfirmDiscardAsync()) return;
        if (!File.Exists(filePath))
        {
            recentProjectService.Remove(filePath);
            RefreshRecentProjects();
            await dialogMessageService.ShowWarningAsync("最近项目文件不存在，已从列表中移除。", "打开最近项目");
            return;
        }

        await LoadProjectFromPathAsync(filePath);
    }

    private async Task LoadProjectFromPathAsync(string filePath)
    {
        var loadedProject = await persistence.LoadProjectAsync(filePath);
        if (loadedProject == null)
        {
            await dialogMessageService.ShowErrorAsync("文件加载失败或格式不正确", "错误");
            return;
        }

        this.project = loadedProject;
        this.currentFilePath = filePath;

        OpenTabs.Clear();   // 清空旧的标签页
        ProjectGraphs.Clear();
        foreach (var g in project.Graphs) ProjectGraphs.Add(g);
        SetProjectEntry(ProjectGraphs.FirstOrDefault(graph => graph.Id == project.EntryGraphId)
            ?? ProjectGraphs.FirstOrDefault(graph => graph.IsProjectEntry)
            ?? ProjectGraphs.FirstOrDefault());

        ActiveGraph = project.Graphs.FirstOrDefault(g => g.Id == project.ActiveGraphId) ?? project.Graphs.FirstOrDefault();

        IsDirty = false;
        RaisePropertyChanged(nameof(ProjectName));
        RaisePropertyChanged(nameof(ProjectFileDisplay));
        UpdateTitle();
        RunStatusText = $"项目已打开：{project.Name}";
        AddExecutionLog("Info", RunStatusText);
        recentProjectService.Add(filePath);
        RefreshRecentProjects();
    }

    private async Task SaveGraphAsync()
    {
        if (currentFilePath == null) { await SaveGraphAsAsync(); return; }
        SyncProjectEntry();
        SyncViewModelsToGraph();
        await persistence.SaveProjectAsync(project, currentFilePath);
        IsDirty = false;
        RaisePropertyChanged(nameof(ProjectFileDisplay));
        RunStatusText = $"项目已保存：{currentFilePath}";
        AddExecutionLog("Info", RunStatusText);
        recentProjectService.Add(currentFilePath);
        RefreshRecentProjects();
    }

    private async Task SaveGraphAsAsync()
    {
        string? selectedPath = fileDialogService.SaveFile(new SaveFileDialogOptions
        {
            Filter = "流程项目文件 (*.kproj)|*.kproj",
            Title = "保存流程项目",
            FileName = project.Name,
            DefaultExtension = ".kproj"
        });
        if (string.IsNullOrWhiteSpace(selectedPath)) return;
        currentFilePath = selectedPath;
        project.Name = Path.GetFileNameWithoutExtension(selectedPath);
        RaisePropertyChanged(nameof(ProjectName));
        RaisePropertyChanged(nameof(ProjectFileDisplay));
        SyncProjectEntry();
        SyncViewModelsToGraph();
        await persistence.SaveProjectAsync(project, currentFilePath);
        IsDirty = false;
        UpdateTitle();
        RunStatusText = $"项目已另存为：{currentFilePath}";
        AddExecutionLog("Info", RunStatusText);
        recentProjectService.Add(currentFilePath);
        RefreshRecentProjects();
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        RecentProjects.AddRange(recentProjectService.Load().Select(path => new RecentProjectItemViewModel(path)));
    }

    private void SyncProjectEntry()
    {
        FlowGraph? entry = ProjectGraphs.FirstOrDefault(graph => graph.IsProjectEntry)
            ?? ProjectGraphs.FirstOrDefault();
        if (entry != null)
        {
            SetProjectEntry(entry);
        }
    }

    private async Task ClearCanvasAsync()
    {
        if (!await dialogMessageService.ShowConfirmAsync("确定要清空画布吗？此操作不可撤销", "请确认")) return;
        Connections.Clear();
        Nodes.Clear();
        activeGraph?.Nodes.Clear();
        activeGraph?.Connections.Clear();
        SelectedNode = null;
        IsDirty = true;
    }

    private void LoadGraphInternal(FlowGraph g)
    {
        // ── 1. 批量准备数据 (在内存中完成，不分发 UI 事件) ──
        var newNodeVms = new List<FlowNodeViewModel>();
        var nodeVmMap = new Dictionary<Guid, FlowNodeViewModel>();

        foreach (var node in g.Nodes)
        {
            descriptorMap.TryGetValue(node.NodeType, out var desc);
            var viewmodel = new FlowNodeViewModel(node, colorService, desc?.Parameters, fileDialogService);
            if (desc != null) viewmodel.NodeDescription = desc.Description;
            newNodeVms.Add(viewmodel);
            nodeVmMap[node.Id] = viewmodel;
        }

        var newConnVms = new List<FlowConnectionViewModel>();
        var portVmMap = BuildPortVmMap(newNodeVms);

        foreach (var conn in g.Connections)
        {
            if (portVmMap.TryGetValue(conn.SourcePortId, out var src)
                && portVmMap.TryGetValue(conn.TargetPortId, out var tgt))
            {
                var connVm = new FlowConnectionViewModel(conn.Id, src, tgt, colorService)
                {
                    HasBreakpoint = conn.HasBreakpoint,
                    HasProbe = conn.HasProbe
                };
                newConnVms.Add(connVm);
            }
        }
        if (newConnVms.Count != g.Connections.Count)
        {
            var validConnectionIds = newConnVms.Select(conn => conn.ConnectionId).ToHashSet();
            g.Connections.RemoveAll(conn => !validConnectionIds.Contains(conn.Id));
            IsDirty = true;
        }

        // ── 2. 原子化更新集合 (一瞬间切换，避免逐个加载的视觉闪烁) ──
        // 在切换前处理好状态，避免 UI 订阅后的二次更新
        UpdateConnectionStates(newNodeVms, newConnVms);

        var vm = new FlowGraphViewModel(g)
        {
            Nodes = new BulkObservableCollection<FlowNodeViewModel>(newNodeVms),
            Connections = new BulkObservableCollection<FlowConnectionViewModel>(newConnVms)
        };
        vm.CloseCommand = new DelegateCommand(() => CloseTabCommand.Execute(vm));

        // 订阅集合变化以更新 IsDirty
        vm.Nodes.CollectionChanged += (_, _) => IsDirty = true;
        vm.Connections.CollectionChanged += (_, _) => IsDirty = true;

        // 订阅属性变化以更新 IsDirty
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.Name) ||
                e.PropertyName == nameof(vm.ConnectionStyle))
            {
                IsDirty = true;
            }
        };

        OpenTabs.Add(vm);
        ActiveTab = vm;

        // ── 3. 后续处理 ──
        IsDirty = false;
        UpdateTitle();
    }

    private Dictionary<Guid, PortViewModel> BuildPortVmMap(IEnumerable<FlowNodeViewModel> nodeVms)
    {
        var map = new Dictionary<Guid, PortViewModel>();
        foreach (var nvm in nodeVms)
        {
            foreach (var p in nvm.GetVisiblePorts())
            {
                map.TryAdd(p.PortId, p);
            }
        }
        return map;
    }

    private void SyncViewModelsToGraph()
    {
        if (activeGraph == null) return;
        foreach (var nvm in Nodes)
        {
            var model = activeGraph.Nodes.FirstOrDefault(n => n.Id == nvm.NodeId);
            if (model != null) nvm.SyncToModel(model);
        }

        foreach (var cvm in Connections)
        {
            var model = activeGraph.Connections.FirstOrDefault(c => c.Id == cvm.ConnectionId);
            if (model != null)
            {
                model.HasBreakpoint = cvm.HasBreakpoint;
                model.HasProbe = cvm.HasProbe;
            }
        }
    }

    private void UpdateTitle()
    {
        string name = activeGraph?.Name ?? project.Name;
        Title = IsDirty ? $"{name}*" : name;
    }

    private Task<bool> ConfirmDiscardAsync()
        => dialogMessageService.ShowConfirmAsync("当前流程图有未保存的更改，是否放弃？", "确认");

    // ── 实现 Kwy.MVVM INavigationAware ─────────────────────────────────────────────────────────────
    public bool IsNavigationTarget(NavigationContext _) => true;

    public void OnNavigatedFrom(NavigationContext _)
    { }

    public void OnNavigatedTo(NavigationContext _)
    { }
}

