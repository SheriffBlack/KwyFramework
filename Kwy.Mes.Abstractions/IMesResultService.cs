using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesResultService
{
    Task<MesResult> UploadTestResultAsync(MesTestResult result, CancellationToken cancellationToken = default);
}
