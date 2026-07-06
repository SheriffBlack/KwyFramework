using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Http.Mapping;

public interface IHttpMesMessageMapper
{
    HttpRequestMessage CreateGetWorkOrderRequest(string workOrderNo);

    HttpRequestMessage CreateCheckRouteRequest(MesUnit unit, MesStation station);

    HttpRequestMessage CreateGetRecipeRequest(string recipeName);

    HttpRequestMessage CreateUploadTestResultRequest(MesTestResult result);

    HttpRequestMessage CreateUploadTraceRequest(MesTraceRecord record);

    ValueTask<MesResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken = default);

    ValueTask<MesResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken = default);
}
