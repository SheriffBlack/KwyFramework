using System.Numerics;
using Kwy.UI.ThreeD.Abstractions.Primitives;

namespace Kwy.UI.ThreeD.Abstractions.Scene;

public abstract record LightDescriptor(LinearColor Color, float Intensity = 1F);

public sealed record AmbientLightDescriptor(LinearColor Color, float Intensity = 1F)
    : LightDescriptor(Color, Intensity);

public sealed record DirectionalLightDescriptor(
    Vector3 Direction,
    LinearColor Color,
    float Intensity = 1F) : LightDescriptor(Color, Intensity);

public sealed record PointLightDescriptor(
    Vector3 Position,
    float Range,
    LinearColor Color,
    float Intensity = 1F) : LightDescriptor(Color, Intensity);
