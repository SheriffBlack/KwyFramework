using Kwy.Device.Abstractions.Vision;
using Kwy.Device.Core.Vision;
using Kwy.Device.Cameras.HikVision;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.Device.Camera.Tests;

public sealed class CameraTests
{
    [Fact]
    public void CameraConfig_RequiresStableSelectorAndValidAcquisitionSettings()
    {
        var config = new CameraConfig
        {
            DeviceId = "Camera.Top",
            DeviceName = "Top camera",
            SerialNumber = "SN001"
        };

        Assert.True(config.Validate());

        config.SerialNumber = null;
        Assert.False(config.Validate());
    }

    [Fact]
    public void CameraRegistry_ResolvesCameraAndOptionalCapability()
    {
        var camera = new FakeCamera("Camera.Top");
        var registry = new CameraRegistry(new[] { camera });

        Assert.Same(camera, registry.GetRequired("camera.top"));
        Assert.Same(camera, registry.GetRequiredCapability<IFrameSource>("Camera.Top"));
        Assert.Throws<NotSupportedException>(() =>
            registry.GetRequiredCapability<ISoftwareTriggerCamera>("Camera.Top"));
    }

    [Fact]
    public void CameraRegistry_RejectsDuplicateDeviceIds()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CameraRegistry(new[] { new FakeCamera("Camera.Top"), new FakeCamera("camera.top") }));
    }

    [Fact]
    public void HikVisionRegistration_SupportsMultipleIndependentCameras()
    {
        var services = new ServiceCollection();
        services.AddKwyHikVisionCamera(config =>
        {
            config.DeviceId = "Camera.Top";
            config.DeviceName = "Top";
            config.SerialNumber = "TOP-SN";
        });
        services.AddKwyHikVisionCamera(config =>
        {
            config.DeviceId = "Camera.Bottom";
            config.DeviceName = "Bottom";
            config.SerialNumber = "BOTTOM-SN";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        ICameraRegistry registry = provider.GetRequiredService<ICameraRegistry>();

        Assert.Equal(2, registry.Cameras.Count);
        Assert.IsType<HikCameraDevice>(registry.GetRequired("Camera.Top"));
        Assert.IsType<HikCameraDevice>(registry.GetRequired("Camera.Bottom"));
    }

    [Fact]
    public async Task CameraBase_StartIsSingleFlight_AndWaitReceivesManagedFrame()
    {
        await using var camera = new FakeCamera("Camera.Top");
        await camera.ConnectAsync();

        await Task.WhenAll(camera.StartGrabbingAsync(), camera.StartGrabbingAsync());
        Task<CameraFrame> pendingFrame = camera.WaitForNextFrameAsync(TimeSpan.FromSeconds(1));
        camera.Publish(new CameraFrame(new byte[] { 1, 2, 3 }, 1, 1, 7, "Mono8"));

        CameraFrame frame = await pendingFrame;
        Assert.Equal(1, camera.StartCount);
        Assert.Equal(7, frame.FrameNumber);
        Assert.Equal(new byte[] { 1, 2, 3 }, frame.PixelData.ToArray());

        await camera.StopGrabbingAsync();
        Assert.Equal(1, camera.StopCount);
    }

    private sealed class FakeCamera : CameraBase
    {
        private bool connected;

        public FakeCamera(string deviceId)
            : base(deviceId, deviceId, new CameraConfig
            {
                DeviceId = deviceId,
                DeviceName = deviceId,
                SerialNumber = "TEST"
            })
        {
        }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public override string DeviceModel => "FAKE";

        public void Publish(CameraFrame frame) => RaiseFrameArrived(frame);

        protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            connected = true;
            return Task.CompletedTask;
        }

        protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            await StopGrabbingAsync(cancellationToken);
            connected = false;
        }

        protected override bool IsConnectionAlive() => connected;

        protected override Task StartGrabbingCoreAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        protected override Task StopGrabbingCoreAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }
}
