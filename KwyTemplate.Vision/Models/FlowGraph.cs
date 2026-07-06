using Kwy.MVVM.Core;
using System.Text.Json.Serialization;

namespace KwyTemplate.Vision.Models;

public enum FlowGraphRunStatus
{
    NotRun,
    Running,
    Ok,
    Ng,
    Skipped
}

/// <summary>
/// 整张流程图的数据：包含所有节点 + 连线，支持 JSON 序列化保存/加载。
/// </summary>
public class FlowGraph : BindableBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = "未命名流程";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _connectionStyle = 2;

    public int ConnectionStyle
    {
        get => _connectionStyle;
        set => SetProperty(ref _connectionStyle, value);
    }

    private bool _isProjectEntry;

    public bool IsProjectEntry
    {
        get => _isProjectEntry;
        set => SetProperty(ref _isProjectEntry, value);
    }

    private bool _isDisabled;

    public bool IsDisabled
    {
        get => _isDisabled;
        set => SetProperty(ref _isDisabled, value);
    }

    private bool _isRenaming;

    [JsonIgnore]
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    private FlowGraphRunStatus _runStatus = FlowGraphRunStatus.NotRun;

    [JsonIgnore]
    public FlowGraphRunStatus RunStatus
    {
        get => _runStatus;
        set
        {
            if (SetProperty(ref _runStatus, value))
            {
                RaisePropertyChanged(nameof(RunStatusText));
            }
        }
    }

    private TimeSpan? _lastElapsed;

    [JsonIgnore]
    public TimeSpan? LastElapsed
    {
        get => _lastElapsed;
        set
        {
            if (SetProperty(ref _lastElapsed, value))
            {
                RaisePropertyChanged(nameof(LastElapsedText));
            }
        }
    }

    [JsonIgnore]
    public string RunStatusText => RunStatus switch
    {
        FlowGraphRunStatus.Running => "RUN",
        FlowGraphRunStatus.Ok => "OK",
        FlowGraphRunStatus.Ng => "NG",
        FlowGraphRunStatus.Skipped => "SKIP",
        _ => "READY"
    };

    [JsonIgnore]
    public string LastElapsedText => LastElapsed is TimeSpan elapsed
        ? $"{elapsed.TotalMilliseconds:F0} ms"
        : string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<FlowNode> Nodes { get; set; } = new();

    public List<FlowConnection> Connections { get; set; } = new();

    /// <summary>根据端口 Id 快速找到所属节点。</summary>
    public FlowNode? FindNodeByPortId(Guid portId)
    {
        foreach (var node in Nodes)
        {
            if (node.InputPorts.Any(p => p.Id == portId)
                || node.OutputPorts.Any(p => p.Id == portId))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>根据端口 Id 找到端口实例。</summary>
    public FlowPort? FindPort(Guid portId)
    {
        foreach (var node in Nodes)
        {
            var port = node.InputPorts.FirstOrDefault(p => p.Id == portId)
                    ?? node.OutputPorts.FirstOrDefault(p => p.Id == portId);
            if (port != null)
            {
                return port;
            }
        }

        return null;
    }
}
