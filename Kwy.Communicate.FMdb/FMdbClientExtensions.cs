namespace Kwy.Communicate.FMdb;

/// <summary>
/// Optional synchronous helpers for callers that cannot use async APIs yet.
/// </summary>
public static class FMdbClientExtensions
{
    /// <inheritdoc cref="ICommunicationFMdb.ReadCoilsAsync(int, int, byte?, CancellationToken)" />
    public static bool[] ReadCoils(this ICommunicationFMdb client, int startingAddress, int count, byte? unitIdentifier = null)
        => client.ReadCoilsAsync(startingAddress, count, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadDiscreteInputsAsync(int, int, byte?, CancellationToken)" />
    public static bool[] ReadDiscreteInputs(this ICommunicationFMdb client, int startingAddress, int count, byte? unitIdentifier = null)
        => client.ReadDiscreteInputsAsync(startingAddress, count, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadHoldingRegistersAsync{T}(int, int, byte?, CancellationToken)" />
    public static T[] ReadHoldingRegisters<T>(this ICommunicationFMdb client, int startingAddress, int count, byte? unitIdentifier = null) where T : unmanaged
        => client.ReadHoldingRegistersAsync<T>(startingAddress, count, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadInputRegistersAsync{T}(int, int, byte?, CancellationToken)" />
    public static T[] ReadInputRegisters<T>(this ICommunicationFMdb client, int startingAddress, int count, byte? unitIdentifier = null) where T : unmanaged
        => client.ReadInputRegistersAsync<T>(startingAddress, count, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadHoldingRegistersRawAsync(ushort, ushort, byte?, CancellationToken)" />
    public static byte[] ReadHoldingRegistersRaw(this ICommunicationFMdb client, ushort startingAddress, ushort quantity, byte? unitIdentifier = null)
        => client.ReadHoldingRegistersRawAsync(startingAddress, quantity, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadInputRegistersRawAsync(ushort, ushort, byte?, CancellationToken)" />
    public static byte[] ReadInputRegistersRaw(this ICommunicationFMdb client, ushort startingAddress, ushort quantity, byte? unitIdentifier = null)
        => client.ReadInputRegistersRawAsync(startingAddress, quantity, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteSingleCoilAsync(int, bool, byte?, CancellationToken)" />
    public static void WriteSingleCoil(this ICommunicationFMdb client, int registerAddress, bool value, byte? unitIdentifier = null)
        => client.WriteSingleCoilAsync(registerAddress, value, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteMultipleCoilsAsync(int, bool[], byte?, CancellationToken)" />
    public static void WriteMultipleCoils(this ICommunicationFMdb client, int startingAddress, bool[] values, byte? unitIdentifier = null)
        => client.WriteMultipleCoilsAsync(startingAddress, values, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteSingleRegisterAsync(int, ushort, byte?, CancellationToken)" />
    public static void WriteSingleRegister(this ICommunicationFMdb client, int registerAddress, ushort value, byte? unitIdentifier = null)
        => client.WriteSingleRegisterAsync(registerAddress, value, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteSingleRegisterAsync(int, short, byte?, CancellationToken)" />
    public static void WriteSingleRegister(this ICommunicationFMdb client, int registerAddress, short value, byte? unitIdentifier = null)
        => client.WriteSingleRegisterAsync(registerAddress, value, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteMultipleRegistersAsync{T}(int, T[], byte?, CancellationToken)" />
    public static void WriteMultipleRegisters<T>(this ICommunicationFMdb client, int startingAddress, T[] values, byte? unitIdentifier = null) where T : unmanaged
        => client.WriteMultipleRegistersAsync(startingAddress, values, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.WriteMultipleRegistersRawAsync(ushort, byte[], byte?, CancellationToken)" />
    public static void WriteMultipleRegistersRaw(this ICommunicationFMdb client, ushort startingAddress, byte[] dataset, byte? unitIdentifier = null)
        => client.WriteMultipleRegistersRawAsync(startingAddress, dataset, unitIdentifier).GetAwaiter().GetResult();

    /// <inheritdoc cref="ICommunicationFMdb.ReadWriteMultipleRegistersAsync{TRead, TWrite}(int, int, int, TWrite[], byte?, CancellationToken)" />
    public static TRead[] ReadWriteMultipleRegisters<TRead, TWrite>(
        this ICommunicationFMdb client,
        int readStartingAddress,
        int readCount,
        int writeStartingAddress,
        TWrite[] values,
        byte? unitIdentifier = null)
        where TRead : unmanaged
        where TWrite : unmanaged
        => client.ReadWriteMultipleRegistersAsync<TRead, TWrite>(
            readStartingAddress,
            readCount,
            writeStartingAddress,
            values,
            unitIdentifier).GetAwaiter().GetResult();
}
