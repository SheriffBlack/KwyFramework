using System.Buffers.Binary;

namespace KwyPecvd.Device.Thermal.TemperatureController.Omron;

internal static class OmronEj1Protocol
{
    private const byte ReadHoldingRegisters = 0x03;
    private const byte WriteSingleRegister = 0x06;

    public static byte[] CreateReadRequest(byte unitAddress, ushort register) =>
        CreateRequest(unitAddress, ReadHoldingRegisters, register, 1);

    public static byte[] CreateWriteRequest(byte unitAddress, ushort register, ushort value) =>
        CreateRequest(unitAddress, WriteSingleRegister, register, value);

    public static short ParseReadResponse(ReadOnlySpan<byte> frame, byte unitAddress)
    {
        Validate(frame, unitAddress, ReadHoldingRegisters, 7);
        if (frame[2] != 2)
            throw new FormatException($"Unexpected Omron EJ1 data length {frame[2]}.");

        return BinaryPrimitives.ReadInt16BigEndian(frame.Slice(3, 2));
    }

    public static void ValidateWriteResponse(
        ReadOnlySpan<byte> response,
        ReadOnlySpan<byte> request,
        byte unitAddress)
    {
        Validate(response, unitAddress, WriteSingleRegister, 8);
        if (!response.SequenceEqual(request))
            throw new FormatException("Omron EJ1 write echo does not match the request.");
    }

    private static byte[] CreateRequest(
        byte unitAddress,
        byte function,
        ushort register,
        ushort value)
    {
        var frame = new byte[8];
        frame[0] = unitAddress;
        frame[1] = function;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), register);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), value);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), CalculateCrc(frame.AsSpan(0, 6)));
        return frame;
    }

    private static void Validate(
        ReadOnlySpan<byte> frame,
        byte unitAddress,
        byte function,
        int expectedLength)
    {
        if (frame.Length < 3)
            throw new FormatException($"Unexpected Omron EJ1 frame length {frame.Length}.");
        if (frame[0] != unitAddress)
            throw new FormatException($"Unexpected Omron EJ1 unit address {frame[0]}.");
        if ((frame[1] & 0x80) != 0)
            throw new InvalidOperationException($"Omron EJ1 returned Modbus exception {frame[2]}.");
        if (frame.Length != expectedLength)
            throw new FormatException($"Unexpected Omron EJ1 frame length {frame.Length}.");
        if (frame[1] != function)
            throw new FormatException($"Unexpected Omron EJ1 function 0x{frame[1]:X2}.");

        ushort expectedCrc = CalculateCrc(frame[..^2]);
        ushort actualCrc = BinaryPrimitives.ReadUInt16LittleEndian(frame[^2..]);
        if (actualCrc != expectedCrc)
            throw new FormatException("Omron EJ1 response CRC is invalid.");
    }

    private static ushort CalculateCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
        }

        return crc;
    }
}
