using KwyTemplate.Vision.Models;

using Kwy.ComponentModel;

namespace KwyTemplate.Vision.NodeDescriptors;

/// <summary>
/// 节点描述符：描述一种节点类型的静态元信息（用于节点面板展示）
/// 实现此接口 = 注册一种新的节点类型
/// </summary>
public interface IFlowNodeDescriptor
{
    /// <summary>节点类型唯一标识，与 FlowNode.NodeType 对应</summary>
    string NodeType { get; }

    /// <summary>显示名称，如 "图像阈值"</summary>
    string DisplayName { get; }

    /// <summary>分类，用于节点面板分组，如 "图像处理" / "通信" / "逻辑控制"</summary>
    string Category { get; }

    /// <summary>节点描述说明</summary>
    string Description { get; }

    /// <summary>节点图标资源 Key（对应 Kwy.UI.WPF 资源字典中的图标）</summary>
    string? IconKey { get; }

    /// <summary>节点参数定义。Descriptor 只描述参数元数据，具体业务由 Executor 执行。</summary>
    IReadOnlyList<KwyParameterDefinition> Parameters => Array.Empty<KwyParameterDefinition>();

    /// <summary>
    /// 创建一个带默认端口和参数的新节点实例
    /// </summary>
    FlowNode CreateDefaultNode();
}
