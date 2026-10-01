using System.Globalization;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class S1F3Parser : IGemMessageParser<IReadOnlyList<GemVid>>
{
    public GemParseResult<IReadOnlyList<GemVid>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out var items, out string? error))
            return GemParseResult<IReadOnlyList<GemVid>>.Failure(error!);
        var result = new List<GemVid>(items.Count);
        foreach (Item item in items)
        {
            if (!GemItemReader.TryUInt32(item, out uint id))
                return GemParseResult<IReadOnlyList<GemVid>>.Failure("S1F3 contains an invalid SVID.");
            result.Add(new GemVid(id));
        }
        return GemParseResult<IReadOnlyList<GemVid>>.Success(result);
    }
}

public sealed class S2F15Parser : IGemMessageParser<IReadOnlyList<GemEquipmentConstantChange>>
{
    public GemParseResult<IReadOnlyList<GemEquipmentConstantChange>> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, null, out var pairs, out string? error))
            return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Failure(error!);
        var result = new List<GemEquipmentConstantChange>(pairs.Count);
        foreach (Item pair in pairs)
        {
            if (!GemItemReader.TryList(pair, 2, out var fields, out error) ||
                !GemItemReader.TryUInt32(fields[0], out uint ecid))
                return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Failure(error ?? "S2F15 contains an invalid ECID/value pair.");
            result.Add(new(new GemEcid(ecid), fields[1]));
        }
        return GemParseResult<IReadOnlyList<GemEquipmentConstantChange>>.Success(result);
    }
}

public sealed class S2F23Parser : IGemMessageParser<GemTraceRequest>
{
    public GemParseResult<GemTraceRequest> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 5, out var fields, out string? error) ||
            !GemItemReader.TryUInt32(fields[0], out uint traceId) ||
            fields[1].Format != SecsFormat.ASCII ||
            !GemItemReader.TryUInt32(fields[2], out uint totalSamples) ||
            !GemItemReader.TryList(fields[4], null, out var svidItems, out error))
            return GemParseResult<GemTraceRequest>.Failure(error ?? "S2F23 structure is invalid.");

        string dsper = fields[1].GetString();
        if (dsper.Length != 8 || !uint.TryParse(dsper, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return GemParseResult<GemTraceRequest>.Failure("DSPER must use hhmmsscc format.");
        int hours = int.Parse(dsper[..2], CultureInfo.InvariantCulture);
        int minutes = int.Parse(dsper.Substring(2, 2), CultureInfo.InvariantCulture);
        int seconds = int.Parse(dsper.Substring(4, 2), CultureInfo.InvariantCulture);
        int centiseconds = int.Parse(dsper.Substring(6, 2), CultureInfo.InvariantCulture);
        if (hours > 23 || minutes > 59 || seconds > 59)
            return GemParseResult<GemTraceRequest>.Failure("DSPER contains an invalid time value.");
        var interval = new TimeSpan(0, hours, minutes, seconds, centiseconds * 10);
        var vids = new List<GemVid>(svidItems.Count);
        foreach (Item item in svidItems)
        {
            if (!GemItemReader.TryUInt32(item, out uint vid))
                return GemParseResult<GemTraceRequest>.Failure("S2F23 contains an invalid SVID.");
            vids.Add(new GemVid(vid));
        }
        return GemParseResult<GemTraceRequest>.Success(new(traceId, interval, totalSamples, vids));
    }
}

public sealed class S2F41Parser : IGemMessageParser<GemRemoteCommand>
{
    public GemParseResult<GemRemoteCommand> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.ASCII ||
            !GemItemReader.TryList(fields[1], null, out var pairs, out error))
            return GemParseResult<GemRemoteCommand>.Failure(error ?? "S2F41 structure is invalid.");
        var parameters = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (Item pair in pairs)
        {
            if (!GemItemReader.TryList(pair, 2, out var parameter, out error) || parameter[0].Format != SecsFormat.ASCII)
                return GemParseResult<GemRemoteCommand>.Failure(error ?? "S2F41 contains an invalid command parameter.");
            string name = parameter[0].GetString();
            if (!parameters.TryAdd(name, parameter[1]))
                return GemParseResult<GemRemoteCommand>.Failure($"Parameter '{name}' is duplicated.");
        }
        return GemParseResult<GemRemoteCommand>.Success(new(fields[0].GetString(), parameters));
    }
}

public sealed record GemAlarmEnableRequest(bool Enabled, GemAlid Alid);

public sealed class S5F3Parser : IGemMessageParser<GemAlarmEnableRequest>
{
    public GemParseResult<GemAlarmEnableRequest> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.Binary ||
            !GemItemReader.TryUInt32(fields[1], out uint alid))
            return GemParseResult<GemAlarmEnableRequest>.Failure(error ?? "S5F3 structure is invalid.");
        byte aled = fields[0].FirstValue<byte>();
        if (aled is not 0 and not 0x80)
            return GemParseResult<GemAlarmEnableRequest>.Failure("ALED must be 0 or 128.");
        return GemParseResult<GemAlarmEnableRequest>.Success(new(aled == 0x80, new GemAlid(alid)));
    }
}

public sealed class S7F3Parser : IGemMessageParser<GemProcessProgram>
{
    public GemParseResult<GemProcessProgram> Parse(SecsMessage message)
    {
        if (!GemItemReader.TryList(message.SecsItem, 2, out var fields, out string? error) ||
            fields[0].Format != SecsFormat.ASCII)
            return GemParseResult<GemProcessProgram>.Failure(error ?? "S7F3 structure is invalid.");
        return GemParseResult<GemProcessProgram>.Success(new(fields[0].GetString(), fields[1]));
    }
}
