using System.Text;

namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

internal static class PfeifferA100Protocol
{
    private const byte Start = (byte)'#';
    private const byte End = (byte)'\r';

    public static byte[] CreateRunningRequest(string address, bool running) =>
        CreateRequest(address, "SYS", running ? "ON" : "OFF");

    public static byte[] CreateStatusRequest(string address) =>
        CreateRequest(address, "STA", parameter: null);

    public static void ValidateCommandResponse(ReadOnlySpan<byte> response, string address)
    {
        ReadOnlySpan<byte> data = GetData(response, address);
        if (ContainsError(data))
            throw new InvalidOperationException("Pfeiffer A100 rejected the command.");
    }

    public static PfeifferA100Status ParseStatusResponse(
        ReadOnlySpan<byte> response,
        string address)
    {
        ReadOnlySpan<byte> data = GetData(response, address);
        if (ContainsError(data))
            throw new InvalidOperationException("Pfeiffer A100 rejected the status query.");
        if (data.Length != 32)
            throw new FormatException(
                $"Unexpected Pfeiffer A100 status length {data.Length}; expected 32.");

        byte status = data[0];
        byte alarm1 = data[29];
        byte warning = data[30];
        byte alarm2 = data[31];

        return new PfeifferA100Status(
            IsRunning: (status & 0x40) != 0,
            HasWarning: (warning & 0x01) != 0 || (warning & 0x10) != 0,
            HasAlarm: (alarm1 & 0x01) != 0 || (alarm2 & 0x10) != 0,
            IsOverTemperature: (warning & 0x10) != 0 || (alarm2 & 0x10) != 0);
    }

    private static byte[] CreateRequest(
        string address,
        string command,
        string? parameter)
    {
        string message = $"#{address}{command}{parameter}\r";
        return Encoding.ASCII.GetBytes(message);
    }

    private static ReadOnlySpan<byte> GetData(
        ReadOnlySpan<byte> response,
        string address)
    {
        if (response.Length < 5 || response[0] != Start || response[^1] != End)
            throw new FormatException("Pfeiffer A100 response framing is invalid.");

        Span<byte> expectedAddress = stackalloc byte[3];
        Encoding.ASCII.GetBytes(address, expectedAddress);
        if (!response.Slice(1, 3).SequenceEqual(expectedAddress))
            throw new FormatException("Pfeiffer A100 response address does not match the request.");

        return response[4..^1];
    }

    private static bool ContainsError(ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length - 1; index++)
        {
            if (data[index] == (byte)'E' && data[index + 1] == (byte)'R')
                return true;
        }

        return false;
    }
}
