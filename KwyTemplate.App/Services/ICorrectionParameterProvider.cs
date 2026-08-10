using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

public interface ICorrectionParameterProvider
{
    event EventHandler? ParametersChanged;

    CorrectionParameterSnapshot CreateSnapshot(object? instrumentConfig);
}
