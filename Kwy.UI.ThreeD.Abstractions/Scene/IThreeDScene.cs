using Kwy.UI.ThreeD.Abstractions.Materials;

namespace Kwy.UI.ThreeD.Abstractions.Scene;

public interface IThreeDScene
{
    IReadOnlyCollection<IThreeDNode> Nodes { get; }

    IReadOnlyCollection<LightDescriptor> Lights { get; }

    IThreeDNode Add(SceneNodeDescriptor descriptor);

    bool Remove(Guid nodeId);

    bool TryGetNode(Guid nodeId, out IThreeDNode? node);

    void SetLights(IEnumerable<LightDescriptor> lights);

    void Clear();
}

public interface IThreeDNode
{
    Guid Id { get; }

    string? Name { get; }

    object? Tag { get; set; }

    System.Numerics.Matrix4x4 Transform { get; set; }

    bool IsVisible { get; set; }

    bool IsHitTestVisible { get; set; }
}

public interface IMaterialThreeDNode : IThreeDNode
{
    MaterialDescriptor? Material { get; set; }
}
