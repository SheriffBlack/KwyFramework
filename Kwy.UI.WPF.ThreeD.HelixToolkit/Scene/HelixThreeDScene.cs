using System.Numerics;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Kwy.UI.ThreeD.Abstractions.Rendering;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.WPF.ThreeD.HelixToolkit.Conversion;

namespace Kwy.UI.WPF.ThreeD.HelixToolkit.Scene;

internal sealed class HelixThreeDScene(Viewport3DX viewport) : IThreeDScene
{
    private readonly Dictionary<Guid, HelixThreeDNode> nodes = [];
    private readonly Dictionary<Guid, GroupModel3D?> parents = [];
    private readonly List<LightDescriptor> lights = [];

    public IReadOnlyCollection<IThreeDNode> Nodes => nodes.Values;

    public IReadOnlyCollection<LightDescriptor> Lights => lights;

    internal event EventHandler<ThreeDHitEventArgs>? Hit;

    public IThreeDNode Add(SceneNodeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        VerifyAccess();

        EnsureUniqueIds(descriptor);

        var node = CreateNode(descriptor, null);
        viewport.Items.Add(node.Element);
        return node;
    }

    public bool Remove(Guid nodeId)
    {
        VerifyAccess();
        if (!nodes.Remove(nodeId, out var node))
            return false;

        if (parents[nodeId] is { } parent)
            parent.Children.Remove(node.Element);
        else
            viewport.Items.Remove(node.Element);
        parents.Remove(nodeId);
        UnregisterDescendants(node.Element);
        return true;
    }

    public bool TryGetNode(Guid nodeId, out IThreeDNode? node)
    {
        var found = nodes.TryGetValue(nodeId, out var helixNode);
        node = helixNode;
        return found;
    }

    public void SetLights(IEnumerable<LightDescriptor> newLights)
    {
        ArgumentNullException.ThrowIfNull(newLights);
        VerifyAccess();

        var descriptors = newLights.ToArray();
        var elements = descriptors.Select(CreateLight).ToArray();

        foreach (var light in viewport.Items.OfType<Light3D>().ToArray())
            viewport.Items.Remove(light);

        lights.Clear();
        lights.AddRange(descriptors);
        foreach (var element in elements)
        {
            viewport.Items.Add(element);
        }
    }

    public void Clear()
    {
        VerifyAccess();
        viewport.Items.Clear();
        nodes.Clear();
        parents.Clear();
        lights.Clear();
    }

    private HelixThreeDNode CreateNode(SceneNodeDescriptor descriptor, GroupModel3D? parent)
    {
        Element3D element;
        HelixThreeDNode node;

        switch (descriptor)
        {
            case MeshNodeDescriptor mesh:
                var meshElement = new MeshGeometryModel3D
                {
                    Geometry = mesh.Geometry.ToHelix(),
                    Material = mesh.Material.ToHelix()
                };
                element = meshElement;
                node = new HelixMaterialThreeDNode(mesh, meshElement);
                break;

            case LineNodeDescriptor line:
                var positions = line.Positions.ToArray();
                var indices = new int[Math.Max(0, (positions.Length - 1) * 2)];
                for (var index = 0; index < positions.Length - 1; index++)
                {
                    indices[index * 2] = index;
                    indices[index * 2 + 1] = index + 1;
                }

                element = new LineGeometryModel3D
                {
                    Geometry = new LineGeometry3D
                    {
                        Positions = new Vector3Collection(positions),
                        Indices = new IntCollection(indices)
                    },
                    Color = line.Color.ToMediaColor(),
                    Thickness = line.Thickness
                };
                node = new HelixThreeDNode(line, element);
                break;

            case GroupNodeDescriptor group:
                var groupElement = new GroupModel3D();
                element = groupElement;
                node = new HelixThreeDNode(group, groupElement);
                Register(node, descriptor, parent);
                foreach (var childDescriptor in group.Children)
                    groupElement.Children.Add(CreateNode(childDescriptor, groupElement).Element);
                ApplyCommonProperties(node, descriptor);
                return node;

            default:
                throw new NotSupportedException($"Scene node type '{descriptor.GetType().Name}' is not supported by the HelixToolkit backend.");
        }

        Register(node, descriptor, parent);
        ApplyCommonProperties(node, descriptor);
        return node;
    }

    private void Register(HelixThreeDNode node, SceneNodeDescriptor descriptor, GroupModel3D? parent)
    {
        if (!nodes.TryAdd(node.Id, node))
            throw new InvalidOperationException($"A scene node with ID '{node.Id}' already exists.");
        parents.Add(node.Id, parent);

        if (descriptor is GroupNodeDescriptor)
            return;

        node.Element.MouseDown3D += (_, args) =>
        {
            if (args is not MouseDown3DEventArgs mouseArgs ||
                mouseArgs.HitTestResult is not { IsValid: true } result)
                return;

            Hit?.Invoke(this, new ThreeDHitEventArgs(new ThreeDHitResult(
                node,
                result.PointHit,
                result.NormalAtHit,
                (float)result.Distance,
                result.IndiceStartLocation >= 0 ? result.IndiceStartLocation / 3 : null)));
        };
    }

    private static void ApplyCommonProperties(HelixThreeDNode node, SceneNodeDescriptor descriptor)
    {
        node.Transform = descriptor.Transform;
        node.IsVisible = descriptor.IsVisible;
        node.IsHitTestVisible = descriptor.IsHitTestVisible;
    }

    private static Element3D CreateLight(LightDescriptor descriptor) => descriptor switch
    {
        AmbientLightDescriptor light => new AmbientLight3D
        {
            Color = Scale(light.Color.ToMediaColor(), light.Intensity)
        },
        DirectionalLightDescriptor light => new DirectionalLight3D
        {
            Direction = new Vector3D(light.Direction.X, light.Direction.Y, light.Direction.Z),
            Color = Scale(light.Color.ToMediaColor(), light.Intensity)
        },
        PointLightDescriptor light => new PointLight3D
        {
            Position = new Point3D(light.Position.X, light.Position.Y, light.Position.Z),
            Range = light.Range,
            Color = Scale(light.Color.ToMediaColor(), light.Intensity)
        },
        _ => throw new NotSupportedException($"Light type '{descriptor.GetType().Name}' is not supported by the HelixToolkit backend.")
    };

    private static System.Windows.Media.Color Scale(System.Windows.Media.Color color, float intensity)
    {
        var scale = Math.Max(0F, intensity);
        return System.Windows.Media.Color.FromScRgb(
            color.ScA,
            Math.Clamp(color.ScR * scale, 0F, 1F),
            Math.Clamp(color.ScG * scale, 0F, 1F),
            Math.Clamp(color.ScB * scale, 0F, 1F));
    }

    private void UnregisterDescendants(Element3D element)
    {
        if (element is not GroupModel3D group)
            return;

        foreach (var child in group.Children)
        {
            var pair = nodes.FirstOrDefault(entry => ReferenceEquals(entry.Value.Element, child));
            if (pair.Key != Guid.Empty)
            {
                nodes.Remove(pair.Key);
                parents.Remove(pair.Key);
            }
            UnregisterDescendants(child);
        }
    }

    private void EnsureUniqueIds(SceneNodeDescriptor root)
    {
        var descriptorIds = new HashSet<Guid>();
        Visit(root);
        return;

        void Visit(SceneNodeDescriptor descriptor)
        {
            if (nodes.ContainsKey(descriptor.Id) || !descriptorIds.Add(descriptor.Id))
                throw new InvalidOperationException($"A scene node with ID '{descriptor.Id}' already exists.");

            if (descriptor is not GroupNodeDescriptor group)
                return;

            foreach (var child in group.Children)
                Visit(child);
        }
    }

    private void VerifyAccess()
    {
        if (!viewport.Dispatcher.CheckAccess())
            throw new InvalidOperationException("The 3D scene must be modified on its WPF dispatcher thread.");
    }
}
