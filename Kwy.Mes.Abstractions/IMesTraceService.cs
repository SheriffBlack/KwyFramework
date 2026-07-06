using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesTraceService
{
    Task<MesResult> UploadTraceAsync(MesTraceRecord record, CancellationToken cancellationToken = default);
}
