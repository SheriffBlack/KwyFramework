using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Materials;

namespace Kwy.UI.ThreeD.Abstractions.Updates;

public abstract record ThreeDSceneUpdate(Guid NodeId);

public sealed record SetNodeTransform(Guid NodeId, Matrix4x4 Transform)
    : ThreeDSceneUpdate(NodeId);

public sealed record SetNodeVisibility(Guid NodeId, bool IsVisible)
    : ThreeDSceneUpdate(NodeId);

public sealed record SetNodeMaterial(Guid NodeId, MaterialDescriptor Material)
    : ThreeDSceneUpdate(NodeId);

public sealed record RemoveSceneNode(Guid NodeId)
    : ThreeDSceneUpdate(NodeId);
