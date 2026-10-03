using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesResultUploadService
{
    Task<MesResult> UploadTestResultAsync(MesTestResultUploadRequest request, CancellationToken cancellationToken = default);
}