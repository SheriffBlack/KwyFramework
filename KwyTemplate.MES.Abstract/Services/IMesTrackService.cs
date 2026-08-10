using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Services;

public interface IMesTrackService
{
    Task<MesResult<MesTrackResult>> TrackInAsync(MesTrackRequest request, CancellationToken cancellationToken = default);

    Task<MesResult<MesTrackResult>> TrackOutAsync(MesTrackOutRequest request, CancellationToken cancellationToken = default);
}