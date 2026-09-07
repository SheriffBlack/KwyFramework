using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Gem;

public static class GemMessageFactory
{
    public static SecsMessage AreYouThereRequest()
        => new(1, 1, true, Name: "AreYouThereRequest");

    public static SecsMessage AreYouThereResponse()
        => new(1, 2, Name: "AreYouThereResponse");

    public static SecsMessage SelectedEquipmentStatusRequest(params GemVid[] vids)
        => new(1, 3, true, SecsItem.L(vids.Select(id => SecsItem.U4(id.Value)).ToArray()), Name: "SelectedEquipmentStatusRequest");

    public static SecsMessage SelectedEquipmentStatusData(IEnumerable<GemVariable> variables)
        => new(1, 4, false, SecsItem.L(variables.Select(item => item.Value).ToArray()), Name: "SelectedEquipmentStatusData");

    public static SecsMessage RemoteCommand(GemRemoteCommand command)
        => new(2, 41, true, SecsItem.L(
            SecsItem.A(command.CommandName),
            SecsItem.L(command.Parameters.Select(pair => SecsItem.L(SecsItem.A(pair.Key), pair.Value)).ToArray())),
            Name: "HostCommandSend");

    public static SecsMessage AlarmReport(GemAlarm alarm)
        => new(5, 1, true, SecsItem.L(SecsItem.B((byte)alarm.State), SecsItem.U4(alarm.AlarmId), SecsItem.A(alarm.Text)), Name: "AlarmReportSend");

    public static SecsMessage EventReport(uint eventId, IReadOnlyList<GemReport> reports, GemRegistry registry)
    {
        SecsItem[] reportItems = reports
            .Select(report => SecsItem.L(
                SecsItem.U4(report.ReportId),
                SecsItem.L(report.VariableIds.Select(id => registry.Variables.TryGetValue(id, out var variable) ? variable.Value : SecsItem.A(string.Empty)).ToArray())))
            .ToArray();

        return new SecsMessage(6, 11, true, SecsItem.L(SecsItem.U4(eventId), SecsItem.L(reportItems)), Name: "EventReportSend");
    }

    public static SecsMessage TerminalMessage(GemTerminalMessage message)
        => new(10, 1, true, SecsItem.L(SecsItem.B(message.TerminalId), SecsItem.A(message.Text)), Name: "TerminalRequest");

    public static SecsMessage ProcessProgramLoadInquire(string ppid, uint length)
        => new(7, 1, true, SecsItem.L(SecsItem.A(ppid), SecsItem.U4(length)), Name: "ProcessProgramLoadInquire");

    public static SecsMessage ProcessProgramSend(GemRecipe recipe)
        => new(7, 3, true, SecsItem.L(SecsItem.A(recipe.Ppid), recipe.Body), Name: "ProcessProgramSend");

    public static SecsMessage ProcessProgramRequest(string ppid)
        => new(7, 5, true, SecsItem.A(ppid), Name: "ProcessProgramRequest");

    public static SecsMessage TraceDataSend(GemTraceSample sample)
        => new(6, 1, true, SecsItem.L(
            SecsItem.U4(sample.TraceId),
            SecsItem.U4(sample.SampleNumber),
            SecsItem.L(sample.Values.Select(pair => SecsItem.L(SecsItem.U4(pair.Key.Value), pair.Value)).ToArray())),
            Name: "TraceDataSend");
}
