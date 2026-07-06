using System.Net.Http.Json;
using Kwy.Files;
using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Http.Mapping;

public sealed class JsonHttpMesMessageMapper : IHttpMesMessageMapper
{
    private readonly HttpMesOptions options;

    public JsonHttpMesMessageMapper(HttpMesOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public HttpRequestMessage CreateGetWorkOrderRequest(string workOrderNo)
        => CreateJsonRequest(options.GetWorkOrderUrl, new { WorkOrderNo = workOrderNo }, nameof(CreateGetWorkOrderRequest));

    public HttpRequestMessage CreateCheckRouteRequest(MesUnit unit, MesStation station)
        => CreateJsonRequest(options.CheckRouteUrl, new { Unit = unit, Station = station }, nameof(CreateCheckRouteRequest));

    public HttpRequestMessage CreateGetRecipeRequest(string recipeName)
        => CreateJsonRequest(options.GetRecipeUrl, new { RecipeName = recipeName }, nameof(CreateGetRecipeRequest));

    public HttpRequestMessage CreateUploadTestResultRequest(MesTestResult result)
        => CreateJsonRequest(options.UploadTestResultUrl, result, nameof(CreateUploadTestResultRequest));

    public HttpRequestMessage CreateUploadTraceRequest(MesTraceRecord record)
        => CreateJsonRequest(options.UploadTraceUrl, record, nameof(CreateUploadTraceRequest));

    public async ValueTask<MesResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.IsSuccessStatusCode)
        {
            return MesResult<T>.Fail(((int)response.StatusCode).ToString(), response.ReasonPhrase ?? "HTTP request failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<MesResult<T>>(JsonHelper.WebOptions, cancellationToken).ConfigureAwait(false);
        return result ?? MesResult<T>.Fail("EMPTY_RESPONSE", "MES returned an empty response.");
    }

    public async ValueTask<MesResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.IsSuccessStatusCode)
        {
            return MesResult.Fail(((int)response.StatusCode).ToString(), response.ReasonPhrase ?? "HTTP request failed.");
        }

        var result = await response.Content.ReadFromJsonAsync<MesResult>(JsonHelper.WebOptions, cancellationToken).ConfigureAwait(false);
        return result ?? MesResult.Ok();
    }

    private HttpRequestMessage CreateJsonRequest<T>(string? url, T payload, string operation)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"HTTP MES endpoint is not configured for {operation}.");
        }

        return new HttpRequestMessage(options.DefaultMethod, url)
        {
            Content = JsonContent.Create(payload, options: JsonHelper.WebOptions)
        };
    }
}
