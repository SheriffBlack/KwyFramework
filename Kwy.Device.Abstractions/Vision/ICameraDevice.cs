using System.Buffers;

namespace Kwy.Device.Abstractions.Vision;

/// <summary>
/// A managed camera frame whose pixel memory is independent from the vendor SDK buffer.
/// Frame event subscribers may retain the frame after the callback by calling <see cref="Retain"/>.
/// </summary>
public sealed class CameraFrame : IDisposable
{
    private readonly IDisposable? owner;
    private int referenceCount = 1;
    private int disposed;

    public CameraFrame(
        ReadOnlyMemory<byte> pixelData,
        int width,
        int height,
        long frameNumber,
        string pixelFormat = "",
        int stride = 0,
        DateTimeOffset timestamp = default)
        : this(pixelData, width, height, frameNumber, pixelFormat, stride, timestamp, null)
    {
    }

    private CameraFrame(
        ReadOnlyMemory<byte> pixelData,
        int width,
        int height,
        long frameNumber,
        string pixelFormat,
        int stride,
        DateTimeOffset timestamp,
        IDisposable? owner)
    {
        PixelData = pixelData;
        Width = width;
        Height = height;
        FrameNumber = frameNumber;
        PixelFormat = pixelFormat;
        Stride = stride;
        Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
        this.owner = owner;
    }

    public ReadOnlyMemory<byte> PixelData { get; }

    public int Width { get; }

    public int Height { get; }

    public long FrameNumber { get; }

    public string PixelFormat { get; }

    public int Stride { get; }

    public DateTimeOffset Timestamp { get; }

    public CameraFrame Retain()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(CameraFrame));
        }

        Interlocked.Increment(ref referenceCount);
        if (Volatile.Read(ref disposed) != 0)
        {
            Dispose();
            throw new ObjectDisposedException(nameof(CameraFrame));
        }

        return this;
    }

    public static CameraFrame CreatePooledCopy(
        ReadOnlySpan<byte> pixelData,
        int width,
        int height,
        long frameNumber,
        string pixelFormat = "",
        int stride = 0,
        DateTimeOffset timestamp = default)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(pixelData.Length);
        pixelData.CopyTo(buffer);
        return new CameraFrame(
            new ReadOnlyMemory<byte>(buffer, 0, pixelData.Length),
            width,
            height,
            frameNumber,
            pixelFormat,
            stride,
            timestamp,
            new PooledBufferOwner(buffer));
    }

    public void Dispose()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        int remaining = Interlocked.Decrement(ref referenceCount);
        if (remaining <= 0 && Interlocked.Exchange(ref disposed, 1) == 0)
        {
            owner?.Dispose();
        }
    }

    private sealed class PooledBufferOwner : IDisposable
    {
        private byte[]? buffer;

        public PooledBufferOwner(byte[] buffer)
        {
            this.buffer = buffer;
        }

        public void Dispose()
        {
            byte[]? rented = Interlocked.Exchange(ref buffer, null);
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}

/// <summary>Basic camera identity, lifecycle, and configuration capability.</summary>
public interface ICameraDevice : IDevice, IConfigurableDevice
{
}

/// <summary>Continuous or triggered frame-stream capability.</summary>
public interface IFrameSource
{
    event EventHandler<CameraFrame>? FrameArrived;

    bool IsGrabbing { get; }

    Task StartGrabbingAsync(CancellationToken cancellationToken = default);

    Task StopGrabbingAsync(CancellationToken cancellationToken = default);

    Task<CameraFrame> WaitForNextFrameAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Software-trigger capability. Not every camera or acquisition mode supports it.</summary>
public interface ISoftwareTriggerCamera
{
    Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken = default);
}

/// <summary>Common camera parameter capability expressed in vendor-independent units.</summary>
public interface ICameraParameterController
{
    Task SetExposureTimeAsync(double exposureTimeUs, CancellationToken cancellationToken = default);

    Task SetGainAsync(double gain, CancellationToken cancellationToken = default);
}

/// <summary>Resolves cameras by stable application device ID.</summary>
public interface ICameraRegistry
{
    IReadOnlyCollection<ICameraDevice> Cameras { get; }

    ICameraDevice GetRequired(string deviceId);

    TCapability GetRequiredCapability<TCapability>(string deviceId) where TCapability : class;
}
