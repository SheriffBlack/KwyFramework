using System.Diagnostics.CodeAnalysis;

namespace Kwy.Device.Abstractions.Instrument;

/// <summary>
/// Instrument-side measurement limit exposed in the same engineering unit as the measured value.
/// </summary>
public sealed record InstrumentMeasurementLimit(
    double? LowerLimit = null,
    double? UpperLimit = null,
    string? Unit = null);

/// <summary>
/// Optional instrument capability for exposing one shared limit from current device configuration.
/// </summary>
public interface IMeasurementLimitProvider
{
    bool TryGetMeasurementLimit([NotNullWhen(true)] out InstrumentMeasurementLimit? limit);
}

/// <summary>
/// Optional instrument capability for exposing per-test limits from current device configuration.
/// </summary>
public interface IMeasurementLimitSetProvider
{
    bool TryGetMeasurementLimits([NotNullWhen(true)] out IReadOnlyDictionary<string, InstrumentMeasurementLimit>? limits);
}
