using Ivi.Visa;
using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using NationalInstruments.Visa;
using System.Text;

namespace Kwy.Communicate.Visa;

/// <summary>基于 NI-VISA 的消息型仪器通信客户端，主要用于 GPIB 和 SCPI 仪器。</summary>
public class VisaCommunication : CommunicationBase, ICommandQueryClient
{
    private readonly VisaConfig visaConfig;
    private readonly SemaphoreSlim ioSemaphore = new(1, 1);
    private IMessageBasedSession? session;

    /// <summary>使用经过验证的配置快照初始化通信客户端。</summary>
    public VisaCommunication(VisaConfig config) : this(new ConfigSnapshot(CreateSnapshot(config))) { }

    private VisaCommunication(ConfigSnapshot snapshot) : base(snapshot.Value)
        => visaConfig = snapshot.Value;

    /// <inheritdoc />
    protected override async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        Task<IMessageBasedSession> openTask = Task.Run(OpenSession, CancellationToken.None);
        try
        {
            session = await openTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DisposeLateSession(openTask);
            throw;
        }
    }

    /// <inheritdoc />
    protected override async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        await ioSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IMessageBasedSession? current = session;
            session = null;
            current?.Dispose();
        }
        finally
        {
            ioSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override Task SendInternalAsync(byte[] data, CancellationToken cancellationToken)
        => ExecuteIoAsync(async (current, token) =>
        {
            await current.RawIO.WriteAsync(data, token).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    /// <inheritdoc />
    protected override Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        => ExecuteIoAsync(async (current, token) =>
        {
            IVisaReadResult result = await current.RawIO.ReadAsync(buffer, token).ConfigureAwait(false);
            return checked((int)result.ActualCount);
        }, cancellationToken);

    /// <inheritdoc />
    protected override bool ValidateConnection() => session != null;

    /// <inheritdoc />
    protected override async Task<bool> CheckConnectionAliveAsync(CancellationToken cancellationToken)
    {
        if (!ValidateConnection())
            return false;
        if (string.IsNullOrWhiteSpace(visaConfig.KeepAliveCommand))
            return true;

        try
        {
            _ = await QueryCoreAsync(visaConfig.KeepAliveCommand, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnErrorOccurred(ex, $"NI-VISA keep-alive failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public ValueTask WriteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return WriteAsync(Encoding.ASCII.GetBytes(AppendTerminator(command)), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (!IsConnected)
            throw new InvalidOperationException("NI-VISA session is not connected.");

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        try
        {
            return await QueryCoreAsync(command, linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"NI-VISA query failed: {ex.Message}").ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> QueryCoreAsync(string command, CancellationToken cancellationToken)
    {
        return await ExecuteIoAsync(async (current, token) =>
        {
            byte[] commandBytes = Encoding.ASCII.GetBytes(AppendTerminator(command));
            await current.RawIO.WriteAsync(commandBytes, token).ConfigureAwait(false);

            var response = new byte[visaConfig.QueryBufferSize];
            IVisaReadResult result = await current.RawIO.ReadAsync(response, token).ConfigureAwait(false);
            if (result.ReadStatus == ReadStatus.MaximumCountReached)
            {
                throw new InvalidDataException(
                    $"NI-VISA response exceeded QueryBufferSize ({visaConfig.QueryBufferSize} bytes).");
            }

            return Encoding.ASCII.GetString(response, 0, checked((int)result.ActualCount));
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ExecuteIoAsync<T>(
        Func<IMessageBasedSession, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await ioSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IMessageBasedSession current = session
                ?? throw new InvalidOperationException("NI-VISA session is not open.");
            return await operation(current, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ioSemaphore.Release();
        }
    }

    private IMessageBasedSession OpenSession()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NationalInstruments.Visa requires Windows.");

        using var resourceManager = new ResourceManager();
        IVisaSession opened = resourceManager.Open(visaConfig.ResourceName, AccessModes.None, visaConfig.Timeout);
        if (opened is not IMessageBasedSession messageSession)
        {
            opened.Dispose();
            throw new NotSupportedException(
                $"VISA resource '{visaConfig.ResourceName}' is not a message-based resource.");
        }

        messageSession.TimeoutMilliseconds = visaConfig.Timeout;
        messageSession.SendEndEnabled = visaConfig.SendEndEnabled;
        messageSession.TerminationCharacterEnabled = visaConfig.ReadTerminationEnabled;
        messageSession.TerminationCharacter = visaConfig.ReadTerminationCharacter;
        return messageSession;
    }

    private string AppendTerminator(string command)
        => string.IsNullOrEmpty(visaConfig.WriteTerminator)
            || command.EndsWith(visaConfig.WriteTerminator, StringComparison.Ordinal)
                ? command
                : command + visaConfig.WriteTerminator;

    private static void DisposeLateSession(Task<IMessageBasedSession> openTask)
    {
        if (openTask.IsCompletedSuccessfully)
        {
            openTask.Result.Dispose();
            return;
        }

        _ = openTask.ContinueWith(
            static task =>
            {
                _ = task.Exception;
                if (task.Status == TaskStatus.RanToCompletion)
                    task.Result.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        await base.DisposeAsync().ConfigureAwait(false);
        if (ioSemaphore.Wait(0))
            ioSemaphore.Dispose();
    }

    private static VisaConfig CreateSnapshot(VisaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Validate())
            throw new ArgumentException("NI-VISA configuration is invalid.", nameof(config));
        return config.Snapshot();
    }

    private sealed record ConfigSnapshot(VisaConfig Value);
}
