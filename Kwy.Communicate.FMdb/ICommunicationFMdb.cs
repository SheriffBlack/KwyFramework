using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// Modbus client operations exposed by the FluentModbus wrapper.
/// </summary>
public interface ICommunicationFMdb : ICommunicationClient
{
    /// <summary>
    /// Reads coil values.
    /// </summary>
    Task<bool[]> ReadCoilsAsync(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads discrete input values.
    /// </summary>
    Task<bool[]> ReadDiscreteInputsAsync(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads holding registers and converts them to unmanaged values.
    /// </summary>
    Task<T[]> ReadHoldingRegistersAsync<T>(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads input registers and converts them to unmanaged values.
    /// </summary>
    Task<T[]> ReadInputRegistersAsync<T>(int startingAddress, int count, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads holding registers as raw bytes.
    /// </summary>
    Task<byte[]> ReadHoldingRegistersRawAsync(ushort startingAddress, ushort quantity, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads input registers as raw bytes.
    /// </summary>
    Task<byte[]> ReadInputRegistersRawAsync(ushort startingAddress, ushort quantity, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single coil value.
    /// </summary>
    Task WriteSingleCoilAsync(int registerAddress, bool value, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple coil values.
    /// </summary>
    Task WriteMultipleCoilsAsync(int startingAddress, bool[] values, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single unsigned register value.
    /// </summary>
    Task WriteSingleRegisterAsync(int registerAddress, ushort value, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single signed register value.
    /// </summary>
    Task WriteSingleRegisterAsync(int registerAddress, short value, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple unmanaged register values.
    /// </summary>
    Task WriteMultipleRegistersAsync<T>(int startingAddress, T[] values, byte? unitIdentifier = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Writes raw register bytes. The byte array must contain complete registers.
    /// </summary>
    Task WriteMultipleRegistersRawAsync(ushort startingAddress, byte[] dataset, byte? unitIdentifier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes registers and reads registers in one Modbus request.
    /// </summary>
    Task<TRead[]> ReadWriteMultipleRegistersAsync<TRead, TWrite>(
        int readStartingAddress,
        int readCount,
        int writeStartingAddress,
        TWrite[] values,
        byte? unitIdentifier = null,
        CancellationToken cancellationToken = default)
        where TRead : unmanaged
        where TWrite : unmanaged;
}
