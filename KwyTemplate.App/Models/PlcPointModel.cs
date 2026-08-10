using Kwy.MVVM.Core;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Models;

public sealed class PlcPointModel : BindableBase
{
    private string valueText = string.Empty;
    private string writeValueText = string.Empty;
    private string statusMessage = string.Empty;
    private DateTime? lastUpdatedAt;

    public PlcPointModel(MachinePlcPointDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        WriteValueText = definition.DataType == typeof(bool) ? "False" : "0";
    }

    public MachinePlcPointDefinition Definition { get; }

    public string Key => Definition.Key;

    public string Address => Definition.Address;

    public string DisplayName => Definition.DisplayName;

    public string DataTypeName => ToDisplayTypeName(Definition.DataType);

    public bool IsReadOnly => Definition.IsReadOnly;

    public bool CanWrite => !Definition.IsReadOnly;

    public string ValueText
    {
        get => valueText;
        set => SetProperty(ref valueText, value);
    }

    public string WriteValueText
    {
        get => writeValueText;
        set => SetProperty(ref writeValueText, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public DateTime? LastUpdatedAt
    {
        get => lastUpdatedAt;
        set
        {
            if (SetProperty(ref lastUpdatedAt, value))
            {
                RaisePropertyChanged(nameof(LastUpdatedAtText));
            }
        }
    }

    public string LastUpdatedAtText => LastUpdatedAt?.ToString("HH:mm:ss.fff") ?? "--";

    private static string ToDisplayTypeName(Type dataType)
    {
        if (dataType == typeof(bool))
        {
            return "Bool";
        }

        if (dataType == typeof(short))
        {
            return "Int16";
        }

        if (dataType == typeof(ushort))
        {
            return "UInt16";
        }

        if (dataType == typeof(int))
        {
            return "Int32";
        }

        if (dataType == typeof(uint))
        {
            return "UInt32";
        }

        if (dataType == typeof(float))
        {
            return "Float";
        }

        return dataType.Name;
    }
}
