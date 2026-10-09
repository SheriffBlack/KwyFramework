using Kwy.Device.PLC.Abstractions;

namespace KwyPecvd.Device.Mfc.Plc;

public sealed class PlcMfc : MfcBase
{
    private readonly PlcMfcDefinition plcDefinition;
    private readonly IPlcPointDefinitionProvider pointProvider;
    private readonly ILogicalPlcReader reader;
    private readonly ILogicalPlcWriter writer;

    public PlcMfc(
        PlcMfcDefinition definition,
        IPlcPointDefinitionProvider pointProvider,
        ILogicalPlcReader reader,
        ILogicalPlcWriter writer)
        : base(definition?.Mfc ?? throw new ArgumentNullException(nameof(definition)), initiallyOffline: true)
    {
        definition.Validate();
        plcDefinition = definition;
        this.pointProvider = pointProvider ?? throw new ArgumentNullException(nameof(pointProvider));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    protected override Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        plcDefinition.Validate();
        ValidatePoint(plcDefinition.SetPointId, PlcDataType.Float, false, true);
        ValidatePoint(plcDefinition.FeedbackId, PlcDataType.Float, true, false);
        if (plcDefinition.OfflineId is not null)
            ValidatePoint(plcDefinition.OfflineId, PlcDataType.Boolean, true, false);
        return Task.CompletedTask;
    }

    protected override async ValueTask<MfcCycleResult> ExchangeAsync(
        double setPoint,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
            plcDefinition.SetPointId,
            (float)setPoint,
            cancellationToken).ConfigureAwait(false);

        var feedback = await reader.ReadAsync<float>(
            plcDefinition.FeedbackId,
            cancellationToken).ConfigureAwait(false);

        var offline = plcDefinition.OfflineId is not null &&
            await reader.ReadAsync<bool>(
                plcDefinition.OfflineId,
                cancellationToken).ConfigureAwait(false);

        return new MfcCycleResult(feedback, offline, DateTimeOffset.UtcNow);
    }

    private void ValidatePoint(
        string pointId,
        PlcDataType expectedType,
        bool requireReadable,
        bool requireWritable)
    {
        var point = pointProvider.GetRequired(pointId);
        if (point.DataType != expectedType)
            throw new InvalidOperationException(
                $"MFC {Id} point '{pointId}' must be {expectedType}, but it is {point.DataType}.");
        if (requireReadable && point.Access == PlcPointAccess.WriteOnly)
            throw new InvalidOperationException($"MFC {Id} point '{pointId}' must be readable.");
        if (requireWritable && point.Access == PlcPointAccess.ReadOnly)
            throw new InvalidOperationException($"MFC {Id} point '{pointId}' must be writable.");
    }
}
