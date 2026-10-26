using System.Numerics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Kwy.UI.ThreeD.Abstractions.Geometry;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Primitives;
using MediaColor = System.Windows.Media.Color;
using HelixMeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using HelixDiffuseMaterial = HelixToolkit.Wpf.SharpDX.DiffuseMaterial;
using WpfMaterial = HelixToolkit.Wpf.SharpDX.Material;

namespace Kwy.UI.WPF.ThreeD.HelixToolkit.Conversion;

internal static class HelixConverters
{
    public static HelixMeshGeometry3D ToHelix(this MeshGeometry source)
    {
        var normals = source.Normals.IsEmpty
            ? CalculateNormals(source.Positions.Span, source.TriangleIndices.Span)
            : source.Normals.ToArray();

        var geometry = new HelixMeshGeometry3D
        {
            Positions = new Vector3Collection(source.Positions.ToArray()),
            TriangleIndices = new IntCollection(source.TriangleIndices.ToArray()),
            Normals = new Vector3Collection(normals)
        };

        if (!source.TextureCoordinates.IsEmpty)
            geometry.TextureCoordinates = new Vector2Collection(source.TextureCoordinates.ToArray());

        if (!source.VertexColors.IsEmpty)
        {
            geometry.Colors = new Color4Collection(
                source.VertexColors.ToArray().Select(static color => new Color4(color)));
        }

        return geometry;
    }

    public static WpfMaterial ToHelix(this MaterialDescriptor source) => source switch
    {
        UnlitMaterialDescriptor material => new HelixDiffuseMaterial
        {
            DiffuseColor = material.Color.ToHelixColor(),
            EnableUnLit = true,
            DiffuseMap = material.Texture?.ToHelix()
        },
        PhongMaterialDescriptor material => new PhongMaterial
        {
            DiffuseColor = material.Diffuse.ToHelixColor(),
            SpecularColor = material.Specular.ToHelixColor(),
            EmissiveColor = material.Emissive.ToHelixColor(),
            SpecularShininess = material.Shininess,
            DiffuseMap = material.DiffuseTexture?.ToHelix()
        },
        PbrMaterialDescriptor material => new PBRMaterial
        {
            AlbedoColor = material.BaseColor.ToHelixColor(),
            MetallicFactor = material.Metallic,
            RoughnessFactor = material.Roughness,
            AlbedoMap = material.BaseColorTexture?.ToHelix()
        },
        _ => throw new NotSupportedException($"Material type '{source.GetType().Name}' is not supported by the HelixToolkit backend.")
    };

    private static TextureModel ToHelix(this TextureSource source)
    {
        if (source.Data.IsEmpty)
            throw new ArgumentException("Texture data cannot be empty.", nameof(source));

        return TextureModel.Create(new MemoryStream(source.Data.ToArray(), writable: false))
            ?? throw new InvalidOperationException($"HelixToolkit could not create a texture from '{source.MediaType}' data.");
    }

    public static Transform3D ToHelix(this Matrix4x4 matrix) => new MatrixTransform3D(new Matrix3D(
        matrix.M11, matrix.M12, matrix.M13, matrix.M14,
        matrix.M21, matrix.M22, matrix.M23, matrix.M24,
        matrix.M31, matrix.M32, matrix.M33, matrix.M34,
        matrix.M41, matrix.M42, matrix.M43, matrix.M44));

    public static Matrix4x4 ToNumerics(this Transform3D transform)
    {
        var matrix = transform.Value;
        return new Matrix4x4(
            (float)matrix.M11, (float)matrix.M12, (float)matrix.M13, (float)matrix.M14,
            (float)matrix.M21, (float)matrix.M22, (float)matrix.M23, (float)matrix.M24,
            (float)matrix.M31, (float)matrix.M32, (float)matrix.M33, (float)matrix.M34,
            (float)matrix.OffsetX, (float)matrix.OffsetY, (float)matrix.OffsetZ, (float)matrix.M44);
    }

    public static MediaColor ToMediaColor(this LinearColor color)
    {
        var value = color.Clamp();
        return MediaColor.FromScRgb(value.Alpha, value.Red, value.Green, value.Blue);
    }

    public static Color4 ToHelixColor(this LinearColor color)
    {
        var value = color.Clamp();
        return new Color4(value.Red, value.Green, value.Blue, value.Alpha);
    }

    private static Vector3[] CalculateNormals(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices)
    {
        var normals = new Vector3[positions.Length];
        for (var index = 0; index < indices.Length; index += 3)
        {
            var i0 = indices[index];
            var i1 = indices[index + 1];
            var i2 = indices[index + 2];
            var normal = Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]);
            if (normal.LengthSquared() <= float.Epsilon)
                continue;

            normals[i0] += normal;
            normals[i1] += normal;
            normals[i2] += normal;
        }

        for (var index = 0; index < normals.Length; index++)
        {
            normals[index] = normals[index].LengthSquared() > float.Epsilon
                ? Vector3.Normalize(normals[index])
                : Vector3.UnitY;
        }

        return normals;
    }
}
