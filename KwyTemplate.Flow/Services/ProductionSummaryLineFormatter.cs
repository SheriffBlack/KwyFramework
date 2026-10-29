namespace KwyTemplate.Flow.Services;

/// <summary>
/// Formats the six-field line protocol consumed by the external MES summary importer.
/// </summary>
internal static class ProductionSummaryLineFormatter
{
    private const char Separator = '，';

    public static string AutomaticNumber(string code, string value, string unit = "颗")
        => Format(code, "数值", unit, value, "自动");

    public static string ManualText(string code, string value)
        => Format(code, "文字", string.Empty, value, "手动输入");

    private static string Format(string code, string valueType, string unit, string value, string source)
        => string.Join(Separator, code, code, valueType, unit, value, source);
}
