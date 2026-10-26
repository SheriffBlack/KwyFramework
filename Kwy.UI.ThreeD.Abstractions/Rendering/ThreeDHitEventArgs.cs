using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Scene;

namespace Kwy.UI.ThreeD.Abstractions.Rendering;

public sealed record ThreeDHitResult(
    IThreeDNode Node,
    Vector3 Position,
    Vector3 Normal,
    float Distance,
    int? TriangleIndex = null);

public sealed class ThreeDHitEventArgs(ThreeDHitResult result) : EventArgs
{
    public ThreeDHitResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
}
