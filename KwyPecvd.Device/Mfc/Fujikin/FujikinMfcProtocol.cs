using System.Buffers.Binary;

namespace KwyPecvd.Device.Mfc.Fujikin;

internal static class FujikinMfcProtocol
{
    public const byte Acknowledge = 0x06;
    public const byte NegativeAcknowledge = 0x16;
    public const byte Start = 0x02;

    private const byte ReadCommandType = 0x80;
    private const byte WriteCommandType = 0x81;

    private static readonly byte[] SetPointCommand = [0x69, 0x01, 0xA4];
    private static readonly byte[] RampTimeCommand = [0x6A, 0x01, 0xA4];
    private static readonly byte[] IndicatedFlowCommand = [0x6A, 0x01, 0xA9];

    public static byte[] CreateWriteSetPointRequest(byte address, short rawSetPoint)
    {
        Span<byte> parameter = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(parameter, rawSetPoint);
        return CreateRequest(address, WriteCommandType, SetPointCommand, parameter);
    }

    public static byte[] CreateReadIndicatedFlowRequest(byte address) =>
        CreateRequest(address, ReadCommandType, IndicatedFlowCommand, []);

    public static byte[] CreateWriteRampTimeRequest(byte address, int rawRampTime)
    {
        Span<byte> parameter = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(parameter, rawRampTime);
        return CreateRequest(address, WriteCommandType, RampTimeCommand, parameter);
    }

    public static short ParseIndicatedFlowResponse(
        ReadOnlySpan<byte> response,
        byte expectedAddress)
    {
        ValidateResponse(
            response,
            expectedAddress,
            ReadCommandType,
            IndicatedFlowCommand,
            sizeof(short));

        return BinaryPrimitives.ReadInt16LittleEndian(response.Slice(7, sizeof(short)));
    }

    public static int GetFrameLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
            throw new ArgumentException("Fujikin response header must contain four bytes.", nameof(header));

        return header[3] + 6;
    }

    private static byte[] CreateRequest(
        byte address,
        byte commandType,
        ReadOnlySpan<byte> command,
        ReadOnlySpan<byte> parameter)
    {
        var frame = new byte[command.Length + parameter.Length + 6];
        frame[0] = address;
        frame[1] = Start;
        frame[2] = commandType;
        frame[3] = checked((byte)(command.Length + parameter.Length));
        command.CopyTo(frame.AsSpan(4));
        parameter.CopyTo(frame.AsSpan(4 + command.Length));
        frame[^2] = 0;
        frame[^1] = CalculateChecksum(frame);
        return frame;
    }

    private static void ValidateResponse(
        ReadOnlySpan<byte> response,
        byte expectedAddress,
        byte expectedCommandType,
        ReadOnlySpan<byte> expectedCommand,
        int expectedDataLength)
    {
        int expectedLength = expectedCommand.Length + expectedDataLength + 6;
        if (response.Length != expectedLength)
            throw new FormatException($"Unexpected Fujikin response length {response.Length}; expected {expectedLength}.");
        if (response[0] != expectedAddress)
            throw new FormatException($"Unexpected Fujikin response address 0x{response[0]:X2}.");
        if (response[1] != Start)
            throw new FormatException("Fujikin response start byte is invalid.");
        if (response[2] != expectedCommandType)
            throw new FormatException($"Unexpected Fujikin response command type 0x{response[2]:X2}.");
        if (!response.Slice(4, expectedCommand.Length).SequenceEqual(expectedCommand))
            throw new FormatException("Fujikin response command does not match the request.");
        if (response[^1] != CalculateChecksum(response))
            throw new FormatException("Fujikin response checksum is invalid.");
    }

    private static byte CalculateChecksum(ReadOnlySpan<byte> frame)
    {
        byte checksum = 0;
        for (int index = 1; index < frame.Length - 2; index++)
            checksum = unchecked((byte)(checksum + frame[index]));

        return checksum;
    }
}
