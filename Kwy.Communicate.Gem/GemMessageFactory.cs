using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static class GemMessageFactory
{
    public static SecsMessage AreYouThereRequest()
        => Create(GemMessageDefinitions.AreYouThereRequest);

    public static SecsMessage AreYouThereResponse()
        => Create(GemMessageDefinitions.AreYouThereResponse);

    public static SecsMessage EstablishCommunicationsRequest(string model, string softwareRevision)
        => Create(GemMessageDefinitions.EstablishCommunicationsRequest, L(A(model), A(softwareRevision)));

    public static SecsMessage SelectedEquipmentStatusRequest(params GemVid[] vids)
        => Create(GemMessageDefinitions.SelectedEquipmentStatusRequest, L(vids.Select(id => U4(id.Value)).ToArray()));

    public static SecsMessage SelectedEquipmentStatusData(IEnumerable<GemVariable> variables)
        => Create(GemMessageDefinitions.SelectedEquipmentStatusData, L(variables.Select(item => item.Value).ToArray()));

    public static SecsMessage RemoteCommand(GemRemoteCommand command)
        => Create(GemMessageDefinitions.HostCommandSend, L(
            A(command.CommandName),
            L(command.Parameters.Select(pair => L(A(pair.Key), pair.Value)).ToArray())));

    public static SecsMessage AlarmReport(GemAlarm alarm)
        => Create(GemMessageDefinitions.AlarmReportSend, L(B((byte)alarm.State), U4(alarm.AlarmId), A(alarm.Text)));

    public static SecsMessage EventReport(uint eventId, IReadOnlyList<GemReport> reports, GemRegistry registry)
    {
        Item[] reportItems = reports
            .Select(report => L(
                U4(report.ReportId),
                L(report.VariableIds.Select(id => registry.Variables.TryGetValue(id, out var variable)
                    ? variable.Value
                    : A(string.Empty)).ToArray())))
            .ToArray();

        return Create(GemMessageDefinitions.EventReportSend, L(U4(eventId), L(reportItems)));
    }

    public static SecsMessage TerminalMessage(GemTerminalMessage message)
        => Create(GemMessageDefinitions.TerminalRequest, L(B(message.TerminalId), A(message.Text)));

    public static SecsMessage ProcessProgramLoadInquire(string ppid, uint length)
        => Create(GemMessageDefinitions.ProcessProgramLoadInquire, L(A(ppid), U4(length)));

    public static SecsMessage ProcessProgramSend(GemRecipe recipe)
        => Create(GemMessageDefinitions.ProcessProgramSend, L(A(recipe.Ppid), recipe.Body));

    public static SecsMessage ProcessProgramRequest(string ppid)
        => Create(GemMessageDefinitions.ProcessProgramRequest, A(ppid));

    public static SecsMessage TraceDataSend(GemTraceSample sample)
        => Create(GemMessageDefinitions.TraceDataSend, L(
            U4(sample.TraceId),
            U4(sample.SampleNumber),
            L(sample.Values.Select(pair => L(U4(pair.Key.Value), pair.Value)).ToArray())));

    private static SecsMessage Create(SecsMessageDefinition definition, Item? item = null)
        => new(definition.Id.Stream, definition.Id.Function, definition.ReplyExpected)
        {
            Name = definition.Name,
            SecsItem = item
        };
}
