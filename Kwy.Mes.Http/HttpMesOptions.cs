namespace Kwy.Mes.Http;

public sealed class HttpMesOptions
{
    public string? GetWorkOrderUrl { get; set; }

    public string? CheckRouteUrl { get; set; }

    public string? GetRecipeUrl { get; set; }

    public string? UploadTestResultUrl { get; set; }

    public string? UploadTraceUrl { get; set; }

    public HttpMethod DefaultMethod { get; set; } = HttpMethod.Post;
}
