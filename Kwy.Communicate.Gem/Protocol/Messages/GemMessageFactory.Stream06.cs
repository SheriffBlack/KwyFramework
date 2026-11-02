using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Gem;

public static partial class GemMessageFactory
{
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

        var reportValues = reports
            .Select(report => new
            {
                Report = report,
                Values = report.VariableIds.Select(id => values.TryGetValue(id, out GemVariable? variable)
                        ? variable
                        : throw new InvalidOperationException($"No runtime value is available for VID {id.Value}."))
                    .ToArray()
            })
            .ToArray();
        Item[] reportItems = reportValues
            .Select(item => L(
                U4(item.Report.Rptid.Value),
                L(item.Values.Select(variable => GemItemCloner.Clone(variable.Value)).ToArray())))
            .ToArray();

        return Create(
            GemMessageDefinitions.EventReportSend,
            L(U4(dataId), U4(eventId.Value), L(reportItems)));
    }

    public static SecsMessage TraceDataSend(GemTraceSample sample)
        => Create(GemMessageDefinitions.TraceDataSend, L(
            U4(sample.TraceId),
            U4(sample.SampleNumber),
            L(sample.Values
                .Select(pair => L(U4(pair.Key.Value), GemItemCloner.Clone(pair.Value)))
                .ToArray())));
}
