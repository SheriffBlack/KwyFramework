using KwyTemplate.MES.Models;

namespace KwyTemplate.MES.Services;

public interface IMesStandardSampleService
{
    
    Task<MesResult<MesStandardSampleSetup>> GetStandardSampleAsync(MesStandardSampleRequest request, CancellationToken cancellationToken = default);

    Task<MesResult> SaveStandardSampleCheckEquipmentAsync(MesStandardSampleCheckSaveRequest request, CancellationToken cancellationToken = default);

    Task<MesResult> SaveStandardSampleCheckAsync(MesStandardSampleCheckSaveRequest request, CancellationToken cancellationToken = default);
}