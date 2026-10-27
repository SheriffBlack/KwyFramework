using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Scene;

namespace Kwy.UI.ThreeD.Abstractions.Updates;

public interface IThreeDUpdateSink
{
    void Apply(ThreeDSceneUpdate update);

    void Reset();
}

public abstract record ThreeDSceneUpdate;

public sealed record AddSceneNode(SceneNodeDescriptor Node)
    : ThreeDSceneUpdate;

public sealed record SetNodeTransform(Guid NodeId, Matrix4x4 Transform)
    : ThreeDSceneUpdate;

public sealed record SetNodeVisibility(Guid NodeId, bool IsVisible)
    : ThreeDSceneUpdate;

public sealed record SetNodeHitTestVisibility(Guid NodeId, bool IsHitTestVisible)
    : ThreeDSceneUpdate;

public sealed record SetNodeMaterial(Guid NodeId, MaterialDescriptor Material)
    : ThreeDSceneUpdate;

public sealed record RemoveSceneNode(Guid NodeId)
    : ThreeDSceneUpdate;

public sealed record SetSceneLights(IReadOnlyList<LightDescriptor> Lights)
    : ThreeDSceneUpdate;

public sealed record ClearThreeDScene : ThreeDSceneUpdate
{
    public static ClearThreeDScene Instance { get; } = new();

    private ClearThreeDScene()
    {
    }
}
