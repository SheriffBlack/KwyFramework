using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Materials;

namespace Kwy.UI.ThreeD.Abstractions.State;

/// <summary>
/// 与具体渲染后端无关的节点运行状态；null 表示保持对应属性不变。
/// </summary>
public sealed record ThreeDNodeState(
    Guid NodeId,
    Matrix4x4? Transform = null,
    bool? IsVisible = null,
    bool? IsHitTestVisible = null,
    MaterialDescriptor? Material = null);
