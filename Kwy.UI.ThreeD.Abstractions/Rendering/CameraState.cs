using System.Numerics;

namespace Kwy.UI.ThreeD.Abstractions.Rendering;

public enum CameraProjection
{
    Perspective,
    Orthographic
}

public sealed record CameraState
{
    public Vector3 Position { get; init; } = new(10F, 10F, 10F);

    public Vector3 Target { get; init; } = Vector3.Zero;

    public Vector3 UpDirection { get; init; } = Vector3.UnitY;

    public CameraProjection Projection { get; init; } = CameraProjection.Perspective;

    public float FieldOfViewRadians { get; init; } = MathF.PI / 4F;

    public float OrthographicWidth { get; init; } = 10F;

    public float NearPlaneDistance { get; init; } = 0.01F;

    public float FarPlaneDistance { get; init; } = 100_000F;
}
