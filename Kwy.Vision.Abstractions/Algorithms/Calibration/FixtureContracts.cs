using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Abstractions.Algorithms;

/// <summary>
/// Request to calculate a coordinate fixturing transform matrix from a reference base pose to a current detected pose.
/// </summary>
public sealed record FixtureRequest(
    VisionPose2D CurrentPose,
    VisionPose2D ReferencePose);

/// <summary>
/// Result containing the coordinate fixturing transform.
/// </summary>
public sealed record FixtureResult(
    VisionTransform2D Transform);
