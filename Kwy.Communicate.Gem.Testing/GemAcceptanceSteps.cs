using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem.Testing;

/// <summary>常用 GEM Equipment 验收步骤。项目可以使用 <see cref="GemAcceptanceStep"/> 组合客户专用步骤。</summary>
public static class GemAcceptanceSteps
{
    public static GemAcceptanceStep Connect(string name = "Connect HSMS")
        => new(name, async (context, token) =>
        {
            await context.Transport.ConnectAsync(token).ConfigureAwait(false);
            if (!context.Transport.IsConnected)
                throw new InvalidOperationException("Transport did not enter the connected state.");
            return "HSMS transport connected.";
        });

    public static GemAcceptanceStep Disconnect(string name = "Disconnect HSMS")
        => new(name, async (context, token) =>
        {
            await context.Transport.DisconnectAsync(token).ConfigureAwait(false);
            return "HSMS transport disconnected.";
        });

    public static GemAcceptanceStep EstablishCommunication(
        string hostModel,
        string hostSoftwareRevision,
        string name = "Establish GEM communication")
        => SendAndExpect(name,
            () => GemMessageFactory.EstablishCommunicationsRequest(hostModel, hostSoftwareRevision),
            GemMessageDefinitions.EstablishCommunicationsAcknowledge.Id,
            response => RequireAck(response.SecsItem, 0, "COMMACK"));

    public static GemAcceptanceStep RequestOnline(string name = "Request Online")
        => SendAndExpect(name, GemMessageFactory.RequestOnline,
            GemMessageDefinitions.OnlineAcknowledge.Id,
            response => RequireAck(response.SecsItem, 0, "ONLACK"));

    public static GemAcceptanceStep ReadStatusVariables(
        IEnumerable<GemVid> vids,
        string name = "Read status variables")
    {
        GemVid[] requested = vids?.ToArray() ?? throw new ArgumentNullException(nameof(vids));
        return SendAndExpect(name,
            () => GemMessageFactory.SelectedEquipmentStatusRequest(requested),
            GemMessageDefinitions.SelectedEquipmentStatusData.Id,
            response =>
            {
                if (response.SecsItem is null || response.SecsItem.Format != SecsFormat.List)
                    throw new InvalidDataException("S1F4 data must be a list.");
                if (response.SecsItem.Count != requested.Length)
                    throw new InvalidDataException($"S1F4 returned {response.SecsItem.Count} values; expected {requested.Length}.");
                return $"Received {requested.Length} status values.";
            });
    }

    public static GemAcceptanceStep ExecuteRemoteCommand(
        GemRemoteCommand command,
        string name = "Execute remote command")
        => SendAndExpect(name,
            () => GemMessageFactory.RemoteCommand(command),
            new SecsMessageId(2, 42),
            response =>
            {
                if (response.SecsItem is null || response.SecsItem.Format != SecsFormat.List || response.SecsItem.Count != 2)
                    throw new InvalidDataException("S2F42 must be L[2] { HCACK, CPACK list }.");
                byte ack = response.SecsItem.Items[0].FirstValue<byte>();
                if (ack is not (byte)GemHostCommandAckCode.Completed and
                    not (byte)GemHostCommandAckCode.Accepted)
                    throw new InvalidDataException($"S2F42 rejected the command with HCACK={ack}.");
                return $"Command '{command.CommandName}' was accepted; completion must be verified by state/event.";
            });

    public static GemAcceptanceStep ReadProcessProgramDirectory(
        string name = "Read process program directory")
        => SendAndExpect(name,
            GemMessageFactory.CurrentProcessProgramDirectoryRequest,
            GemMessageDefinitions.CurrentProcessProgramDirectoryData.Id,
            response =>
            {
                if (response.SecsItem is null || response.SecsItem.Format != SecsFormat.List)
                    throw new InvalidDataException("S7F20 must contain a PPID list.");
                string[] ppids = response.SecsItem.Items.Select(x => x.GetString()).ToArray();
                return $"Received {ppids.Length} PPIDs: {string.Join(", ", ppids)}";
            });

    public static GemAcceptanceStep WaitForEvent(
        GemCeid? expectedCeid = null,
        string name = "Receive collection event")
        => WaitForPrimary(name, GemMessageDefinitions.EventReportSend.Id,
            message =>
            {
                if (message.SecsItem is null || message.SecsItem.Format != SecsFormat.List || message.SecsItem.Count != 3)
                    throw new InvalidDataException("S6F11 must be L[3] { DATAID, CEID, reports }.");
                uint ceid = message.SecsItem.Items[1].FirstValue<uint>();
                if (expectedCeid is { } expected && ceid != expected.Value)
                    throw new InvalidDataException($"Received CEID {ceid}; expected {expected.Value}.");
                return $"Received CEID {ceid}.";
            }, () => new SecsMessage(6, 12) { Name = "EventReportAcknowledge", SecsItem = B(0) });

    public static GemAcceptanceStep WaitForAlarm(
        GemAlid? expectedAlid = null,
        string name = "Receive alarm report")
        => WaitForPrimary(name, GemMessageDefinitions.AlarmReportSend.Id,
            message =>
            {
                if (message.SecsItem is null || message.SecsItem.Format != SecsFormat.List || message.SecsItem.Count != 3)
                    throw new InvalidDataException("S5F1 must be L[3] { ALCD, ALID, ALTX }.");
                uint alid = message.SecsItem.Items[1].FirstValue<uint>();
                if (expectedAlid is { } expected && alid != expected.Value)
                    throw new InvalidDataException($"Received ALID {alid}; expected {expected.Value}.");
                return $"Received ALID {alid}: {message.SecsItem.Items[2].GetString()}";
            }, () => new SecsMessage(5, 2) { Name = "AlarmReportAcknowledge", SecsItem = B(0) });

    public static GemAcceptanceStep Custom(
        string name,
        Func<GemAcceptanceContext, CancellationToken, Task<string?>> action)
        => new(name, action ?? throw new ArgumentNullException(nameof(action)));

    private static GemAcceptanceStep SendAndExpect(
        string name,
        Func<SecsMessage> requestFactory,
        SecsMessageId expected,
        Func<SecsMessage, string> inspect)
        => new(name, async (context, token) =>
        {
            using SecsMessage request = requestFactory();
            using SecsMessage? response = await context.Transport.SendAsync(request, token).ConfigureAwait(false);
            if (response is null)
                throw new InvalidDataException($"No secondary message was returned for {request.Name}.");
            RequireMessage(response, expected);
            return inspect(response);
        });

    private static GemAcceptanceStep WaitForPrimary(
        string name,
        SecsMessageId expected,
        Func<SecsMessage, string> inspect,
        Func<SecsMessage?> replyFactory)
        => new(name, async (context, token) =>
        {
            await foreach (PrimaryMessageWrapper primary in context.Transport
                .ReadAllAsync(token).WithCancellation(token).ConfigureAwait(false))
            {
                SecsMessage message = primary.PrimaryMessage;
                RequireMessage(message, expected);
                string detail = inspect(message);
                if (message.ReplyExpected)
                {
                    using SecsMessage? reply = replyFactory();
                    if (!await primary.TryReplyAsync(reply, token).ConfigureAwait(false))
                        throw new InvalidOperationException($"Could not reply to S{message.S}F{message.F}.");
                }
                return detail;
            }
            throw new EndOfStreamException($"Primary message stream ended before {expected} was received.");
        });

    private static void RequireMessage(SecsMessage message, SecsMessageId expected)
    {
        if (message.S != expected.Stream || message.F != expected.Function)
            throw new InvalidDataException($"Received S{message.S}F{message.F}; expected {expected}.");
    }

    private static string RequireAck(Item? item, byte accepted, string name)
    {
        if (item is null)
            throw new InvalidDataException($"{name} is missing.");
        Item ackItem = item.Format == SecsFormat.List ? item.Items[0] : item;
        byte value = ackItem.FirstValue<byte>();
        if (value != accepted)
            throw new InvalidDataException($"{name}={value}; expected {accepted}.");
        return $"{name}={value}.";
    }
}
