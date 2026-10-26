using System.Windows;
using HelixToolkit.Wpf.SharpDX;
using Kwy.UI.ThreeD.Abstractions.Rendering;

namespace Kwy.UI.WPF.ThreeD.HelixToolkit.Rendering;

public sealed class HelixThreeDBackend : IThreeDBackend<FrameworkElement>
{
    public string Name => "HelixToolkit SharpDX";

    public ThreeDCapabilities Capabilities { get; } = new(
        ThreeDFeatures.Shadows |
        ThreeDFeatures.PbrMaterials |
        ThreeDFeatures.Instancing |
        ThreeDFeatures.PostProcessing |
        ThreeDFeatures.ScreenSpaceLines |
        ThreeDFeatures.OffscreenRendering);

    public IThreeDViewport<FrameworkElement> CreateViewport(ThreeDViewportOptions? options = null) =>
        new HelixThreeDViewport(options ?? new ThreeDViewportOptions());
}
