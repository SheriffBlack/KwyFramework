using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public async Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.Alarms.TryGetValue(alarm.AlarmId, out GemAlarmDefinition? definition))
            throw new InvalidOperationException($"ALID {alarm.AlarmId} is not defined in the GEM interface catalog.");
        if (!definition.Enabled)
            throw new InvalidOperationException($"ALID {alarm.AlarmId} is disabled.");

        registry.SetAlarm(alarm);
        await SendPrimaryOrSpoolAsync(GemMessageFactory.AlarmReport(alarm), cancellationToken).ConfigureAwait(false);
    }

    public async Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default)
    {
        if (!registry.Catalog.Events.TryGetValue(eventId, out GemCollectionEventDefinition? collectionEvent))
            throw new KeyNotFoundException($"Collection event {eventId} is not registered.");
        if (!collectionEvent.Enabled)
            throw new InvalidOperationException($"Collection event {eventId} is disabled.");

        GemReportDefinition[] reports = collectionEvent.LinkedReports
            .Select(id => registry.Catalog.Reports[id.Value])
            .ToArray();
        IReadOnlyDictionary<GemVid, GemVariable> eventSnapshot = registry.Data.CaptureRequired(
            reports.SelectMany(report => report.VariableIds));

        foreach ((GemVid vid, GemVariable value) in eventSnapshot)
        {
            GemVariableDefinition definition = registry.Catalog.Variables[vid.Value];
            if (definition.Format is SecsFormat expectedFormat && value.Value.Format != expectedFormat)
                throw new InvalidOperationException(
                    $"VID {vid.Value} expects SECS format {expectedFormat}, but runtime value uses {value.Value.Format}.");
        }

        await SendPrimaryOrSpoolAsync(
            GemMessageFactory.EventReport(collectionEvent.Ceid, reports, eventSnapshot),
            cancellationToken).ConfigureAwait(false);
    }

    public Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default)
        => SendPrimaryOrSpoolAsync(GemMessageFactory.TerminalMessage(message), cancellationToken);
}
