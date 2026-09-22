namespace Kwy.Device.Abstractions.Motion;

/// <summary>高精度空间计算使用的 double 三维向量；避免 System.Numerics.Vector3 的 float 精度损失。</summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D operator +(Vector3D left, Vector3D right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    public static Vector3D operator -(Vector3D value) => new(-value.X, -value.Y, -value.Z);
    public static Vector3D operator *(Vector3D value, double scalar) => new(value.X * scalar, value.Y * scalar, value.Z * scalar);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}

/// <summary>高精度单位四元数，供姿态组合与 SLERP 使用；业务层不直接依赖该数学类型。</summary>
public readonly record struct QuaternionD(double X, double Y, double Z, double W)
{
    public static QuaternionD Identity => new(0, 0, 0, 1);
    public static QuaternionD Normalize(QuaternionD value)
    {
        double length = Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W);
        if (!double.IsFinite(length) || length <= double.Epsilon) throw new ArgumentOutOfRangeException(nameof(value));
        return new(value.X / length, value.Y / length, value.Z / length, value.W / length);
    }
    public static QuaternionD Conjugate(QuaternionD value) => new(-value.X, -value.Y, -value.Z, value.W);
    public static QuaternionD operator *(QuaternionD left, QuaternionD right) => new(
        left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
        left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
        left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
        left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);
    public static double Dot(QuaternionD left, QuaternionD right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
    public static Vector3D Transform(Vector3D vector, QuaternionD rotation)
    {
        QuaternionD q = Normalize(rotation);
        QuaternionD point = new(vector.X, vector.Y, vector.Z, 0);
        QuaternionD result = q * point * Conjugate(q);
        return new(result.X, result.Y, result.Z);
    }
    public static QuaternionD Slerp(QuaternionD from, QuaternionD to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        from = Normalize(from); to = Normalize(to);
        double dot = Dot(from, to);
        if (dot < 0) { to = new(-to.X, -to.Y, -to.Z, -to.W); dot = -dot; }
        if (dot > 0.9995d) return Normalize(new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount, from.Z + (to.Z - from.Z) * amount, from.W + (to.W - from.W) * amount));
        double theta = Math.Acos(Math.Clamp(dot, -1d, 1d));
        double sinTheta = Math.Sin(theta);
        double left = Math.Sin((1d - amount) * theta) / sinTheta;
        double right = Math.Sin(amount * theta) / sinTheta;
        return new(from.X * left + to.X * right, from.Y * left + to.Y * right, from.Z * left + to.Z * right, from.W * left + to.W * right);
    }
    public static QuaternionD FromEulerDegrees(double rx, double ry, double rz)
    {
        double x = rx * Math.PI / 360d, y = ry * Math.PI / 360d, z = rz * Math.PI / 360d;
        double cx = Math.Cos(x), sx = Math.Sin(x), cy = Math.Cos(y), sy = Math.Sin(y), cz = Math.Cos(z), sz = Math.Sin(z);
        return Normalize(new(sx * cy * cz + cx * sy * sz, cx * sy * cz - sx * cy * sz, cx * cy * sz + sx * sy * cz, cx * cy * cz - sx * sy * sz));
    }
}
