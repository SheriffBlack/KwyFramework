namespace KwyTemplate.Device;

public static class DeviceIds
{
    public const string MainPlc = "PLC.Main";
    public const string MainIoCard = "IO.Main";
    public const string MainScanner = "Scanner.Main";
    public const string MainMarkPrinter = "MarkPrinter.Main";

    public static string Plc(string name) => $"PLC.{Normalize(name)}";

    public static string IoCard(string model, int index) => $"IO.{Normalize(model)}.{index:D2}";

    public static string Instrument(string model, int index) => $"Instrument.{Normalize(model)}.{index:D2}";

    public static string Scanner(string name) => $"Scanner.{Normalize(name)}";

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().Replace(' ', '_');
    }
}
