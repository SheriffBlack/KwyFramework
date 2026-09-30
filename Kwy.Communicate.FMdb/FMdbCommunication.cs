using FluentModbus;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Core;
using Kwy.Communicate.FMdb.Enums;
using System.IO.Ports;
using System.Runtime.CompilerServices;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// FluentModbus 5.3.2 client wrapper integrated with the Kwy communication lifecycle.
/// </summary>
public sealed class FMdbCommunication : CommunicationClientBase, ICommunicationFMdb
{
    private const int MaxAddress = ushort.MaxValue;
    private const int MaxReadBits = 2000;
    private const int MaxReadRegisters = 125;
    private const int MaxWriteCoils = 1968;
    private const int MaxWriteRegisters = 123;
    private const int MaxReadWriteRegisters = 121;

    private readonly MdbConfig modbusConfig;
    private readonly SemaphoreSlim requestSemaphore = new(1, 1);
    private ModbusTcpClient? tcpClient;
    private ModbusRtuClient? rtuClient;

    private ModbusClient? Client => (ModbusClient?)tcpClient ?? rtuClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="FMdbCommunication"/> class.
    /// </summary>
    public FMdbCommunication(MdbConfig config) : this(new ConfigSnapshot(CreateSnapshot(config)))
    {
    }

    private FMdbCommunication(ConfigSnapshot snapshot) : base(snapshot.Value)
        => modbusConfig = snapshot.Value;

    /// <inheritdoc />
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await requestSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DisposeClients();

            if (modbusConfig.Transport == MdbTransport.Tcp)
            {
                var client = new ModbusTcpClient
                {
                    ConnectTimeout = modbusConfig.Timeout,
                    ReadTimeout = modbusConfig.ReadTimeout,
                    WriteTimeout = modbusConfig.WriteTimeout
                };

                await RunBlockingConnectAsync(
                    () => client.Connect(BuildTcpEndpoint(), ToFluentEndianness(modbusConfig.ByteOrder)),
                    client,
                    cancellationToken).ConfigureAwait(false);
                tcpClient = client;
            }
            else
            {
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
                if (!Array.Exists(availablePorts, port => string.Equals(port, modbusConfig.SerialPort, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new System.IO.IOException($"Serial port '{modbusConfig.SerialPort}' does not exist on this machine. Available ports: {string.Join(", ", availablePorts)}");
                }

                var client = new ModbusRtuClient
                {
                    BaudRate = modbusConfig.BaudRate,
                    Parity = ToSerialParity(modbusConfig.Parity),
                    StopBits = ToSerialStopBits(modbusConfig.StopBits),
                    Handshake = ToSerialHandshake(modbusConfig.Handshake),
                    ReadTimeout = modbusConfig.ReadTimeout,
                    WriteTimeout = modbusConfig.WriteTimeout
                };

                await RunBlockingConnectAsync(
                    () => client.Connect(modbusConfig.SerialPort, ToFluentEndianness(modbusConfig.ByteOrder)),
                    client,
                    cancellationToken).ConfigureAwait(false);
                rtuClient = client;
            }
        }
        finally
        {
            requestSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await requestSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DisposeClients();
        }
        finally
        {
            requestSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override bool IsConnectionAlive()
        => tcpClient?.IsConnected == true || rtuClient?.IsConnected == true;

    /// <inheritdoc />
    public Task<bool[]> ReadCoilsAsync(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateRange(startingAddress, count, MaxReadBits, nameof(count));
        return ExecuteRequestAsync(async client =>
        {
            var packed = (await client.ReadCoilsAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, count, cancellationToken)).ToArray();
            var result = DecodeBits(packed, count);
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool[]> ReadDiscreteInputsAsync(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateRange(startingAddress, count, MaxReadBits, nameof(count));
        return ExecuteRequestAsync(async client =>
        {
            var packed = (await client.ReadDiscreteInputsAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, count, cancellationToken)).ToArray();
            var result = DecodeBits(packed, count);
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T[]> ReadHoldingRegistersAsync<T>(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ValidateTypedRange<T>(startingAddress, count, MaxReadRegisters, nameof(count));
        return ExecuteRequestAsync(async client =>
        {
            var result = (await client.ReadHoldingRegistersAsync<T>(ResolveUnitIdentifier(unitIdentifier), startingAddress, count, cancellationToken)).ToArray();
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T[]> ReadInputRegistersAsync<T>(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ValidateTypedRange<T>(startingAddress, count, MaxReadRegisters, nameof(count));
        return ExecuteRequestAsync(async client =>
        {
            var result = (await client.ReadInputRegistersAsync<T>(ResolveUnitIdentifier(unitIdentifier), startingAddress, count, cancellationToken)).ToArray();
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadHoldingRegistersRawAsync(ushort startingAddress, ushort quantity, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateRange(startingAddress, quantity, MaxReadRegisters, nameof(quantity));
        return ExecuteRequestAsync(async client =>
        {
            var result = (await client.ReadHoldingRegistersAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, quantity, cancellationToken)).ToArray();
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadInputRegistersRawAsync(ushort startingAddress, ushort quantity, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateRange(startingAddress, quantity, MaxReadRegisters, nameof(quantity));
        return ExecuteRequestAsync(async client =>
        {
            var result = (await client.ReadInputRegistersAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, quantity, cancellationToken)).ToArray();
            return result;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteSingleCoilAsync(int registerAddress, bool value, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateAddress(registerAddress);
        return ExecuteRequestAsync(
            client => client.WriteSingleCoilAsync(ResolveUnitIdentifier(unitIdentifier), registerAddress, value, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteMultipleCoilsAsync(int startingAddress, bool[] values, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateValues(values);
        ValidateRange(startingAddress, values.Length, MaxWriteCoils, nameof(values));
        return ExecuteRequestAsync(
            client => client.WriteMultipleCoilsAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, values, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteSingleRegisterAsync(int registerAddress, ushort value, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateAddress(registerAddress);
        return ExecuteRequestAsync(
            client => client.WriteSingleRegisterAsync(ResolveUnitIdentifier(unitIdentifier), registerAddress, value, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteSingleRegisterAsync(int registerAddress, short value, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateAddress(registerAddress);
        return ExecuteRequestAsync(
            client => client.WriteSingleRegisterAsync(ResolveUnitIdentifier(unitIdentifier), registerAddress, value, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteMultipleRegistersAsync<T>(int startingAddress, T[] values, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ValidateValues(values);
        ValidateTypedRange<T>(startingAddress, values.Length, MaxWriteRegisters, nameof(values));
        return ExecuteRequestAsync(
            client => client.WriteMultipleRegistersAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, values, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task WriteMultipleRegistersRawAsync(ushort startingAddress, byte[] dataset, byte? unitIdentifier = null, CancellationToken cancellationToken = default)
    {
        ValidateRawDataset(dataset);
        ValidateRange(startingAddress, dataset.Length / 2, MaxWriteRegisters, nameof(dataset));
        return ExecuteRequestAsync(
            client => client.WriteMultipleRegistersAsync(ResolveUnitIdentifier(unitIdentifier), startingAddress, dataset, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TRead[]> ReadWriteMultipleRegistersAsync<TRead, TWrite>(
        int readStartingAddress,
        int readCount,
        int writeStartingAddress,
        TWrite[] values,
        byte? unitIdentifier = null,
        CancellationToken cancellationToken = default)
        where TRead : unmanaged
        where TWrite : unmanaged
    {
        ValidateTypedRange<TRead>(readStartingAddress, readCount, MaxReadRegisters, nameof(readCount));
        ValidateValues(values);
        ValidateTypedRange<TWrite>(writeStartingAddress, values.Length, MaxReadWriteRegisters, nameof(values));

        return ExecuteRequestAsync(async client =>
        {
            var result = (await client.ReadWriteMultipleRegistersAsync<TRead, TWrite>(
                ResolveUnitIdentifier(unitIdentifier),
                readStartingAddress,
                readCount,
                writeStartingAddress,
                values,
                cancellationToken)).ToArray();
            return result;
        }, cancellationToken);
    }

    private async Task<T> ExecuteRequestAsync<T>(Func<ModbusClient, Task<T>> request, CancellationToken cancellationToken)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(FMdbCommunication));

        if (!IsConnected)
            throw new InvalidOperationException("FluentModbus client is not connected.");

        SemaphoreSlim semaphore = requestSemaphore;
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var client = Client ?? throw new InvalidOperationException("FluentModbus client is not initialized.");
            if (!IsConnected)
                throw new InvalidOperationException("FluentModbus client disconnected before the request could start.");
            return await request(client);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Trigger reconnection on any transport/communication failure (not standard protocol exceptions) or if connection is dead
            if (ex is not ModbusException || !IsConnectionAlive())
                _ = HandleCommunicationFailureAsync(ex, $"FluentModbus request failed: {ex.Message}");

            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private Task ExecuteRequestAsync(Func<ModbusClient, Task> request, CancellationToken cancellationToken)
        => ExecuteRequestAsync(async client =>
        {
            await request(client);
            return true;
        }, cancellationToken);

    private byte ResolveUnitIdentifier(byte? unitIdentifier)
        => unitIdentifier ?? modbusConfig.UnitIdentifier;

    private string BuildTcpEndpoint()
    {
        var host = modbusConfig.Host.Trim();
        if (host.Contains(':') && !host.StartsWith('['))
            host = $"[{host}]";

        return $"{host}:{modbusConfig.Port}";
    }

    private void DisposeClients()
    {
        var tcp = tcpClient;
        var rtu = rtuClient;

        tcpClient = null;
        rtuClient = null;

        if (tcp != null)
        {
            try
            {
                tcp.Disconnect();
            }
            catch
            {
            }
            try
            {
                tcp.Dispose();
            }
            catch
            {
            }
        }

        if (rtu != null)
        {
            try
            {
                // Directly disposing the ModbusRtuClient closes the underlying SerialPort.
                // This is safer than calling Close() which can hang on faulty or unplugged USB-serial drivers.
                rtu.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task RunBlockingConnectAsync(Action connect, IDisposable client, CancellationToken cancellationToken)
    {
        Task connectTask = Task.Run(connect, CancellationToken.None);
        try
        {
            await connectTask.WaitAsync(
                TimeSpan.FromMilliseconds(modbusConfig.Timeout),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (connectTask.IsCompleted)
            {
                client.Dispose();
            }
            else
            {
                _ = connectTask.ContinueWith(
                    static (completedTask, state) =>
                    {
                        _ = completedTask.Exception;
                        ((IDisposable)state!).Dispose();
                    },
                    client,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            throw;
        }
    }

    private static bool[] DecodeBits(byte[] packedValues, int count)
    {
        var values = new bool[count];
        for (var index = 0; index < count; index++)
            values[index] = (packedValues[index / 8] & (1 << (index % 8))) != 0;

        return values;
    }

    private static ModbusEndianness ToFluentEndianness(MdbByteOrder byteOrder)
        => byteOrder == MdbByteOrder.BigEndian ? ModbusEndianness.BigEndian : ModbusEndianness.LittleEndian;

    private static Parity ToSerialParity(ParityType parity)
        => parity switch
        {
            ParityType.Odd => Parity.Odd,
            ParityType.Even => Parity.Even,
            ParityType.Mark => Parity.Mark,
            ParityType.Space => Parity.Space,
            _ => Parity.None
        };

    private static StopBits ToSerialStopBits(StopBitsType stopBits)
        => stopBits switch
        {
            StopBitsType.None => StopBits.None,
            StopBitsType.Two => StopBits.Two,
            StopBitsType.OnePointFive => StopBits.OnePointFive,
            _ => StopBits.One
        };

    private static Handshake ToSerialHandshake(HandshakeType handshake)
        => handshake switch
        {
            HandshakeType.XOnXOff => Handshake.XOnXOff,
            HandshakeType.RequestToSend => Handshake.RequestToSend,
            HandshakeType.RequestToSendXOnXOff => Handshake.RequestToSendXOnXOff,
            _ => Handshake.None
        };

    private static void ValidateAddress(int address)
    {
        if (address is < 0 or > MaxAddress)
            throw new ArgumentOutOfRangeException(nameof(address), $"Modbus address must be between 0 and {MaxAddress}.");
    }

    private static void ValidateRange(int startingAddress, int count, int maximumCount, string countParameterName)
    {
        ValidateAddress(startingAddress);
        if (count is <= 0 || count > maximumCount)
            throw new ArgumentOutOfRangeException(countParameterName, $"Modbus quantity must be between 1 and {maximumCount}.");
        if ((long)startingAddress + count > MaxAddress + 1L)
            throw new ArgumentOutOfRangeException(countParameterName, "The requested Modbus range exceeds address 65535.");
    }

    private static void ValidateTypedRange<T>(int startingAddress, int valueCount, int maximumRegisters, string countParameterName)
        where T : unmanaged
    {
        if (valueCount <= 0)
            throw new ArgumentOutOfRangeException(countParameterName, "Value count must be greater than zero.");

        int size = Unsafe.SizeOf<T>();
        long byteCount = (long)size * valueCount;
        if ((byteCount & 1) != 0)
            throw new ArgumentException($"Type '{typeof(T).Name}' does not occupy a whole number of Modbus registers.", countParameterName);
        if (byteCount / 2 > int.MaxValue)
            throw new ArgumentOutOfRangeException(countParameterName);

        ValidateRange(startingAddress, (int)(byteCount / 2), maximumRegisters, countParameterName);
    }

    private static void ValidateValues<T>(T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("At least one value is required.", nameof(values));
    }

    private static void ValidateRawDataset(byte[] dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (dataset.Length < 2 || dataset.Length % 2 != 0)
            throw new ArgumentException("A raw register dataset must contain an even number of bytes and at least one register.", nameof(dataset));
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await base.DisposeAsync();
        if (requestSemaphore.Wait(0))
            requestSemaphore.Dispose();
    }

    private static MdbConfig CreateSnapshot(MdbConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Validate())
            throw new ArgumentException("FluentModbus configuration is invalid.", nameof(config));
        return config.Snapshot();
    }

    private sealed record ConfigSnapshot(MdbConfig Value);
}
