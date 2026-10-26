using Kwy.UI.ThreeD.Abstractions.Primitives;

namespace Kwy.UI.ThreeD.Abstractions.Materials;

public abstract record MaterialDescriptor;

public sealed record UnlitMaterialDescriptor(
    LinearColor Color,
    TextureSource? Texture = null) : MaterialDescriptor;

public sealed record PhongMaterialDescriptor(
    LinearColor Diffuse,
    LinearColor Specular,
    LinearColor Emissive,
    float Shininess = 32F,
    TextureSource? DiffuseTexture = null) : MaterialDescriptor;

public sealed record PbrMaterialDescriptor(
    LinearColor BaseColor,
    float Metallic = 0F,
    float Roughness = 1F,
    TextureSource? BaseColorTexture = null) : MaterialDescriptor;

/// <summary>
/// 编码后的纹理字节数据及其媒体类型，例如 image/png。
/// </summary>
public sealed record TextureSource(ReadOnlyMemory<byte> Data, string MediaType);
