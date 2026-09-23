using csLTDMC;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.IO;
using Kwy.Device.Core.Motion;

namespace Kwy.Device.MotionCards.Leadshine;

public sealed class LeadshineMotionCardDevice :
    MotionCardBase,
    IInterpolationMotionController,
    IAxisChannelDefinitionProvider,
    IPositionCompareOutput,
    IIoCardDevice,
    IBulkAxisSnapshotReader,
    IBufferedAxisSnapshotReader
{
    private const int ChannelsPerIoPort = 32;

    private readonly LeadshineMotionCardConfig config;
    private readonly object syncRoot = new();
    private volatile bool connected;
    private bool boardInitialized;
    private readonly HashSet<short> homingAxes = new();
    private readonly HashSet<short> homedAxes = new();
    private readonly Dictionary<short, short[]> coordinateAxes = new();
    private readonly Dictionary<short, PendingInterpolation> pendingInterpolations = new();
    private readonly PulseOutputScheduler pulseScheduler;

    public LeadshineMotionCardDevice(LeadshineMotionCardConfig config)
        : this(config.DeviceId ?? $"Leadshine-{config.CardNo}", config.Model, config)
    {
    }

    public LeadshineMotionCardDevice(string deviceId, string deviceName, LeadshineMotionCardConfig config)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Leadshine motion card configuration.", nameof(config));
        }

        pulseScheduler = new PulseOutputScheduler(
            WriteDoBit,
            () => !disposed && IsConnected,
            (channel, ex) => RaiseErrorOccurred($"Reset DO pulse channel {channel} failed: {ex.Message}", ex));
    }

    public override string DeviceModel => config.Model;

    /// <summary>运行时仅暴露轴定义快照，避免调用方修改设备配置集合。</summary>
    public IReadOnlyCollection<AxisDefinition> Axes => config.Axes.ToArray();

    public int DigitalInputCount => config.DiChannelCount;

    public int DigitalOutputCount => config.DoChannelCount;

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Execute(() =>
        {
            try
            {
                short cardsFound = LTDMC.dmc_board_init();
                if (cardsFound <= 0)
                {
                    throw new InvalidOperationException($"No Leadshine DMC3800 motion card detected. Result={cardsFound}");
                }

                boardInitialized = true;
                SelectCard();

                if (config.ResetOnConnect)
                {
                    ThrowIfFailed(LTDMC.dmc_board_reset_onecard((ushort)config.CardNo), "Reset Leadshine DMC3800 failed");
                }

                if (config.LoadConfigOnConnect && !string.IsNullOrWhiteSpace(config.ConfigFilePath))
                {
                    ThrowIfFailed(LTDMC.dmc_download_configfile((ushort)config.CardNo, config.ConfigFilePath), $"Load Leadshine config file failed: {config.ConfigFilePath}");
                }

                foreach (AxisDefinition definition in config.Axes)
                {
                    if (double.IsFinite(definition.Limits.MinimumPosition)
                        && double.IsFinite(definition.Limits.MaximumPosition))
                    {
                        SetSoftLimitCore(definition.Channel, definition.Limits.MaximumPosition, definition.Limits.MinimumPosition, definition.Engineering);
                    }
                }

                connected = true;
            }
            catch
            {
                CloseBoardNoThrow();
                throw;
            }
        });

        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Execute(() =>
        {
            if (!boardInitialized)
            {
                return;
            }

            CancelPulseTasks();
            SelectCard();
            ThrowIfFailed(LTDMC.dmc_board_close_onecard((ushort)config.CardNo), "Close Leadshine motion card failed");
            boardInitialized = false;
            homingAxes.Clear();
            homedAxes.Clear();
            coordinateAxes.Clear();
            pendingInterpolations.Clear();
            connected = false;
        });

        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive()
    {
        return connected;
    }

    public override void ServoOn(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            ThrowIfFailed(LTDMC.dmc_write_sevon_pin((ushort)config.CardNo, nativeAxis, 1), $"Servo on axis {axis} failed");
        });
    }

    public override void ServoOff(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            ThrowIfFailed(LTDMC.dmc_write_sevon_pin((ushort)config.CardNo, nativeAxis, 0), $"Servo off axis {axis} failed");
        });
    }

    public override void ClearError(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            ThrowIfFailed(LTDMC.dmc_clear_stop_reason((ushort)config.CardNo, nativeAxis), $"Clear axis {axis} stop reason/error failed");
        });
    }

    protected override void MoveAbsCore(short axis, double position, double velocity, double acc, double dec)
    {
        EnsureReady();
        ValidateAxis(axis);
        ValidateVelocity(velocity, nameof(velocity));
        ValidateAxisMotion(axis, position, velocity, acc, dec);

        Execute(() =>
        {
            StartPointMove(axis, position, velocity, acc, dec, positionMode: 0);
        });
    }

    protected override void MoveRelCore(short axis, double distance, double velocity, double acc, double dec)
    {
        EnsureReady();
        ValidateAxis(axis);
        ValidateVelocity(velocity, nameof(velocity));
        ValidateAxisMotion(axis, GetPosition(axis) + distance, velocity, acc, dec);

        Execute(() => StartPointMove(axis, distance, velocity, acc, dec, positionMode: 1));
    }

    public override void MoveJog(short axis, double velocity)
    {
        EnsureReady();
        ValidateAxis(axis);
        ValidateAxisVelocity(axis, Math.Abs(velocity));

        Execute(() =>
        {
            SelectCard();
            AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
            ushort nativeAxis = (ushort)(axis - 1);

            double rawConverted = AxisEngineeringConverter.ToNativeVelocity(velocity, engineering);
            double nativeVelocity = Math.Abs(rawConverted) * 1000.0;
            // LTDMC 接口使用加速时间而非加速度；未提供有效加速度时采用 0.1 秒的保守默认值。
            double Tacc = 0.1;

            ThrowIfFailed(
                LTDMC.dmc_set_profile((ushort)config.CardNo, nativeAxis, 0, nativeVelocity, Tacc, Tacc, 0),
                $"Set jog speed profile axis {axis} failed");

            // LTDMC 的 dir：0 为负向，1 为正向。
            ushort dir = (ushort)(rawConverted >= 0 ? 1 : 0);
            ThrowIfFailed(
                LTDMC.dmc_vmove((ushort)config.CardNo, nativeAxis, dir),
                $"Start jog axis {axis} failed");
        });
    }

    public override void Stop(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            homingAxes.Remove(axis);
            ushort nativeAxis = (ushort)(axis - 1);
            ThrowIfFailed(LTDMC.dmc_stop((ushort)config.CardNo, nativeAxis, 0), $"Stop axis {axis} failed");
        });
    }

    public override void Abort(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            homingAxes.Remove(axis);
            ushort nativeAxis = (ushort)(axis - 1);
            ThrowIfFailed(LTDMC.dmc_stop((ushort)config.CardNo, nativeAxis, 1), $"Abort axis {axis} failed");
        });
    }

    public override void GoHome(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        AxisDefinition definition = config.GetAxisDefinition(axis);
        LeadshineAxisOptions vendorOptions = config.GetAxisOptions(axis);
        AxisHomeDefinition home = definition.Home;
        if (!home.Enabled)
        {
            throw new InvalidOperationException($"Homing is not enabled for axis {axis}.");
        }

        Execute(() =>
        {
            SelectCard();
            homedAxes.Remove(axis);
            homingAxes.Add(axis);

            AxisEngineeringConfig engineering = definition.Engineering;
            ushort nativeAxis = (ushort)(axis - 1);

            double rawVelocity = AxisEngineeringConverter.ToNativeVelocity(home.SearchVelocity * home.Direction, engineering);
            double highVel = Math.Abs(rawVelocity) * 1000.0;
            double lowVel = Math.Abs(AxisEngineeringConverter.ToNativeVelocity(home.CreepVelocity, engineering)) * 1000.0;
            double nativeAcc = AxisEngineeringConverter.ToNativeAcceleration(home.Acceleration, engineering) * 1_000_000.0;

            double Tacc = highVel / nativeAcc;
            if (Tacc < 0.001) Tacc = 0.001;
            double Tdec = Tacc;

            ThrowIfFailed(
                LTDMC.dmc_set_profile((ushort)config.CardNo, nativeAxis, lowVel, highVel, Tacc, Tdec, 0),
                $"Set home profile axis {axis} failed");

            // LTDMC 的 home_dir：0 为正向，1 为负向。
            ushort homeDir = (ushort)(rawVelocity >= 0 ? 0 : 1);

            ThrowIfFailed(
                LTDMC.dmc_set_homemode((ushort)config.CardNo, nativeAxis, homeDir, highVel, vendorOptions.HomeMode, vendorOptions.EzCount),
                $"Set home mode axis {axis} failed");

            double nativeHomeOffset = AxisEngineeringConverter.ToNativePosition(home.Offset, engineering);
            ThrowIfFailed(
                LTDMC.dmc_set_home_position((ushort)config.CardNo, nativeAxis, 1, nativeHomeOffset),
                $"Set home offset axis {axis} failed");

            ThrowIfFailed(
                LTDMC.dmc_home_move((ushort)config.CardNo, nativeAxis),
                $"Start homing axis {axis} failed");
        });
    }

    public void InitCoordinateSystem(short crdIndex, short[] axes)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ArgumentNullException.ThrowIfNull(axes);
        if (axes.Length is < 2 or > 4)
        {
            throw new ArgumentException("Interpolation supports 2 to 4 axes.", nameof(axes));
        }

        foreach (short axis in axes)
        {
            ValidateAxis(axis);
        }

        Execute(() =>
        {
            LeadshineCoordinateSystemConfig? coordinateConfig = config.GetCoordinateSystemConfig(crdIndex)
                ?? throw new InvalidOperationException($"Coordinate system {crdIndex} is not configured.");
            if (!coordinateConfig.Axes.SequenceEqual(axes))
            {
                throw new InvalidOperationException($"Coordinate system {crdIndex} axes do not match its configured axis order.");
            }

            coordinateAxes[crdIndex] = axes.ToArray();
            pendingInterpolations.Remove(crdIndex);
        });
    }

    public void MoveLinear(short crdIndex, double[] positions, double velocity, double acc)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ArgumentNullException.ThrowIfNull(positions);
        ValidateVelocity(velocity, nameof(velocity));
        short[] axes = GetInitializedCoordinateAxes(crdIndex, positions.Length);
        ValidateCoordinateMotion(axes, positions, velocity, acc);

        Execute(() => QueueInterpolation(
            crdIndex,
            new PendingLinearInterpolation(axes.ToArray(), positions.ToArray(), velocity, acc)));
    }

    public void MoveArc(short crdIndex, double x, double y, double xCenter, double yCenter, short dir, double velocity, double acc)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ValidateVelocity(velocity, nameof(velocity));
        short[] axes = GetInitializedCoordinateAxes(crdIndex, 2);
        ValidateCoordinateMotion(axes, new[] { x, y }, velocity, acc);

        if (dir is not 0 and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dir), dir, "Arc direction must be 0 (clockwise) or 1 (counter-clockwise).");
        }

        Execute(() => QueueInterpolation(
            crdIndex,
            new PendingArcInterpolation(axes.ToArray(), x, y, xCenter, yCenter, dir, velocity, acc)));
    }

    public void StartInterpolation(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        Execute(() =>
        {
            SelectCard();
            if (!pendingInterpolations.Remove(crdIndex, out PendingInterpolation? interpolation))
            {
                throw new InvalidOperationException($"Coordinate system {crdIndex} has no pending interpolation segment.");
            }

            StartInterpolationCore(crdIndex, interpolation);
        });
    }

    public void StopCoordinateSystem(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        Execute(() =>
        {
            SelectCard();
            ushort crd = (ushort)(crdIndex - 1);
            pendingInterpolations.Remove(crdIndex);
            ThrowIfFailed(LTDMC.dmc_stop_multicoor((ushort)config.CardNo, crd, 0), $"Stop coordinate system {crdIndex} failed");
        });
    }

    public bool IsCrdMoving(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        return Execute(() =>
        {
            SelectCard();
            ushort crd = (ushort)(crdIndex - 1);
            return LTDMC.dmc_check_done_multicoor((ushort)config.CardNo, crd) == 0;
        });
    }

    public Task WaitForCoordinateSystemStoppedAsync(short crdIndex, CancellationToken cancellationToken = default)
    {
        return WaitUntilAsync(() => !IsCrdMoving(crdIndex), cancellationToken);
    }

    public async Task WaitForCoordinateSystemCompletedAsync(
        short crdIndex,
        double[] targetPositions,
        double tolerance,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ArgumentNullException.ThrowIfNull(targetPositions);
        short[] axes = GetInitializedCoordinateAxes(crdIndex, targetPositions.Length);
        foreach (double targetPosition in targetPositions)
        {
            ValidateCompletionArguments(targetPosition, tolerance);
        }

        MotionAxisSnapshot[] snapshots = new MotionAxisSnapshot[axes.Length];
        var startupGrace = System.Diagnostics.Stopwatch.StartNew();
        await WaitUntilAsync(
            () =>
            {
                bool coordinateStopped = !IsCrdMoving(crdIndex);
                for (int index = 0; index < axes.Length; index++)
                {
                    snapshots[index] = GetAxisSnapshot(axes[index]);
                }

                return coordinateStopped && snapshots
                    .Select((snapshot, idx) => Math.Abs(snapshot.Position - targetPositions[idx]) <= tolerance)
                    .All(completed => completed);
            },
            timeout,
            () =>
            {
                for (int index = 0; index < axes.Length; index++)
                {
                    Exception? failure = GetMotionCompletionFailure(
                        axes[index],
                        targetPositions[index],
                        tolerance,
                        snapshots[index],
                        allowStoppedFailure: startupGrace.Elapsed >= TimeSpan.FromMilliseconds(100));
                    if (failure is not null)
                    {
                        return failure;
                    }
                }

                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public override double GetPosition(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            double position = LTDMC.dmc_get_position((ushort)config.CardNo, nativeAxis);
            return AxisEngineeringConverter.FromNativePosition(position, GetAxisEngineeringConfig(axis));
        });
    }

    public override double GetEncoderPosition(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            double position = LTDMC.dmc_get_encoder((ushort)config.CardNo, nativeAxis);
            return AxisEngineeringConverter.FromNativePosition(position, GetAxisEngineeringConfig(axis));
        });
    }

    public override double GetVelocity(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            double speed = LTDMC.dmc_read_current_speed((ushort)config.CardNo, nativeAxis);
            // 板卡返回单位为 pulse/s，而工程换算器的底层速度单位为 pulse/ms。
            return AxisEngineeringConverter.FromNativeVelocity(speed / 1000.0, GetAxisEngineeringConfig(axis));
        });
    }

    public override int GetStatus(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            return (int)LTDMC.dmc_axis_io_status((ushort)config.CardNo, nativeAxis);
        });
    }

    public override bool IsMoving(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);
            return LTDMC.dmc_check_done((ushort)config.CardNo, nativeAxis) == 0;
        });
    }

    public override bool IsPositiveLimit(short axis) => (GetStatus(axis) & 0x02) != 0;

    public override bool IsNegativeLimit(short axis) => (GetStatus(axis) & 0x04) != 0;

    public override bool IsAlarm(short axis) => (GetStatus(axis) & 0x01) != 0;

    public override MotionAxisSnapshot GetAxisSnapshot(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ushort nativeAxis = (ushort)(axis - 1);

            double nativePosition = LTDMC.dmc_get_position((ushort)config.CardNo, nativeAxis);
            double nativeEncoderPosition = LTDMC.dmc_get_encoder((ushort)config.CardNo, nativeAxis);

            double nativeVelocity = LTDMC.dmc_read_current_speed((ushort)config.CardNo, nativeAxis);
            int status = (int)LTDMC.dmc_axis_io_status((ushort)config.CardNo, nativeAxis);

            ushort homeResultState = 0;
            ThrowIfFailed(
                LTDMC.dmc_get_home_result((ushort)config.CardNo, nativeAxis, ref homeResultState),
                $"Get home result axis {axis} failed");

            HomeState homeState = HomeState.Idle;
            if (homingAxes.Contains(axis))
            {
                homeState = homeResultState switch
                {
                    0 => HomeState.Running,
                    1 => HomeState.Succeeded,
                    _ => HomeState.Failed
                };

                if (homeState is HomeState.Succeeded or HomeState.Failed)
                {
                    homingAxes.Remove(axis);
                    if (homeState == HomeState.Succeeded)
                    {
                        homedAxes.Add(axis);
                    }
                    else
                    {
                        homedAxes.Remove(axis);
                    }
                }
            }
            else if (homedAxes.Contains(axis))
            {
                homeState = HomeState.Succeeded;
            }

            AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
            bool isMoving = LTDMC.dmc_check_done((ushort)config.CardNo, nativeAxis) == 0;

            bool alarm = (status & 0x01) != 0;
            bool posLimit = (status & 0x02) != 0;
            bool negLimit = (status & 0x04) != 0;
            bool servoOn = LTDMC.dmc_read_sevon_pin((ushort)config.CardNo, nativeAxis) != 0;

            return new MotionAxisSnapshot(
                axis,
                AxisEngineeringConverter.FromNativePosition(nativePosition, engineering),
                AxisEngineeringConverter.FromNativePosition(nativeEncoderPosition, engineering),
                AxisEngineeringConverter.FromNativeVelocity(nativeVelocity / 1000.0, engineering),
                status,
                isMoving,
                alarm,
                posLimit,
                negLimit,
                DateTimeOffset.Now,
                servoOn,
                homeState,
                MapAxisFault(alarm, posLimit, negLimit, homeState, status));
        });
    }

    public MotionAxisSnapshot[] GetMultipleAxisSnapshots(short[] axes)
    {
        if (axes == null || axes.Length == 0) return Array.Empty<MotionAxisSnapshot>();
        var results = new MotionAxisSnapshot[axes.Length];
        GetMultipleAxisSnapshots(axes, results);
        return results;
    }

    public void GetMultipleAxisSnapshots(short[] axes, MotionAxisSnapshot[] destination)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length < axes.Length)
        {
            throw new ArgumentException("Destination buffer is smaller than the axis collection.", nameof(destination));
        }

        Execute(() =>
        {
            SelectCard();

            for (int i = 0; i < axes.Length; i++)
            {
                short axis = axes[i];
                ValidateAxis(axis);
                ushort nativeAxis = (ushort)(axis - 1);

                double nativePosition = LTDMC.dmc_get_position((ushort)config.CardNo, nativeAxis);
                double nativeEncoderPosition = LTDMC.dmc_get_encoder((ushort)config.CardNo, nativeAxis);

                double nativeVelocity = LTDMC.dmc_read_current_speed((ushort)config.CardNo, nativeAxis);
                int status = (int)LTDMC.dmc_axis_io_status((ushort)config.CardNo, nativeAxis);

                ushort homeResultState = 0;
                ThrowIfFailed(
                    LTDMC.dmc_get_home_result((ushort)config.CardNo, nativeAxis, ref homeResultState),
                    $"Get home result axis {axis} failed");

                HomeState homeState = HomeState.Idle;
                if (homingAxes.Contains(axis))
                {
                    homeState = homeResultState switch
                    {
                        0 => HomeState.Running,
                        1 => HomeState.Succeeded,
                        _ => HomeState.Failed
                    };

                    if (homeState is HomeState.Succeeded or HomeState.Failed)
                    {
                        homingAxes.Remove(axis);
                        if (homeState == HomeState.Succeeded)
                        {
                            homedAxes.Add(axis);
                        }
                        else
                        {
                            homedAxes.Remove(axis);
                        }
                    }
                }
                else if (homedAxes.Contains(axis))
                {
                    homeState = HomeState.Succeeded;
                }

                AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
                bool isMoving = LTDMC.dmc_check_done((ushort)config.CardNo, nativeAxis) == 0;

                bool alarm = (status & 0x01) != 0;
                bool posLimit = (status & 0x02) != 0;
                bool negLimit = (status & 0x04) != 0;
                bool servoOn = LTDMC.dmc_read_sevon_pin((ushort)config.CardNo, nativeAxis) != 0;

                destination[i] = new MotionAxisSnapshot(
                    axis,
                    AxisEngineeringConverter.FromNativePosition(nativePosition, engineering),
                    AxisEngineeringConverter.FromNativePosition(nativeEncoderPosition, engineering),
                    AxisEngineeringConverter.FromNativeVelocity(nativeVelocity / 1000.0, engineering),
                    status,
                    isMoving,
                    alarm,
                    posLimit,
                    negLimit,
                    DateTimeOffset.Now,
                    servoOn,
                    homeState,
                    MapAxisFault(alarm, posLimit, negLimit, homeState, status));
            }
        });
    }

    public override async Task<HomeStatus> WaitForHomeCompletedAsync(short axis, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            HomeStatus result = await base.WaitForHomeCompletedAsync(axis, timeout, cancellationToken).ConfigureAwait(false);
            if (result.State == HomeState.Succeeded)
            {
                ApplyHomeCoordinate(axis);
            }

            return result;
        }
        catch (TimeoutException)
        {
            Stop(axis);
            throw;
        }
    }

    public override async Task WaitForHomeCompletedAsync(short axis, CancellationToken cancellationToken = default)
        => await WaitForHomeCompletedAsync(axis, GetAxisDefinition(axis).Home.Timeout, cancellationToken).ConfigureAwait(false);

    public override void SetSoftLimit(short axis, double positive, double negative)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
            SetSoftLimitCore(axis, positive, negative, engineering);
        });
    }

    public AxisDefinition GetAxisDefinition(short axis)
    {
        ValidateAxis(axis);
        return config.GetAxisDefinition(axis);
    }

    public AxisEngineeringConfig GetAxisEngineeringConfig(short axis) => GetAxisDefinition(axis).Engineering;

    public void WriteDoBit(int channel, bool state)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, config.DoChannelCount, nameof(channel));
        ulong bitMask = 1UL << channel;
        WriteDoPortMask(state ? bitMask : 0, bitMask);
    }

    public void WriteDoPortMask(ulong mask)
    {
        WriteDoPortMask(mask, IoBitConverter.CreateWritableMask(config.DoChannelCount));
    }

    public void WriteDoPortMask(ulong mask, ulong changedMask)
    {
        EnsureReady();
        ulong writableMask = IoBitConverter.CreateWritableMask(config.DoChannelCount);
        changedMask &= writableMask;

        if (changedMask == 0)
        {
            return;
        }

        Execute(() =>
        {
            SelectCard();
            int portCount = GetIoPortCount(config.DoChannelCount);
            for (int port = 0; port < portCount; port++)
            {
                int shift = port * ChannelsPerIoPort;
                uint portChangedMask = (uint)(changedMask >> shift);
                if (portChangedMask == 0)
                {
                    continue;
                }

                uint currentPhysical = LTDMC.dmc_read_outport((ushort)config.CardNo, (ushort)port);
                uint requestedLogical = (uint)(mask >> shift);
                uint requestedPhysical = ToPhysicalIoValue(requestedLogical);
                uint targetPhysical = (currentPhysical & ~portChangedMask) | (requestedPhysical & portChangedMask);

                if (targetPhysical != currentPhysical)
                {
                    ThrowIfFailed(
                        LTDMC.dmc_write_outport((ushort)config.CardNo, (ushort)port, targetPhysical),
                        $"Write DO port {port} mask failed");
                }
            }
        });
    }

    public bool[] ReadAllDo()
    {
        EnsureReady();
        return ToLogicalBits(ReadLogicalDoMask(), config.DoChannelCount);
    }

    public bool ReadDiBit(int channel)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, config.DiChannelCount, nameof(channel));
        return (ReadDiPortMask() & (1UL << channel)) != 0;
    }

    public bool[] ReadAllDi()
    {
        EnsureReady();
        return ToLogicalBits(ReadDiPortMask(), config.DiChannelCount);
    }

    public ulong ReadDiPortMask()
    {
        EnsureReady();
        ulong physicalMask = ReadPhysicalIoMask(config.DiChannelCount, readOutput: false);
        return ToLogicalIoValue(physicalMask, config.DiChannelCount);
    }

    public void WritePulse(int channel, int durationMs)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, config.DoChannelCount, nameof(channel));
        pulseScheduler.WritePulse(channel, durationMs);
    }

    public void EnablePso(short axis, double[] triggerPositions, double pulseScale = 10000.0, short pulseWidthUs = 20)
    {
        EnsureReady();
        ValidateAxis(axis);
        ArgumentNullException.ThrowIfNull(triggerPositions);
        if (triggerPositions.Length == 0)
        {
            throw new ArgumentException("Trigger positions cannot be empty.", nameof(triggerPositions));
        }

        Execute(() =>
        {
            SelectCard();
            ushort hcmp = 0;
            ThrowIfFailed(LTDMC.dmc_hcmp_set_mode((ushort)config.CardNo, hcmp, 0), "Disable HCMP before configuration failed");
            ThrowIfFailed(LTDMC.dmc_hcmp_clear_points((ushort)config.CardNo, hcmp), "Clear HCMP points failed");

            // LTDMC 比较输出以 50ns 为一个计数单位，例如 20us 对应 400 个计数。
            int ticks = (int)(pulseWidthUs * 20);
            // cmp_source：0 为指令位置；cmp_logic：1 为正高电平脉冲。
            ThrowIfFailed(
                LTDMC.dmc_hcmp_set_config((ushort)config.CardNo, hcmp, (ushort)(axis - 1), 0, 1, ticks),
                $"Configure HCMP for PSO axis {axis} failed");

            foreach (double position in triggerPositions)
            {
                int pulsePosition = ToIntPosition(position * pulseScale);
                ThrowIfFailed(
                    LTDMC.dmc_hcmp_add_point((ushort)config.CardNo, hcmp, pulsePosition),
                    $"Add compare point {position} for PSO axis {axis} failed");
            }

            ThrowIfFailed(
                LTDMC.dmc_hcmp_set_mode((ushort)config.CardNo, hcmp, 1),
                $"Enable HCMP for PSO axis {axis} failed");
        });
    }

    public void DisablePso()
    {
        EnsureReady();
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(LTDMC.dmc_hcmp_set_mode((ushort)config.CardNo, 0, 0), "Disable HCMP failed");
            ThrowIfFailed(LTDMC.dmc_hcmp_clear_points((ushort)config.CardNo, 0), "Clear HCMP points failed");
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        CancelPulseTasks();
        await base.DisposeAsync();
    }


    private ulong ReadLogicalDoMask()
    {
        ulong physicalMask = ReadPhysicalIoMask(config.DoChannelCount, readOutput: true);
        return ToLogicalIoValue(physicalMask, config.DoChannelCount);
    }

    private ulong ReadPhysicalIoMask(int channelCount, bool readOutput)
    {
        return Execute(() =>
        {
            SelectCard();
            ulong mask = 0;
            int portCount = GetIoPortCount(channelCount);
            for (int port = 0; port < portCount; port++)
            {
                uint portValue = readOutput
                    ? LTDMC.dmc_read_outport((ushort)config.CardNo, (ushort)port)
                    : LTDMC.dmc_read_inport((ushort)config.CardNo, (ushort)port);
                mask |= (ulong)portValue << (port * ChannelsPerIoPort);
            }

            return mask;
        });
    }

    private static bool[] ToLogicalBits(ulong logicalMask, int channelCount)
    {
        var bits = new bool[IoBitConverter.DefaultChannelCount];
        for (int channel = 0; channel < channelCount; channel++)
        {
            bits[channel] = (logicalMask & (1UL << channel)) != 0;
        }

        return bits;
    }

    private ulong ToLogicalIoValue(ulong physicalMask, int channelCount)
    {
        ulong validMask = IoBitConverter.CreateWritableMask(channelCount);
        return config.DigitalIoActiveLow
            ? ~physicalMask & validMask
            : physicalMask & validMask;
    }

    private uint ToPhysicalIoValue(uint logicalValue)
    {
        return config.DigitalIoActiveLow ? ~logicalValue : logicalValue;
    }

    private static int GetIoPortCount(int channelCount)
        => (channelCount + ChannelsPerIoPort - 1) / ChannelsPerIoPort;

    private void StartPointMove(
        short axis,
        double positionOrDistance,
        double velocity,
        double acceleration,
        double deceleration,
        ushort positionMode)
    {
        SelectCard();
        AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
        ushort nativeAxis = (ushort)(axis - 1);
        int nativePosition = ToIntPosition(AxisEngineeringConverter.ToNativePosition(positionOrDistance, engineering));
        double nativeVelocity = Math.Abs(AxisEngineeringConverter.ToNativeVelocity(velocity, engineering)) * 1000.0;
        double nativeAcceleration = AxisEngineeringConverter.ToNativeAcceleration(acceleration, engineering) * 1_000_000.0;
        double nativeDeceleration = AxisEngineeringConverter.ToNativeAcceleration(deceleration, engineering) * 1_000_000.0;
        double accelerationTime = Math.Max(nativeVelocity / nativeAcceleration, 0.001);
        double decelerationTime = Math.Max(nativeVelocity / nativeDeceleration, 0.001);

        ThrowIfFailed(
            LTDMC.dmc_set_profile((ushort)config.CardNo, nativeAxis, 0, nativeVelocity, accelerationTime, decelerationTime, 0),
            $"Set speed profile axis {axis} failed");
        ThrowIfFailed(
            LTDMC.dmc_pmove((ushort)config.CardNo, nativeAxis, nativePosition, positionMode),
            $"Start {(positionMode == 0 ? "absolute" : "relative")} motion axis {axis} failed");
    }

    private void QueueInterpolation(short coordinateSystem, PendingInterpolation interpolation)
    {
        if (pendingInterpolations.ContainsKey(coordinateSystem))
        {
            throw new InvalidOperationException(
                $"Coordinate system {coordinateSystem} already has a pending interpolation segment. " +
                "DMC3800 does not provide the continuous FIFO used by DMC5X10 controllers.");
        }

        if (IsCrdMoving(coordinateSystem))
        {
            throw new InvalidOperationException($"Coordinate system {coordinateSystem} is currently moving.");
        }

        pendingInterpolations.Add(coordinateSystem, interpolation);
    }

    private void StartInterpolationCore(short coordinateSystem, PendingInterpolation interpolation)
    {
        ushort nativeCoordinate = (ushort)(coordinateSystem - 1);
        ushort[] nativeAxes = interpolation.Axes.Select(axis => (ushort)(axis - 1)).ToArray();
        double nativeVelocity = ToCoordinateVelocity(interpolation.Velocity, interpolation.Axes) * 1000.0;
        double nativeAcceleration = ToCoordinateAcceleration(interpolation.Acceleration, interpolation.Axes) * 1_000_000.0;
        double accelerationTime = Math.Max(nativeVelocity / nativeAcceleration, 0.001);

        ThrowIfFailed(
            LTDMC.dmc_set_vector_profile_multicoor(
                (ushort)config.CardNo,
                nativeCoordinate,
                0,
                nativeVelocity,
                accelerationTime,
                accelerationTime,
                0),
            $"Set vector profile for coordinate {coordinateSystem} failed");

        switch (interpolation)
        {
            case PendingLinearInterpolation linear:
                int[] targets = linear.Positions
                    .Select((position, index) => ToIntPosition(
                        AxisEngineeringConverter.ToNativePosition(position, GetAxisEngineeringConfig(linear.Axes[index]))))
                    .ToArray();
                ThrowIfFailed(
                    LTDMC.dmc_line_multicoor(
                        (ushort)config.CardNo,
                        nativeCoordinate,
                        (ushort)nativeAxes.Length,
                        nativeAxes,
                        targets,
                        0),
                    $"Start linear interpolation on coordinate {coordinateSystem} failed");
                break;

            case PendingArcInterpolation arc:
                int[] target =
                {
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(arc.X, GetAxisEngineeringConfig(arc.Axes[0]))),
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(arc.Y, GetAxisEngineeringConfig(arc.Axes[1])))
                };
                int[] center =
                {
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(arc.XCenter, GetAxisEngineeringConfig(arc.Axes[0]))),
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(arc.YCenter, GetAxisEngineeringConfig(arc.Axes[1])))
                };
                ThrowIfFailed(
                    LTDMC.dmc_arc_move_multicoor(
                        (ushort)config.CardNo,
                        nativeCoordinate,
                        nativeAxes,
                        target,
                        center,
                        (ushort)arc.Direction,
                        0),
                    $"Start arc interpolation on coordinate {coordinateSystem} failed");
                break;

            default:
                throw new NotSupportedException($"Unsupported interpolation type {interpolation.GetType().Name}.");
        }
    }

    private void CancelPulseTasks()
    {
        pulseScheduler.CancelAll();
    }

    private void CloseBoardNoThrow()
    {
        if (boardInitialized)
        {
            LTDMC.dmc_board_close_onecard((ushort)config.CardNo);
        }

        boardInitialized = false;
        connected = false;
        homingAxes.Clear();
        homedAxes.Clear();
        coordinateAxes.Clear();
        pendingInterpolations.Clear();
    }

    private void ApplyHomeCoordinate(short axis)
    {
        AxisDefinition definition = GetAxisDefinition(axis);
        double nativePosition = AxisEngineeringConverter.ToNativePosition(definition.Home.Position, definition.Engineering);
        ushort nativeAxis = (ushort)(axis - 1);

        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(
                LTDMC.dmc_set_position((ushort)config.CardNo, nativeAxis, ToIntPosition(nativePosition)),
                $"Set command position after homing axis {axis} failed");
            ThrowIfFailed(
                LTDMC.dmc_set_encoder((ushort)config.CardNo, nativeAxis, ToIntPosition(nativePosition)),
                $"Set encoder position after homing axis {axis} failed");
        });
    }

    private T Execute<T>(Func<T> action)
    {
        lock (syncRoot)
        {
            return action();
        }
    }

    private void Execute(Action action)
    {
        lock (syncRoot)
        {
            action();
        }
    }

    private void SelectCard()
    {
        // LTDMC 的具体函数直接接收 CardNo，无需维护额外的选卡上下文。
    }

    private void EnsureReady()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new InvalidOperationException("Leadshine motion card is not connected.");
        }
    }

    private void ValidateAxis(short axis)
    {
        if (axis < 1 || axis > config.AxisCount)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), axis, $"Axis must be between 1 and {config.AxisCount}.");
        }
    }

    private static void ValidateCoordinate(short crdIndex)
    {
        if (crdIndex is < 1 or > LeadshineMotionCardConfig.MaxSupportedCoordinateSystemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(crdIndex), crdIndex, $"Coordinate index must be between 1 and {LeadshineMotionCardConfig.MaxSupportedCoordinateSystemCount}.");
        }
    }

    private static void ValidateVelocity(double velocity, string parameterName)
    {
        if (velocity <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, velocity, "Velocity must be greater than 0.");
        }
    }

    private void ValidateAxisMotion(short axis, double position, double velocity, double acceleration, double deceleration)
    {
        AxisDefinition definition = GetAxisDefinition(axis);
        if (position < definition.Limits.MinimumPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"Axis {axis} position must be greater than or equal to {definition.Limits.MinimumPosition}.");
        }

        if (position > definition.Limits.MaximumPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"Axis {axis} position must be less than or equal to {definition.Limits.MaximumPosition}.");
        }

        ValidateAxisVelocity(axis, velocity);
        ValidateMaximum(axis, acceleration, definition.Limits.MaximumAcceleration, nameof(acceleration));
        ValidateMaximum(axis, deceleration, definition.Limits.MaximumDeceleration, nameof(deceleration));
    }

    private void ValidateAxisVelocity(short axis, double velocity)
    {
        ValidateMaximum(axis, velocity, GetAxisDefinition(axis).Limits.MaximumVelocity, nameof(velocity));
    }

    private static void ValidateMaximum(short axis, double value, double maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Axis {axis} {parameterName} must be finite and greater than 0.");
        }

        if (value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Axis {axis} {parameterName} must not exceed {maximum}.");
        }
    }

    private void ValidateCoordinateMotion(short[] axes, double[] positions, double velocity, double acceleration)
    {
        for (int index = 0; index < axes.Length; index++)
        {
            AxisDefinition definition = GetAxisDefinition(axes[index]);
            double position = positions[index];
            if (position < definition.Limits.MinimumPosition
                || position > definition.Limits.MaximumPosition)
            {
                throw new ArgumentOutOfRangeException(nameof(positions), position, $"Axis {axes[index]} interpolation target is outside configured travel limits.");
            }

            ValidateAxisVelocity(axes[index], velocity);
            ValidateMaximum(axes[index], acceleration, definition.Limits.MaximumAcceleration, nameof(acceleration));
        }
    }

    private short[] GetInitializedCoordinateAxes(short coordinateSystem, int expectedDimension)
    {
        short[] axes = Execute(() => coordinateAxes.TryGetValue(coordinateSystem, out short[]? configured)
            ? configured.ToArray()
            : throw new InvalidOperationException($"Coordinate system {coordinateSystem} has not been initialized."));
        if (axes.Length != expectedDimension)
        {
            throw new InvalidOperationException($"Coordinate system {coordinateSystem} dimension is {axes.Length}, but the motion requires {expectedDimension} axes.");
        }

        return axes;
    }

    private double ToCoordinateVelocity(double velocity, IReadOnlyList<short> axes)
        => Math.Abs(AxisEngineeringConverter.ToNativeVelocity(velocity, GetAxisEngineeringConfig(axes[0])));

    private double ToCoordinateAcceleration(double acceleration, IReadOnlyList<short> axes)
        => AxisEngineeringConverter.ToNativeAcceleration(acceleration, GetAxisEngineeringConfig(axes[0]));

    private void SetSoftLimitCore(short axis, double positive, double negative, AxisEngineeringConfig engineering)
    {
        int posNative = ToIntPosition(AxisEngineeringConverter.ToNativePosition(positive, engineering));
        int negNative = ToIntPosition(AxisEngineeringConverter.ToNativePosition(negative, engineering));
        ushort nativeAxis = (ushort)(axis - 1);

        int minLimit = Math.Min(posNative, negNative);
        int maxLimit = Math.Max(posNative, negNative);

        ThrowIfFailed(
            LTDMC.dmc_set_softlimit((ushort)config.CardNo, nativeAxis, 1, 0, 0, minLimit, maxLimit),
            $"Set soft limit axis {axis} failed");
    }

    private static int ToIntPosition(double value) => checked((int)Math.Round(value));

    private void ThrowIfFailed(short result, string message)
    {
        if (result != 0)
        {
            var fullMessage = $"[{DeviceName}/{DeviceId}] {message}. Result={result}.";
            RaiseErrorOccurred(fullMessage);
            throw new InvalidOperationException(fullMessage);
        }
    }

    private abstract record PendingInterpolation(short[] Axes, double Velocity, double Acceleration);

    private sealed record PendingLinearInterpolation(
        short[] Axes,
        double[] Positions,
        double Velocity,
        double Acceleration)
        : PendingInterpolation(Axes, Velocity, Acceleration);

    private sealed record PendingArcInterpolation(
        short[] Axes,
        double X,
        double Y,
        double XCenter,
        double YCenter,
        short Direction,
        double Velocity,
        double Acceleration)
        : PendingInterpolation(Axes, Velocity, Acceleration);
}
