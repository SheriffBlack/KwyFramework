using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesResultUploadService
{
    Task<MesResult> UploadTestResultAsync(MesTestResultUploadRequest request, CancellationToken cancellationToken = default);
}