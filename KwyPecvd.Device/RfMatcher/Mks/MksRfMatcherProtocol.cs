namespace KwyPecvd.Device.RfMatcher.Mks;

internal static class MksRfMatcherProtocol
{
    public const byte Acknowledge = 0x06;

    public const byte SetControlModeCommand = 93;

    public const byte EnableMotorMoveCommand = 98;

    public const byte SetLoadPositionCommand = 112;

    public const byte SetTunePositionCommand = 122;

    public const byte QueryStatusCommand = 219;

    public static byte[] CreateSetModeRequest(
        byte address,
        byte network,
        RfMatcherMode mode)
    {
        var value = mode switch
        {
            RfMatcherMode.Auto => (byte)1,
            RfMatcherMode.Manual => (byte)2,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        return CreateRequest(
            address,
            SetControlModeCommand,
            [network, 0, value, 0]);
    }

    public static byte[] CreateMotorMoveRequest(
        byte address,
        byte network,
        bool enabled) =>
        CreateRequest(
            address,
            EnableMotorMoveCommand,
            [network, 0, enabled ? (byte)1 : (byte)0, 0]);

    public static byte[] CreatePositionRequest(
        byte address,
        byte network,
        byte command,
        double position)
    {
        if (command is not (
            SetLoadPositionCommand or
            SetTunePositionCommand))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        var raw = checked(
            (ushort)Math.Round(
                position * 100,
                MidpointRounding.AwayFromZero));

        return CreateRequest(
            address,
            command,
            [
                network,
                0,
                (byte)raw,
                (byte)(raw >> 8)
            ]);
    }

    public static byte[] CreateStatusRequest(byte address) =>
        CreateRequest(address, QueryStatusCommand, []);

    public static void ValidateCommandResponse(
        ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            throw new FormatException(
                "MKS RF matcher command response has no CSR byte.");
        }

        if (data[0] != 0)
        {
            throw new InvalidOperationException(
                $"MKS RF matcher rejected command with CSR {data[0]}.");
        }
    }

    public static MksRfMatcherStatus ParseStatus(
        ReadOnlySpan<byte> data,
        byte network)
    {
        if (data.Length != 88)
        {
            throw new FormatException(
                $"MKS RF matcher status length is {data.Length}; expected 88.");
        }

        if (network is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(network));
        }

        var positionOffset = network == 1 ? 4 : 12;
        var outputBit = network == 1 ? 0 : 2;
        var matchedBit = network == 1 ? 1 : 3;
        var autoBit = network == 1 ? 4 : 6;

        var loadPosition =
            ReadUInt16(data, positionOffset) / 100d;

        var tunePosition =
            ReadUInt16(data, positionOffset + 2) / 100d;

        var isOutputOn = IsBitSet(data[0], outputBit);
        var isMatched = IsBitSet(data[0], matchedBit);
        var isAuto = IsBitSet(data[1], autoBit);

        var isFaulted =
            (data[1] & 0b0000_1111) != 0 ||
            (data[3] & 0b0010_0101) != 0;

        return new MksRfMatcherStatus(
            Mode: isAuto
                ? RfMatcherMode.Auto
                : RfMatcherMode.Manual,
            TunePosition: tunePosition,
            LoadPosition: loadPosition,
            IsOutputOn: isOutputOn,
            IsMatched: isMatched,
            IsFaulted: isFaulted);
    }

    public static byte[] ValidateAndGetData(
        ReadOnlySpan<byte> frame,
        byte expectedAddress,
        byte expectedCommand)
    {
        if (frame.Length < 3)
        {
            throw new FormatException("MKS RF matcher response is incomplete.");
        }

        var header = frame[0];
        var address = (byte)(header >> 3);
        var shortLength = header & 0x07;
        var extended = shortLength == 7;
        var dataLength = extended ? frame[2] : shortLength;
        var dataOffset = extended ? 3 : 2;
        var expectedLength = dataOffset + dataLength + 1;

        if (address != expectedAddress || frame[1] != expectedCommand)
        {
            throw new FormatException(
                "MKS RF matcher response address or command does not match the request.");
        }

        if (frame.Length != expectedLength)
        {
            throw new FormatException(
                "MKS RF matcher response length is invalid.");
        }

        byte checksum = 0;

        for (var index = 0; index < frame.Length - 1; index++)
        {
            checksum ^= frame[index];
        }

        if (checksum != frame[^1])
        {
            throw new FormatException(
                "MKS RF matcher response checksum is invalid.");
        }

        return frame.Slice(dataOffset, dataLength).ToArray();
    }

    private static byte[] CreateRequest(
        byte address,
        byte command,
        ReadOnlySpan<byte> data)
    {
        if (address > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        var extended = data.Length >= 7;
        var dataOffset = extended ? 3 : 2;
        var frame = new byte[dataOffset + data.Length + 1];

        frame[0] = (byte)((address << 3) |
            (extended ? 7 : data.Length));
        frame[1] = command;

        if (extended)
        {
            frame[2] = checked((byte)data.Length);
        }

        data.CopyTo(frame.AsSpan(dataOffset));

        byte checksum = 0;

        for (var index = 0; index < frame.Length - 1; index++)
        {
            checksum ^= frame[index];
        }

        frame[^1] = checksum;
        return frame;
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> data,
        int offset) =>
        (ushort)(data[offset] |
                 (data[offset + 1] << 8));

    private static bool IsBitSet(byte value, int bit) =>
        (value & (1 << bit)) != 0;
}
