using System.Text;
using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using Kwy.Device.Core;

namespace KwyTemplate.Device.MarkPrinters;

public sealed class TcpMarkPrintDevice : DeviceBase, IMarkPrintDevice
{
    private const string CommandPrefix = "PRINT ";
    // The printer protocol returns the literal token SUCCESS on an accepted print string.
    // Only that token is success; FAIL and every other response must remain failures.
    private const string SuccessResponse = "SUCCESS";
    private const int ReadBufferSize = 1024;

    private readonly ICommunicationFactory communicationFactory;
    private readonly SemaphoreSlim operationSemaphore = new(1, 1);
    private IByteTransport? transport;

    public TcpMarkPrintDevice(
        string deviceId,
        string deviceName,
        MarkPrintConfig config,
        ICommunicationFactory communicationFactory)
        : base(deviceId, deviceName, config)
    {
        this.communicationFactory = communicationFactory ?? throw new ArgumentNullException(nameof(communicationFactory));
    }

    public override string DeviceModel => "TCP Mark Printer";

    private MarkPrintConfig Config => (MarkPrintConfig)DeviceParameter;

    public async Task SetPrintStringAsync(string printString, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        string payload = NormalizePayload(printString);
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Mark print string is empty.");
        }

        await operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (transport is not { IsConnected: true })
            {
                throw new InvalidOperationException($"{DeviceName} is not connected.");
            }

            string command = CommandPrefix + payload;
            await transport.WriteAsync(Encoding.ASCII.GetBytes(command), cancellationToken).ConfigureAwait(false);

            byte[] buffer = new byte[ReadBufferSize];
            int length = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            string response = Encoding.ASCII.GetString(buffer, 0, length).Trim();
            if (!response.Equals(SuccessResponse, StringComparison.OrdinalIgnoreCase))
            {
                string message = response.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)
                    ? $"{DeviceName} set print string failed: {response}"
                    : $"{DeviceName} set print string failed. Response={response}";
                var exception = new InvalidOperationException(message);
                RaiseOperationOccurred(DeviceOperationKind.ParameterWrite, "SetPrintString", false, message, exception);
                throw exception;
            }

            RaiseOperationOccurred(DeviceOperationKind.ParameterWrite, "SetPrintString", true, $"{DeviceName} set print string succeeded.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is not InvalidOperationException)
            {
                RaiseOperationOccurred(DeviceOperationKind.ParameterWrite, "SetPrintString", false, $"{DeviceName} set print string failed: {ex.Message}", ex);
            }

            throw;
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        if (!Config.Validate())
        {
            throw new InvalidOperationException($"{DeviceName} mark printer configuration is invalid.");
        }

        ICommunicationClient client = communicationFactory.CreateClient(Config.Tcp);
        if (client is not IByteTransport byteTransport)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Configured mark printer communication client does not support byte transport.");
        }

        transport = byteTransport;
        await byteTransport.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        IByteTransport? current = transport;
        transport = null;
        if (current == null)
        {
            return;
        }

        await current.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await current.DisposeAsync().ConfigureAwait(false);
    }

    protected override bool IsConnectionAlive()
        => transport is { IsConnected: true };

    private static string NormalizePayload(string value)
        => string.Join(' ', value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
