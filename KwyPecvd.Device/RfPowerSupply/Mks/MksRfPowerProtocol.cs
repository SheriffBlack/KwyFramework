using System.Globalization;
using System.Text;

namespace KwyPecvd.Device.RfPowerSupply.Mks;

internal static class MksRfPowerProtocol
{
    public static byte[] CreateSetPowerRequest(double power)
    {
        int rawPower = checked((int)Math.Round(power, MidpointRounding.AwayFromZero));
        return CreateRequest($"OEM{rawPower}");
    }

    public static byte[] CreateEnableRequest(bool enabled) =>
        CreateRequest(enabled ? "TRG" : "OFF");

    public static byte[] CreateStatusRequest() => CreateRequest("ACT");

    public static void ValidateCommandResponse(ReadOnlySpan<byte> response)
    {
        string text = ParseLine(response);
        if (text.Contains("ERR", StringComparison.OrdinalIgnoreCase) || text.Contains('?'))
            throw new InvalidOperationException($"MKS RF power supply rejected command: {text}");
    }

    public static MksRfPowerStatus ParseStatusResponse(ReadOnlySpan<byte> response)
    {
        string text = ParseLine(response).Replace("*", string.Empty, StringComparison.Ordinal);
        string[] fields = text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (fields.Length != 5)
            throw new FormatException(
                $"Unexpected MKS RF status field count {fields.Length}; expected 5.");

        return new MksRfPowerStatus(
            SetPoint: ParseNumber(fields[0]),
            ForwardPower: ParseNumber(fields[1]),
            ReflectedPower: ParseNumber(fields[2]),
            PaVoltage: ParseNumber(fields[3]),
            Frequency: ParseNumber(fields[4]));
    }

    private static byte[] CreateRequest(string command) =>
        Encoding.ASCII.GetBytes(command + "\r");

    private static string ParseLine(ReadOnlySpan<byte> response)
    {
        if (response.Length < 2 || response[^1] != (byte)'\r')
            throw new FormatException("MKS RF response framing is invalid.");

        string text = Encoding.ASCII.GetString(response[..^1]).Trim();
        if (text.Length == 0)
            throw new FormatException("MKS RF response is empty.");

        return text;
    }

    private static double ParseNumber(string value)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result) ||
            !double.IsFinite(result))
            throw new FormatException($"MKS RF value '{value}' is invalid.");

        return result;
    }
}
