using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

public interface ICorrectionParameterProvider
{
    event EventHandler? ParametersChanged;

    CorrectionParameterSnapshot CreateSnapshot(object? instrumentConfig, bool preferInstrumentFrequency = false);

    void SetFrequencyOverride(string frequency, string frequencyUnit);

    void ClearFrequencyOverride();
}
