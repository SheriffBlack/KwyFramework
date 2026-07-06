using Kwy.Communicate.Abstractions;
using Kwy.Mes.Abstractions.Models;
using Kwy.Mes.Core;
using Kwy.Mes.Http.Mapping;

namespace Kwy.Mes.Http;

public sealed class HttpMesService : MesServiceBase
{
    private readonly IRequestClient<HttpRequestMessage, HttpResponseMessage> client;
    private readonly IHttpMesMessageMapper mapper;

    public HttpMesService(
        IRequestClient<HttpRequestMessage, HttpResponseMessage> client,
        IHttpMesMessageMapper mapper)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    protected override async Task<MesResult> ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return MesResult.Ok();
    }

    protected override async Task<MesResult> DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        return MesResult.Ok();
    }

    public override async Task<MesResult<MesWorkOrder>> GetWorkOrderAsync(string workOrderNo, CancellationToken cancellationToken = default)
    {
        using var request = mapper.CreateGetWorkOrderRequest(workOrderNo);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await mapper.ReadResultAsync<MesWorkOrder>(response, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<MesResult<MesRouteCheckResult>> CheckRouteAsync(MesUnit unit, MesStation station, CancellationToken cancellationToken = default)
    {
        using var request = mapper.CreateCheckRouteRequest(unit, station);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await mapper.ReadResultAsync<MesRouteCheckResult>(response, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<MesResult<MesRecipe>> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
    {
        using var request = mapper.CreateGetRecipeRequest(recipeName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await mapper.ReadResultAsync<MesRecipe>(response, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<MesResult> UploadTestResultAsync(MesTestResult result, CancellationToken cancellationToken = default)
    {
        using var request = mapper.CreateUploadTestResultRequest(result);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await mapper.ReadResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public override async Task<MesResult> UploadTraceAsync(MesTraceRecord record, CancellationToken cancellationToken = default)
    {
        using var request = mapper.CreateUploadTraceRequest(record);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await mapper.ReadResultAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
