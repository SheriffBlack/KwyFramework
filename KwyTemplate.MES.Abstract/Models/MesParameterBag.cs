using System.Collections.ObjectModel;
using System.Globalization;

namespace KwyTemplate.MES.Abstract.Models;

public sealed class MesParameterBag
{
    private readonly Dictionary<string, MesParameterValue> values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, MesParameterValue> Values => new ReadOnlyDictionary<string, MesParameterValue>(values);

    public int Count => values.Count;

    public static MesParameterBag Empty { get; } = new();

    public void Set(string key, object? value, string? displayName = null, string? unit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        values[key] = new MesParameterValue(key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, displayName, unit);
    }

    public bool TryGet(string key, out MesParameterValue value)
        => values.TryGetValue(key, out value!);

    public bool TryGetString(string key, out string value)
    {
        if (values.TryGetValue(key, out MesParameterValue? parameter))
        {
            value = parameter.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetInt32(string key, out int value)
    {
        value = 0;
        return TryGetString(key, out string text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public bool TryGetDouble(string key, out double value)
    {
        value = 0;
        return TryGetString(key, out string text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

public sealed record MesParameterValue(
    string Key,
    string Value,
    string? DisplayName = null,
    string? Unit = null);