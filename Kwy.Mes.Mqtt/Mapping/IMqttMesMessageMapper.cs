using Kwy.Communicate.Mqtt;
using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Mqtt.Mapping;

public interface IMqttMesMessageMapper
{
    MqttMessage CreateGetWorkOrderRequest(string correlationId, string workOrderNo);

    MqttMessage CreateCheckRouteRequest(string correlationId, MesUnit unit, MesStation station);

    MqttMessage CreateGetRecipeRequest(string correlationId, string recipeName);

    MqttMessage CreateUploadTestResultRequest(string correlationId, MesTestResult result);

    MqttMessage CreateUploadTraceRequest(string correlationId, MesTraceRecord record);

    MesResult<T> ReadResult<T>(MqttMessage message);

    MesResult ReadResult(MqttMessage message);

    string? TryReadCorrelationId(MqttMessage message);
}
