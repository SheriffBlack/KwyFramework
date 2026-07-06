using KwyTemplate.Vision.Models;
using Kwy.MVVM.Core;
using Kwy.UI.WPF.FlowDesigner.Controls;
using System.Collections.ObjectModel;

namespace KwyTemplate.Vision.ViewModels.Items;

/// <summary>
/// 单个流程图的画布 ViewModel（用于多标签页切换）
/// </summary>
public class FlowGraphViewModel : BindableBase
{
    public FlowGraph Graph { get; }

    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                Graph.Name = value;
                RaisePropertyChanged(nameof(Title));
            }
        }
    }

    public string Title => Name;

    private ConnectionStyle _connectionStyle;

    public ConnectionStyle ConnectionStyle
    {
        get => _connectionStyle;
        set
        {
            if (SetProperty(ref _connectionStyle, value))
            {
                Graph.ConnectionStyle = (int)value;
                RaisePropertyChanged(nameof(ConnectionStyleIndex));
            }
        }
    }

    public int ConnectionStyleIndex
    {
        get => (int)ConnectionStyle;
        set => ConnectionStyle = (ConnectionStyle)value;
    }

    private BulkObservableCollection<FlowNodeViewModel> _nodes = new();

    public BulkObservableCollection<FlowNodeViewModel> Nodes
    {
        get => _nodes;
        set => SetProperty(ref _nodes, value);
    }

    private BulkObservableCollection<FlowConnectionViewModel> _connections = new();

    public BulkObservableCollection<FlowConnectionViewModel> Connections
    {
        get => _connections;
        set => SetProperty(ref _connections, value);
    }

    private object? _selectedItem;

    public object? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public System.Windows.Input.ICommand? CloseCommand { get; set; }

    public FlowGraphViewModel(FlowGraph graph)
    {
        Graph = graph;
        _name = graph.Name;
        _connectionStyle = (ConnectionStyle)graph.ConnectionStyle;
    }
}
