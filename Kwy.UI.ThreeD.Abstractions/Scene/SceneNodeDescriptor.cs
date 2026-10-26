using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Geometry;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Primitives;

namespace Kwy.UI.ThreeD.Abstractions.Scene;

public abstract record SceneNodeDescriptor
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string? Name { get; init; }

    public object? Tag { get; init; }

    public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;

    public bool IsVisible { get; init; } = true;

    public bool IsHitTestVisible { get; init; } = true;
}

public sealed record MeshNodeDescriptor : SceneNodeDescriptor
{
    public required MeshGeometry Geometry { get; init; }

    public required MaterialDescriptor Material { get; init; }
}

public sealed record LineNodeDescriptor : SceneNodeDescriptor
{
    public required ReadOnlyMemory<Vector3> Positions { get; init; }

    public LinearColor Color { get; init; } = LinearColor.White;

    public float Thickness { get; init; } = 1F;
}

public sealed record GroupNodeDescriptor : SceneNodeDescriptor
{
    public IReadOnlyList<SceneNodeDescriptor> Children { get; init; } = [];
}
