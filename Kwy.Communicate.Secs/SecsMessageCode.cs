namespace Kwy.Communicate.Secs;

/// <summary>
/// Identifies a SECS-II message by its stream and function.
/// </summary>
/// <remarks>
/// Unlike HSMS message types, the set of SxFy messages is extensible. This value type therefore
/// provides constants for common standard messages while still allowing equipment-specific codes.
/// </remarks>
public readonly record struct SecsMessageCode
{
    public static SecsMessageCode S1F1AreYouThere { get; } = new(1, 1);

    public static SecsMessageCode S1F2OnLineData { get; } = new(1, 2);

    public static SecsMessageCode S1F13EstablishCommunicationsRequest { get; } = new(1, 13);

    public static SecsMessageCode S1F14EstablishCommunicationsAcknowledge { get; } = new(1, 14);

    public SecsMessageCode(byte stream, byte function)
    {
        if (stream > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(stream), stream, "A SECS-II stream must be between 0 and 127.");
        }

        Stream = stream;
        Function = function;
    }

    public byte Stream { get; }

    public byte Function { get; }

    public bool IsPrimary => Function % 2 == 1;

    public override string ToString() => $"S{Stream}F{Function}";
}
