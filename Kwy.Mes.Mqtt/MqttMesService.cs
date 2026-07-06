using System.Collections.Concurrent;
using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.Mqtt;
using Kwy.Mes.Abstractions.Models;
using Kwy.Mes.Core;
using Kwy.Mes.Mqtt.Mapping;

namespace Kwy.Mes.Mqtt;

public sealed class MqttMesService : MesServiceBase
{
    private readonly IMessageClient<MqttMessage> client;
    private readonly MqttMesOptions options;
    private readonly IMqttMesMessageMapper mapper;
    private readonly ConcurrentDictionary<string, PendingResponse> pendingResponses = new();

    public MqttMesService(
        IMessageClient<MqttMessage> client,
        MqttMesOptions options,
        IMqttMesMessageMapper mapper)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        this.client.MessageReceived += OnMessageReceived;
    }

    protected override async Task<MesResult> ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

        if (client is IMqttCommunication mqtt)
        {
            var topics = GetResponseTopics().ToArray();
            if (topics.Length > 0)
            {
                await mqtt.SubscribeAsync(topics, cancellationToken).ConfigureAwait(false);
            }
        }

        return MesResult.Ok();
    }

    protected override async Task<MesResult> DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var pending in pendingResponses.Values)
        {
            pending.TrySetCanceled();
        }

        pendingResponses.Clear();
        await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        return MesResult.Ok();
    }

    public override Task<MesResult<MesWorkOrder>> GetWorkOrderAsync(string workOrderNo, CancellationToken cancellationToken = default)
        => SendAndWaitAsync<MesWorkOrder>(
            correlationId => mapper.CreateGetWorkOrderRequest(correlationId, workOrderNo),
            options.WorkOrderResponseTopic,
            nameof(GetWorkOrderAsync),
            cancellationToken);

    public override Task<MesResult<MesRouteCheckResult>> CheckRouteAsync(MesUnit unit, MesStation station, CancellationToken cancellationToken = default)
        => SendAndWaitAsync<MesRouteCheckResult>(
            correlationId => mapper.CreateCheckRouteRequest(correlationId, unit, station),
            options.RouteCheckResponseTopic,
            nameof(CheckRouteAsync),
            cancellationToken);

    public override Task<MesResult<MesRecipe>> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default)
        => SendAndWaitAsync<MesRecipe>(
            correlationId => mapper.CreateGetRecipeRequest(correlationId, recipeName),
            options.RecipeResponseTopic,
            nameof(GetRecipeAsync),
            cancellationToken);

    public override Task<MesResult> UploadTestResultAsync(MesTestResult result, CancellationToken cancellationToken = default)
        => PublishOrWaitAsync(
            correlationId => mapper.CreateUploadTestResultRequest(correlationId, result),
            options.TestResultResponseTopic,
            nameof(UploadTestResultAsync),
            cancellationToken);

    public override Task<MesResult> UploadTraceAsync(MesTraceRecord record, CancellationToken cancellationToken = default)
        => PublishOrWaitAsync(
            correlationId => mapper.CreateUploadTraceRequest(correlationId, record),
            options.TraceResponseTopic,
            nameof(UploadTraceAsync),
            cancellationToken);

    private async Task<MesResult<T>> SendAndWaitAsync<T>(
        Func<string, MqttMessage> requestFactory,
        string? responseTopic,
        string operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseTopic))
        {
            return MesResult<T>.Unsupported($"{operation} without response topic");
        }

        var message = await PublishAndWaitMessageAsync(requestFactory, responseTopic, cancellationToken).ConfigureAwait(false);
        return mapper.ReadResult<T>(message);
    }

    private async Task<MesResult> PublishOrWaitAsync(
        Func<string, MqttMessage> requestFactory,
        string? responseTopic,
        string operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseTopic))
        {
            var message = requestFactory(Guid.NewGuid().ToString("N"));
            await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
            return MesResult.Ok($"{operation} published.");
        }

        var response = await PublishAndWaitMessageAsync(requestFactory, responseTopic, cancellationToken).ConfigureAwait(false);
        return mapper.ReadResult(response);
    }

    private async Task<MqttMessage> PublishAndWaitMessageAsync(
        Func<string, MqttMessage> requestFactory,
        string responseTopic,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var pending = new PendingResponse(responseTopic);
        if (!pendingResponses.TryAdd(correlationId, pending))
        {
            throw new InvalidOperationException($"Duplicate MES MQTT correlation id: {correlationId}.");
        }

        try
        {
            var message = requestFactory(correlationId);
            await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(options.ResponseTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var registration = linked.Token.Register(static state => ((PendingResponse)state!).TrySetCanceled(), pending);
            return await pending.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MES MQTT response timeout. Topic={responseTopic}, Timeout={options.ResponseTimeout}.");
        }
        finally
        {
            pendingResponses.TryRemove(correlationId, out _);
        }
    }

    private IEnumerable<string> GetResponseTopics()
    {
        if (!string.IsNullOrWhiteSpace(options.WorkOrderResponseTopic))
        {
            yield return options.WorkOrderResponseTopic;
        }

        if (!string.IsNullOrWhiteSpace(options.RouteCheckResponseTopic))
        {
            yield return options.RouteCheckResponseTopic;
        }

        if (!string.IsNullOrWhiteSpace(options.RecipeResponseTopic))
        {
            yield return options.RecipeResponseTopic;
        }

        if (!string.IsNullOrWhiteSpace(options.TestResultResponseTopic))
        {
            yield return options.TestResultResponseTopic;
        }

        if (!string.IsNullOrWhiteSpace(options.TraceResponseTopic))
        {
            yield return options.TraceResponseTopic;
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs<MqttMessage> e)
    {
        var correlationId = mapper.TryReadCorrelationId(e.Message);
        if (correlationId is null)
        {
            return;
        }

        if (pendingResponses.TryGetValue(correlationId, out var pending)
            && string.Equals(pending.ResponseTopic, e.Message.Topic, StringComparison.Ordinal))
        {
            pending.TrySetResult(e.Message);
        }
    }

    private sealed class PendingResponse
    {
        private readonly TaskCompletionSource<MqttMessage> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingResponse(string responseTopic)
        {
            ResponseTopic = responseTopic;
        }

        public string ResponseTopic { get; }

        public Task<MqttMessage> Task => completion.Task;

        public void TrySetResult(MqttMessage message) => completion.TrySetResult(message);

        public void TrySetCanceled() => completion.TrySetCanceled();
    }
}
