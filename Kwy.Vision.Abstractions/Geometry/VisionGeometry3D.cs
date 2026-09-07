namespace Kwy.Vision.Abstractions.Geometry;

public readonly record struct VisionPoint3D(double X, double Y, double Z);

public readonly record struct VisionVector3D(double X, double Y, double Z)
{
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public VisionVector3D Normalize()
    {
        double length = Length;
        if (!double.IsFinite(length) || length <= double.Epsilon)
        {
            throw new InvalidOperationException("A zero or non-finite vector cannot be normalized.");
        }

        return new VisionVector3D(X / length, Y / length, Z / length);
    }
}

/// <summary>A 3D rotation quaternion in X, Y, Z, W order.</summary>
public readonly record struct VisionQuaternion(double X, double Y, double Z, double W)
{
    public static VisionQuaternion Identity { get; } = new(0, 0, 0, 1);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

    public VisionQuaternion Normalize()
    {
        double length = Length;
        if (!double.IsFinite(length) || length <= double.Epsilon)
        {
            throw new InvalidOperationException("A zero or non-finite quaternion cannot be normalized.");
        }

        return new VisionQuaternion(X / length, Y / length, Z / length, W / length);
    }
}

/// <summary>A 3D rigid pose composed of position and quaternion rotation.</summary>
public readonly record struct VisionPose3D(
    VisionPoint3D Position,
    VisionQuaternion Orientation)
{
    public static VisionPose3D Identity { get; } = new(
        new VisionPoint3D(0, 0, 0),
        VisionQuaternion.Identity);
}

/// <summary>A 3D plane represented by one point and a normal vector.</summary>
public readonly record struct VisionPlane(
    VisionPoint3D Point,
    VisionVector3D Normal)
{
    public VisionPlane Normalize()
        => new(Point, Normal.Normalize());

    public double SignedDistanceTo(VisionPoint3D point)
    {
        VisionVector3D normal = Normal.Normalize();
        return (point.X - Point.X) * normal.X
            + (point.Y - Point.Y) * normal.Y
            + (point.Z - Point.Z) * normal.Z;
    }
}
