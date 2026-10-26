using System.Numerics;
using System.Windows;
using HelixToolkit.Wpf.SharpDX;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.WPF.ThreeD.HelixToolkit.Conversion;

namespace Kwy.UI.WPF.ThreeD.HelixToolkit.Scene;

internal class HelixThreeDNode(
    SceneNodeDescriptor descriptor,
    Element3D element) : IThreeDNode
{
    public Guid Id { get; } = descriptor.Id;

    public string? Name { get; } = descriptor.Name;

    public object? Tag { get; set; } = descriptor.Tag;

    public Matrix4x4 Transform
    {
        get => Element.Transform.ToNumerics();
        set => Element.Transform = value.ToHelix();
    }

    public bool IsVisible
    {
        get => Element.Visibility == Visibility.Visible;
        set => Element.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool IsHitTestVisible
    {
        get => Element.IsHitTestVisible;
        set => Element.IsHitTestVisible = value;
    }

    internal Element3D Element { get; } = element;
}

internal sealed class HelixMaterialThreeDNode(
    MeshNodeDescriptor descriptor,
    MeshGeometryModel3D element) : HelixThreeDNode(descriptor, element), IMaterialThreeDNode
{
    public MaterialDescriptor? Material
    {
        get => material;
        set
        {
            material = value;
            element.Material = value?.ToHelix();
        }
    }

    private MaterialDescriptor? material = descriptor.Material;
}
