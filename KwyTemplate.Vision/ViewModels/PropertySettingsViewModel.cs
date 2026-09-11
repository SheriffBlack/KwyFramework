using KwyTemplate.Vision.ViewModels.Items;
using Kwy.MVVM.Core;
using System.Collections.ObjectModel;

namespace KwyTemplate.Vision.ViewModels;

/// <summary>
/// 属性设置面板 ViewModel：负责显示和编辑选中节点/连线的参数
/// </summary>
public class PropertySettingsViewModel : BindableBase
{
    private FlowNodeViewModel? _selectedNode;

    public FlowNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    private BulkObservableCollection<FlowConnectionViewModel> _selectedConnections = new();

    public BulkObservableCollection<FlowConnectionViewModel> SelectedConnections
    {
        get => _selectedConnections;
        set => SetProperty(ref _selectedConnections, value);
    }

    public event Action<string>? GraphNameChanged;

    private string _graphName = string.Empty;

    public string GraphName
    {
        get => _graphName;
        set { if (SetProperty(ref _graphName, value)) GraphNameChanged?.Invoke(value); }
    }

    private FlowGraphViewModel? _activeGraph;

    public FlowGraphViewModel? ActiveGraph
    {
        get => _activeGraph;
        set => SetProperty(ref _activeGraph, value);
    }

    public string PanelTitle
    {
        get
        {
            if (SelectedNode != null) return "节点属性";
            if (SelectedConnections.Count > 0) return "连线属性";
            return "流程图属性";
        }
    }

    public PropertySettingsViewModel()
    {
    }

    /// <summary>
    /// 更新当前显示的内容
    /// </summary>
    public void UpdateSelection(FlowNodeViewModel? node, IEnumerable<FlowConnectionViewModel> connections, string graphName, FlowGraphViewModel? activeGraph)
    {
        SelectedNode = node;
        // 使用字段赋值避免触发事件循环
        _graphName = graphName;
        RaisePropertyChanged(nameof(GraphName));

        ActiveGraph = activeGraph;

        SelectedConnections.Clear();
        foreach (var conn in connections)
        {
            SelectedConnections.Add(conn);
        }

        RaisePropertyChanged(nameof(PanelTitle));
    }
}
