namespace Kwy.UI.ThreeD.Abstractions.Rendering;

[Flags]
public enum ThreeDFeatures
{
    None = 0,
    Shadows = 1 << 0,
    PbrMaterials = 1 << 1,
    Instancing = 1 << 2,
    PostProcessing = 1 << 3,
    ScreenSpaceLines = 1 << 4,
    OffscreenRendering = 1 << 5,
    ModelImport = 1 << 6
}

public sealed record ThreeDCapabilities(
    ThreeDFeatures Features,
    int MaximumTextureSize = 0,
    int MaximumLights = 0)
{
    public bool Supports(ThreeDFeatures feature) => (Features & feature) == feature;
}
