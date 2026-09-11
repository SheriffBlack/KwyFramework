using KwyTemplate.Vision.Services;

namespace KwyTemplate.Vision.Executors;

/// <summary>
/// 流程端口值。用 HasValue 区分“没有数据”和“数据值本身为 null”。
/// </summary>
public sealed class FlowValue
{
    public static FlowValue Missing { get; } = new(false, null, null);

    public bool HasValue { get; }

    public object? Value { get; }

    public string? DataType { get; }

    private FlowValue(bool hasValue, object? value, string? dataType)
    {
        HasValue = hasValue;
        Value = value;
        DataType = dataType;
    }

    public static FlowValue From(object? value, string? dataType = null)
        => new(true, value, dataType);

    public override string ToString()
        => FlowValueDisplayFormatter.FormatFlowValue(this);
}
