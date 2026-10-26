using System.Numerics;
using System.Runtime.ExceptionServices;
using Kwy.UI.ThreeD.Abstractions.Geometry;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Primitives;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.WPF.ThreeD.HelixToolkit.Rendering;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class HelixThreeDBackendTests
{
    [Fact]
    public void Viewport_MapsSceneNodesAndSupportsTransformUpdates()
        => RunInSta(() =>
        {
            var backend = new HelixThreeDBackend();
            using var viewport = backend.CreateViewport();
            var child = new MeshNodeDescriptor
            {
                Name = "Triangle",
                Geometry = CreateTriangle(),
                Material = new PhongMaterialDescriptor(
                    LinearColor.FromBytes(80, 140, 220),
                    LinearColor.White,
                    LinearColor.Black)
            };
            var group = new GroupNodeDescriptor { Children = [child] };

            var groupNode = viewport.Scene.Add(group);
            Assert.True(viewport.Scene.TryGetNode(child.Id, out var childNode));

            var expected = Matrix4x4.CreateTranslation(10F, 20F, 30F);
            childNode!.Transform = expected;

            Assert.Equal(expected, childNode.Transform);
            Assert.True(viewport.Scene.Remove(groupNode.Id));
            Assert.Empty(viewport.Scene.Nodes);
        });

    private static MeshGeometry CreateTriangle() => new(
        new Vector3[]
        {
            new(0F, 0F, 0F),
            new(1F, 0F, 0F),
            new(0F, 1F, 0F)
        },
        new[] { 0, 1, 2 });

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
