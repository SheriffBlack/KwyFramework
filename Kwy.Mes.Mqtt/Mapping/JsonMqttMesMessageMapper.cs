using System.Text;
using System.Text.Json;
using Kwy.Communicate.Mqtt;
using Kwy.Files;
using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Mqtt.Mapping;

public sealed class JsonMqttMesMessageMapper : IMqttMesMessageMapper
{
    private readonly MqttMesOptions options;

    public JsonMqttMesMessageMapper(MqttMesOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public MqttMessage CreateGetWorkOrderRequest(string correlationId, string workOrderNo)
        => CreateJsonMessage(options.WorkOrderRequestTopic, correlationId, new { WorkOrderNo = workOrderNo }, nameof(CreateGetWorkOrderRequest));

    public MqttMessage CreateCheckRouteRequest(string correlationId, MesUnit unit, MesStation station)
        => CreateJsonMessage(options.RouteCheckRequestTopic, correlationId, new { Unit = unit, Station = station }, nameof(CreateCheckRouteRequest));

    public MqttMessage CreateGetRecipeRequest(string correlationId, string recipeName)
        => CreateJsonMessage(options.RecipeRequestTopic, correlationId, new { RecipeName = recipeName }, nameof(CreateGetRecipeRequest));

    public MqttMessage CreateUploadTestResultRequest(string correlationId, MesTestResult result)
        => CreateJsonMessage(options.TestResultTopic, correlationId, result, nameof(CreateUploadTestResultRequest));

    public MqttMessage CreateUploadTraceRequest(string correlationId, MesTraceRecord record)
        => CreateJsonMessage(options.TraceTopic, correlationId, record, nameof(CreateUploadTraceRequest));

    public MesResult<T> ReadResult<T>(MqttMessage message)
    {
        var json = Encoding.UTF8.GetString(message.Payload.Span);
        return JsonHelper.Deserialize<MesResult<T>>(json, JsonHelper.WebOptions) ?? MesResult<T>.Fail("EMPTY_RESPONSE", "MES returned an empty MQTT response.");
    }

    public MesResult ReadResult(MqttMessage message)
    {
        var json = Encoding.UTF8.GetString(message.Payload.Span);
        return JsonHelper.Deserialize<MesResult>(json, JsonHelper.WebOptions) ?? MesResult.Ok();
    }

    public string? TryReadCorrelationId(MqttMessage message)
    {
        try
        {
            using var document = JsonDocument.Parse(message.Payload);
            if (document.RootElement.TryGetProperty("correlationId", out var camelCase))
            {
                return camelCase.GetString();
            }

            if (document.RootElement.TryGetProperty("CorrelationId", out var pascalCase))
            {
                return pascalCase.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private MqttMessage CreateJsonMessage<T>(string? topic, string correlationId, T payload, string operation)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException($"MQTT MES topic is not configured for {operation}.");
        }

        var envelope = new MqttMesEnvelope<T>(correlationId, payload);
        var bytes = Encoding.UTF8.GetBytes(JsonHelper.Serialize(envelope, JsonHelper.WebOptions));
        return new MqttMessage(topic, bytes, options.QualityOfServiceLevel, options.Retain);
    }

    private sealed record MqttMesEnvelope<T>(string CorrelationId, T Payload);
}
