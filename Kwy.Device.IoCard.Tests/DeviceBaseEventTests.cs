using Kwy.Communicate.Abstractions.Enums;
using Kwy.Device.Abstractions;
using Kwy.Device.Core;
using Xunit;

namespace Kwy.Device.IoCard.Tests;

/// <summary>验证外部 HMI、日志等事件订阅者异常不会中断设备生命周期。</summary>
public sealed class DeviceBaseEventTests
{
    [Fact]
    public async Task ConnectAsync_ExpectedConnectionFailure_IsReportedAsErrorState()
    {
        await using var device = new FailingDevice();
        Exception? reported = null;
        device.ErrorOccurred += (_, args) => reported = args.Exception;

        Exception? thrown = await Record.ExceptionAsync(() => device.ConnectAsync());

        Assert.Null(thrown);
        Assert.False(device.IsConnected);
        Assert.Equal(ConnectionState.Error, device.State);
        Assert.IsType<IOException>(reported);
    }

    [Fact]
    public async Task ConnectAsync_StateSubscriberThrows_ConnectionStillSucceeds()
    {
        await using var device = new TestDevice();
        device.StateChanged += (_, _) => throw new InvalidOperationException("模拟 HMI 回调异常");

        await device.ConnectAsync();

        Assert.True(device.IsConnected);
        Assert.Equal(ConnectionState.Connected, device.State);
    }

    [Fact]
    public void PublishOperation_SubscriberThrows_OperationStillCompletes()
    {
        using var device = new TestDevice();
        device.OperationOccurred += (_, _) => throw new InvalidOperationException("模拟日志回调异常");

        device.PublishOperation();
    }

    private sealed class TestDevice : DeviceBase
    {
        public TestDevice()
            : base("test.device", "测试设备", new TestConfig())
        {
        }

        protected override Task ConnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override bool IsConnectionAlive() => true;

        public void PublishOperation()
            => RaiseOperationOccurred(DeviceOperationKind.Read, "测试操作", true, "测试成功");
    }

    private sealed class TestConfig : IDeviceConfig
    {
        public bool Validate() => true;
    }

    private sealed class FailingDevice : DeviceBase
    {
        public FailingDevice()
            : base("test.failing-device", "离线设备", new TestConfig())
        {
        }

        protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
            => throw new IOException("设备离线");

        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override bool IsConnectionAlive() => false;
    }
}
