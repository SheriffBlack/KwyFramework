using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KwyPecvd.Device.Mfc.Plc;

public sealed record PlcMfcDefinition
{
    public required MfcDefinition Mfc { get; init; }

    public required string SetPointId { get; init; }

    public required string FeedbackId { get; init; }

    public string? OfflineId { get; init; }

    public void Validate()
    {
        Mfc.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(SetPointId);
        ArgumentException.ThrowIfNullOrWhiteSpace(FeedbackId);
    }
}
