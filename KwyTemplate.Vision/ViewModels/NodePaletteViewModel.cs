using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;
using Kwy.MVVM.Core;
using System.Collections.ObjectModel;

namespace KwyTemplate.Vision.ViewModels;

/// <summary>
/// 左侧节点面板 ViewModel。
/// <para>
/// 拖拽/双击节点条目时通过 <see cref="OnNodeAddRequested"/> 回调通知
/// <see cref="FlowEditorViewModel"/> 把节点放入画布，
/// 实现面板 VM 与编辑器 VM 之间的解耦（无需在 XAML 里跨层绑定命令）。
/// </para>
/// </summary>
public class NodePaletteViewModel : BindableBase
{
    private readonly IReadOnlyList<IFlowNodeDescriptor> _allDescriptors;
    private readonly HashSet<string> favoriteNodeTypes = new(StringComparer.OrdinalIgnoreCase);
    private bool isFiltering;

    // ── 回调：由 FlowEditorViewModel 设置 ──────────────
    /// <summary>
    /// 当用户双击或拖入一个节点时触发。
    /// 参数：节点类型字符串；画布落点（拖拽时有值，双击时为 null）。
    /// </summary>
    public Action<string, System.Windows.Point?>? OnNodeAddRequested { get; set; }

    // ── 分类分组 ───────────────────────────────────────
    public BulkObservableCollection<NodeCategoryViewModel> Categories { get; } = new();

    // ── 搜索过滤 ───────────────────────────────────────
    private string _searchText = string.Empty;

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    private bool showFavoritesOnly;

    public bool ShowFavoritesOnly
    {
        get => showFavoritesOnly;
        set { if (SetProperty(ref showFavoritesOnly, value)) ApplyFilter(); }
    }

    public NodePaletteViewModel(IEnumerable<IFlowNodeDescriptor> descriptors)
    {
        _allDescriptors = descriptors.ToList();
        BuildCategories(_allDescriptors);
    }

    private DelegateCommand<NodePaletteItemViewModel>? addNodeCommand;

    public DelegateCommand<NodePaletteItemViewModel> AddNodeCommand
        => addNodeCommand ??= new DelegateCommand<NodePaletteItemViewModel>(
            item =>
            {
                if (item != null)
                {
                    RequestAddNode(item.NodeType, dropPoint: null);
                }
            });

    private DelegateCommand<NodePaletteItemViewModel>? toggleFavoriteCommand;

    public DelegateCommand<NodePaletteItemViewModel> ToggleFavoriteCommand
        => toggleFavoriteCommand ??= new DelegateCommand<NodePaletteItemViewModel>(
            item =>
            {
                if (item != null)
                {
                    ToggleFavorite(item);
                }
            });

    // ── 双击添加（由 NodePaletteView code-behind 调用）──
    public void RequestAddNode(string nodeType, System.Windows.Point? dropPoint = null)
        => OnNodeAddRequested?.Invoke(nodeType, dropPoint);

    public void ToggleFavorite(NodePaletteItemViewModel item)
    {
        if (item.IsFavorite)
        {
            favoriteNodeTypes.Remove(item.NodeType);
            item.IsFavorite = false;
        }
        else
        {
            favoriteNodeTypes.Add(item.NodeType);
            item.IsFavorite = true;
        }

        ApplyFilter();
    }

    // ── 内部 ──────────────────────────────────────────
    private void BuildCategories(IEnumerable<IFlowNodeDescriptor> descriptors)
    {
        Categories.Clear();
        foreach (var g in descriptors.GroupBy(d => d.Category).OrderBy(g => g.Key))
        {
            var cat = new NodeCategoryViewModel(g.Key)
            {
                IsExpanded = isFiltering
            };
            foreach (var d in g)
                cat.Items.Add(new NodePaletteItemViewModel(d)
                {
                    IsFavorite = favoriteNodeTypes.Contains(d.NodeType)
                });
            Categories.Add(cat);
        }
    }

    private void ApplyFilter()
    {
        var kw = _searchText.Trim();
        isFiltering = !string.IsNullOrEmpty(kw);
        var filtered = string.IsNullOrEmpty(kw)
            ? _allDescriptors
            : _allDescriptors.Where(d =>
                d.DisplayName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || d.Category.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || d.Description.Contains(kw, StringComparison.OrdinalIgnoreCase));
        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(d => favoriteNodeTypes.Contains(d.NodeType));
        }

        BuildCategories(filtered);
    }
}

// ── 分类 VM ───────────────────────────────────────────
public class NodeCategoryViewModel : BindableBase
{
    public string CategoryName { get; }
    public BulkObservableCollection<NodePaletteItemViewModel> Items { get; } = new();

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public NodeCategoryViewModel(string name) => CategoryName = name;
}

// ── 节点条目 VM ───────────────────────────────────────
public class NodePaletteItemViewModel : BindableBase
{
    public string NodeType { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string? IconKey { get; }

    /// <summary>用于 WPF DragDrop：携带节点类型信息</summary>
    public Func<FlowNode> Factory { get; }

    private bool isFavorite;

    public bool IsFavorite
    {
        get => isFavorite;
        set
        {
            if (SetProperty(ref isFavorite, value))
            {
                RaisePropertyChanged(nameof(FavoriteText));
            }
        }
    }

    public string FavoriteText => IsFavorite ? "★" : "☆";

    public NodePaletteItemViewModel(IFlowNodeDescriptor descriptor)
    {
        NodeType = descriptor.NodeType;
        DisplayName = descriptor.DisplayName;
        Description = descriptor.Description;
        IconKey = descriptor.IconKey;
        Factory = descriptor.CreateDefaultNode;
    }
}
