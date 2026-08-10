namespace KwyTemplate.App.Models;

public sealed record CorrectionParameterSnapshot(
    string LsStandardValue,
    string LsStandardUnit,
    string RsStandardValue,
    string RsStandardUnit,
    string Frequency,
    string FrequencyUnit,
    string Voltage,
    string VoltageUnit);
