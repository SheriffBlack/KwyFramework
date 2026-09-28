using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static class GemMessageFactory
{
    public static SecsMessage AreYouThereRequest()
        => Create(1, 1, true, "AreYouThereRequest");

    public static SecsMessage AreYouThereResponse()
        => Create(1, 2, false, "AreYouThereResponse");

    public static SecsMessage EstablishCommunicationsRequest(string model, string softwareRevision)
        => Create(1, 13, true, "EstablishCommunicationsRequest", L(A(model), A(softwareRevision)));

    public static SecsMessage SelectedEquipmentStatusRequest(params GemVid[] vids)
        => Create(1, 3, true, "SelectedEquipmentStatusRequest", L(vids.Select(id => U4(id.Value)).ToArray()));

    public static SecsMessage SelectedEquipmentStatusData(IEnumerable<GemVariable> variables)
        => Create(1, 4, false, "SelectedEquipmentStatusData", L(variables.Select(item => item.Value).ToArray()));

    public static SecsMessage RemoteCommand(GemRemoteCommand command)
        => Create(2, 41, true, "HostCommandSend", L(
            A(command.CommandName),
            L(command.Parameters.Select(pair => L(A(pair.Key), pair.Value)).ToArray())));

    public static SecsMessage AlarmReport(GemAlarm alarm)
        => Create(5, 1, true, "AlarmReportSend", L(B((byte)alarm.State), U4(alarm.AlarmId), A(alarm.Text)));

    public static SecsMessage EventReport(uint eventId, IReadOnlyList<GemReport> reports, GemRegistry registry)
    {
        Item[] reportItems = reports
            .Select(report => L(
                U4(report.ReportId),
                L(report.VariableIds.Select(id => registry.Variables.TryGetValue(id, out var variable)
                    ? variable.Value
                    : A(string.Empty)).ToArray())))
            .ToArray();

        return Create(6, 11, true, "EventReportSend", L(U4(eventId), L(reportItems)));
    }

    public static SecsMessage TerminalMessage(GemTerminalMessage message)
        => Create(10, 1, true, "TerminalRequest", L(B(message.TerminalId), A(message.Text)));

    public static SecsMessage ProcessProgramLoadInquire(string ppid, uint length)
        => Create(7, 1, true, "ProcessProgramLoadInquire", L(A(ppid), U4(length)));

    public static SecsMessage ProcessProgramSend(GemRecipe recipe)
        => Create(7, 3, true, "ProcessProgramSend", L(A(recipe.Ppid), recipe.Body));

    public static SecsMessage ProcessProgramRequest(string ppid)
        => Create(7, 5, true, "ProcessProgramRequest", A(ppid));

    public static SecsMessage TraceDataSend(GemTraceSample sample)
        => Create(6, 1, true, "TraceDataSend", L(
            U4(sample.TraceId),
            U4(sample.SampleNumber),
            L(sample.Values.Select(pair => L(U4(pair.Key.Value), pair.Value)).ToArray())));

    private static SecsMessage Create(byte stream, byte function, bool replyExpected, string name, Item? item = null)
        => new(stream, function, replyExpected)
        {
            Name = name,
            SecsItem = item
        };
}
