using System.Numerics;

namespace Kwy.UI.ThreeD.Abstractions.Geometry;

/// <summary>
/// CPU 端的三角网格数据，各渲染实现负责将其上传到自身的 GPU 资源。
/// </summary>
public sealed class MeshGeometry
{
    public MeshGeometry(
        ReadOnlyMemory<Vector3> positions,
        ReadOnlyMemory<int> triangleIndices,
        ReadOnlyMemory<Vector3> normals = default,
        ReadOnlyMemory<Vector2> textureCoordinates = default,
        ReadOnlyMemory<Vector4> vertexColors = default)
    {
        if (positions.IsEmpty)
            throw new ArgumentException("A mesh must contain at least one position.", nameof(positions));

        if (triangleIndices.IsEmpty || triangleIndices.Length % 3 != 0)
            throw new ArgumentException("Triangle indices must contain one or more complete triangles.", nameof(triangleIndices));

        if (!normals.IsEmpty && normals.Length != positions.Length)
            throw new ArgumentException("The normal count must match the position count.", nameof(normals));

        if (!textureCoordinates.IsEmpty && textureCoordinates.Length != positions.Length)
            throw new ArgumentException("The texture-coordinate count must match the position count.", nameof(textureCoordinates));

        if (!vertexColors.IsEmpty && vertexColors.Length != positions.Length)
            throw new ArgumentException("The vertex-color count must match the position count.", nameof(vertexColors));

        foreach (var index in triangleIndices.Span)
        {
            if ((uint)index >= (uint)positions.Length)
                throw new ArgumentOutOfRangeException(nameof(triangleIndices), index, "A triangle index is outside the position buffer.");
        }

        Positions = positions;
        TriangleIndices = triangleIndices;
        Normals = normals;
        TextureCoordinates = textureCoordinates;
        VertexColors = vertexColors;
    }

    public ReadOnlyMemory<Vector3> Positions { get; }

    public ReadOnlyMemory<int> TriangleIndices { get; }

    public ReadOnlyMemory<Vector3> Normals { get; }

    public ReadOnlyMemory<Vector2> TextureCoordinates { get; }

    public ReadOnlyMemory<Vector4> VertexColors { get; }
}
