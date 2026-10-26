using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Geometry;
using Kwy.UI.ThreeD.Abstractions.Materials;
using Kwy.UI.ThreeD.Abstractions.Primitives;
using Kwy.UI.ThreeD.Abstractions.Rendering;
using Kwy.UI.ThreeD.Abstractions.Scene;

namespace Kwy.UI.WPF.ThreeD.Demo;

public sealed class MainWindowViewModel
{
    public IThreeDSceneSource Scene { get; } = new DemoSceneSource();

    public CameraState Camera { get; set; } = new()
    {
        Position = new Vector3(4F, 3F, 5F),
        Target = Vector3.Zero
    };

    private sealed class DemoSceneSource : IThreeDSceneSource
    {
        public void Populate(IThreeDScene scene)
        {
            scene.SetLights([
                new AmbientLightDescriptor(new LinearColor(0.35F, 0.35F, 0.4F)),
                new DirectionalLightDescriptor(new Vector3(-1F, -2F, -1F), LinearColor.White, 1.2F)
            ]);

            scene.Add(new MeshNodeDescriptor
            {
                Name = "FacadeCube",
                Geometry = CreateCube(),
                Material = new PhongMaterialDescriptor(
                    new LinearColor(0.12F, 0.48F, 0.9F),
                    LinearColor.White,
                    LinearColor.Black,
                    48F)
            });
        }

        private static MeshGeometry CreateCube()
        {
            Vector3[] positions =
            [
                new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
                new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)
            ];
            int[] triangles =
            [
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            ];
            return new MeshGeometry(positions, triangles);
        }
    }
}
