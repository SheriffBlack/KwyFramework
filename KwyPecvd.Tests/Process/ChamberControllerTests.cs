using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Simulated;
using KwyPecvd.Process.Chambers;

namespace KwyPecvd.Tests.Process;

public sealed class ChamberControllerTests
{
    [Fact]
    public async Task InitializeAsync_EntersIdleWhenMfcIsReady()
    {
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();

        var result = await chamber.InitializeAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(
            ChamberState.Idle,
            chamber.Snapshot.State);
    }

    [Fact]
    public async Task SetGasFlowAsync_MapsGasToConfiguredMfc()
    {
        // Arrange
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();
        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        // Act：上层只使用工艺名称SiH4，不知道具体MFC ID
        var result = await chamber.SetGasFlowAsync(
            "SiH4",
            100,
            TimeSpan.Zero);

        // 模拟RT执行一个硬件周期
        await mfc.ExecuteCycleAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(100, mfc.Snapshot.SetPoint);
        Assert.True(mfc.Snapshot.Feedback > 0);
    }

    [Fact]
    public async Task SetGasFlowAsync_RejectsUnknownGas()
    {
        // Arrange
        var registry = new HardwareComponentRegistry();
        var chamber = CreateChamber(registry);

        // Act：PM1只配置了SiH4，没有配置NH3
        var result = await chamber.SetGasFlowAsync(
            "NH3",
            100,
            TimeSpan.Zero);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(
            ResultCodes.InvalidArgument,
            result.Code);
    }

    [Fact]
    public async Task SetGasFlowAsync_ReturnsNotReadyWhenMfcIsMissing()
    {
        // Arrange：Definition声明了MFC，但Registry没有实例
        var registry = new HardwareComponentRegistry();
        var chamber = CreateChamber(registry);

        // Act
        var result = await chamber.SetGasFlowAsync(
            "SiH4",
            100,
            TimeSpan.Zero);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(
            ResultCodes.NotReady,
            result.Code);
    }

    [Fact]
    public async Task SetGasFlowAsync_ThrowsWhenMfcBelongsToAnotherChamber()
    {
        // Arrange：将属于PM2的MFC错误地配置给PM1
        var registry = new HardwareComponentRegistry();

        var wrongMfc = CreateMfc(
            moduleId: "PM2");

        registry.Add(wrongMfc);

        var chamber = CreateChamber(registry);

        // Act + Assert
        var exception = await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => chamber.SetGasFlowAsync(
                "SiH4",
                100,
                TimeSpan.Zero));

        Assert.Contains(
            "not chamber 'PM1'",
            exception.Message);
    }

    [Fact]
    public async Task Snapshot_AggregatesLatestMfcFeedback()
    {
        // Arrange
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();
        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        await chamber.SetGasFlowAsync(
            "SiH4",
            100,
            TimeSpan.Zero);

        // 模拟多个RT周期，使反馈逐渐接近目标
        for (var index = 0; index < 20; index++)
        {
            await mfc.ExecuteCycleAsync();
        }

        // Act
        var chamberSnapshot = chamber.Snapshot;

        // Assert
        Assert.Equal(
            "PM1",
            chamberSnapshot.ChamberId);

        var gasFlow = Assert.Single(
            chamberSnapshot.GasFlows);

        Assert.Equal(
            "SiH4",
            gasFlow.GasName);

        Assert.Equal(
            100,
            gasFlow.Mfc.SetPoint);

        Assert.InRange(
            gasFlow.Mfc.Feedback,
            98,
            100);
    }

    [Fact]
    public async Task StopGasFlowAsync_SetsConfiguredMfcToZero()
    {
        // Arrange
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();
        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        await chamber.SetGasFlowAsync(
            "SiH4",
            100,
            TimeSpan.Zero);

        await mfc.ExecuteCycleAsync();

        // Act
        var result = await chamber.StopGasFlowAsync(
            "SiH4");

        await mfc.ExecuteCycleAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(0, mfc.Snapshot.SetPoint);
    }

    [Fact]
    public async Task InitializeAsync_RemainsOfflineWhenMfcReportsOffline()
    {
        var registry = new HardwareComponentRegistry();
        var mfc = CreateOfflineMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        var result = await chamber.InitializeAsync();

        Assert.False(result.Succeeded);

        Assert.Equal(
            ResultCodes.NotReady,
            result.Code);

        Assert.Equal(
            ChamberState.Offline,
            chamber.Snapshot.State);
    }

    [Fact]
    public async Task InitializeAsync_RemainsOfflineWhenFeedbackIsInvalid()
    {
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();

        var result = await chamber.InitializeAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ResultCodes.NotReady, result.Code);
        Assert.Equal(
            ChamberState.Offline,
            chamber.Snapshot.State);
        Assert.Contains(
            MfcDiagnosticCodes.NotSampled,
            result.Message);
    }

    [Fact]
    public async Task StartGasPreparationAsync_EntersPreparing()
    {
        // Arrange
        var registry = new HardwareComponentRegistry();

        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        // 初始化MFC配置
        await mfc.InitializeAsync();

        // 执行第一次采样，使反馈变为有效
        await mfc.ExecuteCycleAsync();

        // 初始化腔体：Offline → Initializing → Idle
        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        Assert.Equal(
            ChamberState.Idle,
            chamber.Snapshot.State);

        // Act
        var result =
            await chamber.StartGasPreparationAsync(
            [
                new ChamberGasTarget
            {
                GasName = "SiH4",
                Target = 100,
                RampDuration =
                    TimeSpan.FromSeconds(5)
            }
            ]);

        // Assert
        Assert.True(result.Succeeded);

        Assert.Equal(
            ChamberState.Preparing,
            chamber.Snapshot.State);

        Assert.True(
            mfc.Snapshot.IsRamping);
    }

    [Fact]
    public async Task SetGasFlowAsync_RejectsWhenChamberIsOffline()
    {
        var registry = new HardwareComponentRegistry();
        var mfc = CreateMfc();

        registry.Add(mfc);

        var chamber = CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();

        var result = await chamber.SetGasFlowAsync(
            "SiH4",
            100,
            TimeSpan.Zero);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultCodes.InvalidState, result.Code);
        Assert.Equal(0, mfc.Snapshot.SetPoint);
    }

    [Fact]
    public async Task
    ExecuteCycleAsync_EntersProcessingWhenGasReachesTarget()
    {
        var registry =
            new HardwareComponentRegistry();

        var mfc = CreateMfc();
        registry.Add(mfc);

        var chamber =
            CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();

        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        var startResult =
            await chamber.StartGasPreparationAsync(
            [
                new ChamberGasTarget
            {
                GasName = "SiH4",
                Target = 100,
                RampDuration = TimeSpan.Zero
            }
            ]);

        Assert.True(startResult.Succeeded);

        for (var index = 0;
             index < 30;
             index++)
        {
            // 底层硬件先刷新反馈
            await mfc.ExecuteCycleAsync();

            // 上层腔体再根据反馈推进状态
            await chamber.ExecuteCycleAsync();

            if (chamber.Snapshot.State ==
                ChamberState.Processing)
            {
                break;
            }
        }

        Assert.Equal(
            ChamberState.Processing,
            chamber.Snapshot.State);

        Assert.InRange(
            mfc.Snapshot.Feedback,
            98,
            102);
    }

    [Fact]
    public async Task StartGasPreparationAsync_AcceptsOnlyOneConcurrentRequest()
    {
        var registry =
            new HardwareComponentRegistry();

        var mfc = CreateMfc();
        registry.Add(mfc);

        var chamber =
            CreateChamber(registry);

        await mfc.InitializeAsync();
        await mfc.ExecuteCycleAsync();

        var initializeResult =
            await chamber.InitializeAsync();

        Assert.True(initializeResult.Succeeded);

        ChamberGasTarget[] targets =
        [
            new ChamberGasTarget
        {
            GasName = "SiH4",
            Target = 100,
            RampDuration =
                TimeSpan.FromSeconds(5)
        }
        ];

        var first =
            chamber.StartGasPreparationAsync(
                targets);

        var second =
            chamber.StartGasPreparationAsync(
                targets);

        var results = await Task.WhenAll(
            first,
            second);

        Assert.Single(
            results,
            result => result.Succeeded);

        Assert.Single(
            results,
            result =>
                !result.Succeeded &&
                result.Code ==
                    ResultCodes.InvalidState);

        Assert.Equal(
            ChamberState.Preparing,
            chamber.Snapshot.State);
    }

    private static ChamberController CreateChamber(
        IHardwareComponentRegistry registry)
    {
        var definition = new ChamberDefinition
        {
            Id = "PM1",
            DisplayName = "Process Module 1",
            GasLines =
            [
                new ChamberGasLineDefinition
                {
                    GasName = "SiH4",
                    MfcId = "MFC_SIH4"
                }
            ]
        };

        return new ChamberController(
            definition,
            registry);
    }

    private static SimulatedMfc CreateMfc(
        string moduleId = "PM1")
    {
        return new SimulatedMfc(
            new MfcDefinition
            {
                Id = "MFC_SIH4",
                ModuleId = moduleId,
                DisplayName = "SiH4 MFC",
                GasName = "SiH4",
                Unit = "sccm",
                FullScale = 500,
                Tolerance = 2,
                ToleranceDelay =
                    TimeSpan.FromSeconds(3)
            });
    }

    private static OfflineMfc CreateOfflineMfc()
    {
        return new OfflineMfc(
            new MfcDefinition
            {
                Id = "MFC_SIH4",
                ModuleId = "PM1",
                DisplayName = "SiH4 MFC",
                GasName = "SiH4",
                Unit = "sccm",
                FullScale = 500,
                Tolerance = 2,
                ToleranceDelay =
                    TimeSpan.FromSeconds(3)
            });
    }

    private sealed class OfflineMfc : MfcBase
    {
        public OfflineMfc(MfcDefinition definition)
            : base(definition, initiallyOffline: true)
        {
        }

        protected override ValueTask<MfcCycleResult>
            ExchangeAsync(
                double setPoint,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                new MfcCycleResult(
                    Feedback: 0,
                    IsOffline: true,
                    IsFeedbackValid: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    DiagnosticCode:
                        MfcDiagnosticCodes.Offline));
        }
    }
}