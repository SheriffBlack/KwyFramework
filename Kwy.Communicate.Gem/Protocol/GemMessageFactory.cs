using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 构造标准 SxFy + Data
/// </summary>
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

    public static SecsMessage EquipmentConstantAcknowledge(byte ackCode)
        => new(2, 16) { Name = "EquipmentConstantAcknowledge", SecsItem = B(ackCode) };

    public static SecsMessage TraceInitializeAcknowledge(byte ackCode)
        => new(2, 24) { Name = "TraceInitializeAcknowledge", SecsItem = B(ackCode) };

    public static SecsMessage HostCommandAcknowledge(GemAckCode ackCode)
        => new(2, 42)
        {
            Name = "HostCommandAcknowledge",
            SecsItem = L(B((byte)ackCode), L())
        };

    public static SecsMessage EnableDisableAlarmAcknowledge(byte ackCode)
        => new(5, 4) { Name = "EnableDisableAlarmAcknowledge", SecsItem = B(ackCode) };

    public static SecsMessage ProcessProgramAcknowledge(byte ackCode)
        => new(7, 4) { Name = "ProcessProgramAcknowledge", SecsItem = B(ackCode) };

    public static SecsMessage RemoteCommand(GemRemoteCommand command)
        => Create(GemMessageDefinitions.HostCommandSend, L(
            A(command.CommandName),
            L(command.Parameters.Select(pair => L(A(pair.Key), pair.Value)).ToArray())));

    public static SecsMessage AlarmReport(GemAlarm alarm)
    {
        var alarmCode = (byte)(alarm.AlarmCode & 0x7F);
        var alcd = alarm.State == GemAlarmState.Set
            ? (byte)(alarmCode | 0x80)
            : alarmCode;

        return Create(
            GemMessageDefinitions.AlarmReportSend,
            L(B(alcd), U4(alarm.AlarmId), A(alarm.Text)));
    }

    public static SecsMessage EventReport(
        uint eventId,
        IReadOnlyList<GemReport> reports,
        GemRegistry registry,
        uint dataId = 0)
        => EventReport(
            new GemCeid(eventId),
            reports.Select(report => new GemReportDefinition(
                new GemRptid(report.ReportId),
                report.VariableIds.Select(id => new GemVid(id)).ToArray())).ToArray(),
            registry.Data,
            dataId);

    public static SecsMessage EventReport(
        GemCeid eventId,
        IReadOnlyList<GemReportDefinition> reports,
        GemDataSnapshot data,
        uint dataId = 0)
        => EventReport(
            eventId,
            reports,
            data.CaptureRequired(reports.SelectMany(report => report.VariableIds)),
            dataId);

    public static SecsMessage EventReport(
        GemCeid eventId,
        IReadOnlyList<GemReportDefinition> reports,
        IReadOnlyDictionary<GemVid, GemVariable> values,
        uint dataId = 0)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(values);

        Item[] reportItems = reports
            .Select(report => L(
                U4(report.Rptid.Value),
                L(report.VariableIds.Select(id => values.TryGetValue(id, out GemVariable? variable)
                    ? variable.Value
                    : throw new InvalidOperationException($"No runtime value is available for VID {id.Value}.")).ToArray())))
            .ToArray();

        return Create(GemMessageDefinitions.EventReportSend, L(U4(dataId), U4(eventId.Value), L(reportItems)));
    }

    public static SecsMessage TerminalMessage(GemTerminalMessage message)
        => Create(GemMessageDefinitions.TerminalRequest, L(B(message.TerminalId), A(message.Text)));

    public static SecsMessage ProcessProgramLoadInquire(string ppid, uint length)
        => Create(GemMessageDefinitions.ProcessProgramLoadInquire, L(A(ppid), U4(length)));

    public static SecsMessage ProcessProgramSend(GemProcessProgram processProgram)
        => Create(GemMessageDefinitions.ProcessProgramSend, L(A(processProgram.Ppid), processProgram.Body));

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
