namespace Kwy.Mes.Mqtt;

public sealed class MqttMesOptions
{
    public string? WorkOrderRequestTopic { get; set; }

    public string? WorkOrderResponseTopic { get; set; }

    public string? RouteCheckRequestTopic { get; set; }

    public string? RouteCheckResponseTopic { get; set; }

    public string? RecipeRequestTopic { get; set; }

    public string? RecipeResponseTopic { get; set; }

    public string? TestResultTopic { get; set; }

    public string? TestResultResponseTopic { get; set; }

    public string? TraceTopic { get; set; }

    public string? TraceResponseTopic { get; set; }

    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public byte QualityOfServiceLevel { get; set; } = 1;

    public bool Retain { get; set; }
}
