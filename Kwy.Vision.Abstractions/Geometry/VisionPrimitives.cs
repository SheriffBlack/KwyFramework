namespace Kwy.Vision.Abstractions.Geometry;

public readonly record struct VisionPoint(double X, double Y);

public readonly record struct VisionSize(double Width, double Height);

public readonly record struct VisionLine(VisionPoint Start, VisionPoint End);
