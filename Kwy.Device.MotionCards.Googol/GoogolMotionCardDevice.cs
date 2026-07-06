using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.IO;
using Kwy.Device.Core.Motion;
using Kwy.Device.MotionCards.Googol.DLL;

namespace Kwy.Device.MotionCards.Googol;

public sealed class GoogolMotionCardDevice :
    MotionCardBase,
    IAdvancedMotionCard,
    IAxisEngineeringUnitProvider,
    IPositionCompareOutput,
    IIoCardDevice,
    IBulkAxisSnapshotReader,
    IBufferedAxisSnapshotReader
{
    private readonly GoogolMotionCardConfig config;
    private readonly object syncRoot = new();
    private volatile bool connected;
    private bool sdkOpened;
    private readonly HashSet<short> homingAxes = new();
    private readonly HashSet<short> homedAxes = new();
    private readonly Dictionary<short, short[]> coordinateAxes = new();
    private readonly PulseOutputScheduler pulseScheduler;

    public GoogolMotionCardDevice(GoogolMotionCardConfig config)
        : this(config.DeviceId ?? $"Googol-{config.CardNo}", config.Model, config)
    {
    }

    public GoogolMotionCardDevice(string deviceId, string deviceName, GoogolMotionCardConfig config)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Googol motion card configuration.", nameof(config));
        }

        pulseScheduler = new PulseOutputScheduler(
            WriteDoBit,
            () => !disposed && IsConnected,
            (channel, ex) => RaiseErrorOccurred($"Reset GPO pulse channel {channel} failed: {ex.Message}", ex));
    }

    public override string DeviceModel => config.Model;

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Execute(() =>
        {
            try
            {
                ThrowIfFailed(
                    mc.GT_Open(config.OpenChannel, config.OpenParameter),
                    $"Open Googol motion card failed. Channel={config.OpenChannel}, Parameter={config.OpenParameter}");
                sdkOpened = true;
                SelectCard();

                if (config.ResetOnConnect)
                {
                    ThrowIfFailed(mc.GT_Reset(), "Reset Googol motion card failed");
                }

                if (config.LoadConfigOnConnect && !string.IsNullOrWhiteSpace(config.ConfigFilePath))
                {
                    ThrowIfFailed(mc.GT_LoadConfig(config.ConfigFilePath), $"Load Googol config file failed: {config.ConfigFilePath}");
                }

                foreach (GoogolAxisConfig axisConfig in config.Axes)
                {
                    if (axisConfig.MinimumPosition is double minimum && axisConfig.MaximumPosition is double maximum)
                    {
                        SetSoftLimitCore(axisConfig.Axis, maximum, minimum, axisConfig.ToEngineeringConfig());
                    }
                }

                connected = true;
            }
            catch
            {
                CloseSdkNoThrow();
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
            if (!sdkOpened)
            {
                return;
            }

            CancelPulseTasks();
            SelectCard();
            ThrowIfFailed(mc.GT_Close(), "Close Googol motion card failed");
            sdkOpened = false;
            homingAxes.Clear();
            homedAxes.Clear();
            coordinateAxes.Clear();
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
            ThrowIfFailed(mc.GT_AxisOn(axis), $"Servo on axis {axis} failed");
        });
    }

    public override void ServoOff(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_AxisOff(axis), $"Servo off axis {axis} failed");
        });
    }

    public override void ClearError(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_ClrSts(axis, 1), $"Clear axis {axis} status failed");
        });
    }

    public override void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5)
    {
        EnsureReady();
        ValidateAxis(axis);
        ValidateVelocity(velocity, nameof(velocity));
        ValidateAxisMotion(axis, position, velocity, acc, dec);

        Execute(() =>
        {
            SelectCard();
            AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
            ThrowIfFailed(mc.GT_PrfTrap(axis), $"Set trap profile axis {axis} failed");
            ThrowIfFailed(mc.GT_GetTrapPrm(axis, out var prm), $"Get trap parameters axis {axis} failed");
            prm.acc = AxisEngineeringConverter.ToNativeAcceleration(acc, engineering);
            prm.dec = AxisEngineeringConverter.ToNativeAcceleration(dec, engineering);
            ThrowIfFailed(mc.GT_SetTrapPrm(axis, ref prm), $"Set trap parameters axis {axis} failed");
            ThrowIfFailed(mc.GT_SetPos(axis, ToIntPosition(AxisEngineeringConverter.ToNativePosition(position, engineering))), $"Set target position axis {axis} failed");
            ThrowIfFailed(mc.GT_SetVel(axis, Math.Abs(AxisEngineeringConverter.ToNativeVelocity(velocity, engineering))), $"Set target velocity axis {axis} failed");
            ThrowIfFailed(mc.GT_Update(AxisMask(axis)), $"Start axis {axis} motion failed");
        });
    }

    public override void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5)
    {
        MoveAbs(axis, GetPosition(axis) + distance, velocity, acc, dec);
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
            double nativeVelocity = AxisEngineeringConverter.ToNativeVelocity(velocity, engineering);
            ThrowIfFailed(mc.GT_PrfJog(axis), $"Set jog profile axis {axis} failed");
            ThrowIfFailed(mc.GT_GetJogPrm(axis, out var prm), $"Get jog parameters axis {axis} failed");
            prm.acc = Math.Max(Math.Abs(nativeVelocity) * 0.1, 0.001);
            prm.dec = prm.acc;
            ThrowIfFailed(mc.GT_SetJogPrm(axis, ref prm), $"Set jog parameters axis {axis} failed");
            ThrowIfFailed(mc.GT_SetVel(axis, nativeVelocity), $"Set jog velocity axis {axis} failed");
            ThrowIfFailed(mc.GT_Update(AxisMask(axis)), $"Start jog axis {axis} failed");
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
            ThrowIfFailed(mc.GT_Stop(AxisMask(axis), 0), $"Stop axis {axis} failed");
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
            ThrowIfFailed(mc.GT_Stop(AxisMask(axis), 1), $"Abort axis {axis} failed");
        });
    }

    public override void GoHome(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        GoogolAxisConfig axisConfig = config.GetAxisConfig(axis);
        if (!axisConfig.Home.Enabled)
        {
            throw new InvalidOperationException($"Homing is disabled for axis {axis}.");
        }

        Execute(() =>
        {
            SelectCard();
            AxisEngineeringConfig engineering = axisConfig.ToEngineeringConfig();
            int homePosition = ToIntPosition(AxisEngineeringConverter.ToNativePosition(axisConfig.Home.Position, engineering));
            double homeVelocity = AxisEngineeringConverter.ToNativeVelocity(axisConfig.Home.Velocity, engineering);
            double homeAcceleration = AxisEngineeringConverter.ToNativeAcceleration(axisConfig.Home.Acceleration, engineering);
            int homeOffset = ToIntPosition(AxisEngineeringConverter.ToNativePosition(axisConfig.Home.Offset, engineering));
            ThrowIfFailed(mc.GT_HomeInit(), "Initialize Googol home module failed");
            ThrowIfFailed(mc.GT_Home(axis, homePosition, homeVelocity, homeAcceleration, homeOffset), $"Home axis {axis} failed");
            homedAxes.Remove(axis);
            homingAxes.Add(axis);
        });
    }

    public AxisEngineeringConfig GetAxisEngineeringConfig(short axis)
    {
        ValidateAxis(axis);
        return config.GetAxisConfig(axis).ToEngineeringConfig();
    }

    public override HomeStatus GetHomeStatus(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_HomeSts(axis, out ushort rawStatus), $"Get home status axis {axis} failed");

            HomeState state = rawStatus switch
            {
                0 when homingAxes.Contains(axis) => HomeState.Succeeded,
                0 when homedAxes.Contains(axis) => HomeState.Succeeded,
                0 => HomeState.Idle,
                1 => HomeState.Running,
                2 => HomeState.Succeeded,
                _ => HomeState.Failed
            };

            if (state is HomeState.Succeeded or HomeState.Failed)
            {
                homingAxes.Remove(axis);
                if (state == HomeState.Succeeded)
                {
                    homedAxes.Add(axis);
                }
                else
                {
                    homedAxes.Remove(axis);
                }
            }

            return new HomeStatus(axis, state, rawStatus,
                state == HomeState.Failed ? $"Axis {axis} homing failed. RawStatus={rawStatus}." : null);
        });
    }

    public void InitCoordinateSystem(short crdIndex, short[] axes)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ArgumentNullException.ThrowIfNull(axes);
        if (axes.Length is < 2 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(axes), axes.Length, "Coordinate system supports 2 to 4 axes.");
        }

        foreach (short axis in axes)
        {
            ValidateAxis(axis);
        }

        GoogolCoordinateSystemConfig coordinateConfig = config.GetCoordinateSystemConfig(crdIndex)
            ?? new GoogolCoordinateSystemConfig { CoordinateSystem = crdIndex, Axes = axes.ToArray() };
        if (!coordinateConfig.Axes.SequenceEqual(axes))
        {
            throw new ArgumentException(
                $"Coordinate system {crdIndex} is configured for axes [{string.Join(", ", coordinateConfig.Axes)}], not [{string.Join(", ", axes)}].",
                nameof(axes));
        }

        Execute(() =>
        {
            SelectCard();
            var prm = new mc.TCrdPrm
            {
                dimension = (short)axes.Length,
                profile1 = axes[0],
                profile2 = axes.Length > 1 ? axes[1] : (short)0,
                profile3 = axes.Length > 2 ? axes[2] : (short)0,
                profile4 = axes.Length > 3 ? axes[3] : (short)0,
                synVelMax = ToCoordinateVelocity(coordinateConfig.MaximumVelocity, axes),
                synAccMax = ToCoordinateAcceleration(coordinateConfig.MaximumAcceleration, axes),
                evenTime = coordinateConfig.SmoothingTime
            };

            ThrowIfFailed(mc.GT_SetCrdPrm(crdIndex, ref prm), $"Set coordinate {crdIndex} parameters failed");
            ThrowIfFailed(mc.GT_CrdClear(crdIndex, 0), $"Clear coordinate {crdIndex} FIFO failed");
            coordinateAxes[crdIndex] = axes.ToArray();
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

        Execute(() =>
        {
            SelectCard();
            int[] nativePositions = positions
                .Select((position, index) => ToIntPosition(AxisEngineeringConverter.ToNativePosition(position, GetAxisEngineeringConfig(axes[index]))))
                .ToArray();
            double nativeVelocity = ToCoordinateVelocity(velocity, axes);
            double nativeAcceleration = ToCoordinateAcceleration(acc, axes);
            short result = nativePositions.Length switch
            {
                2 => mc.GT_LnXY(crdIndex, nativePositions[0], nativePositions[1], nativeVelocity, nativeAcceleration, 0, 0),
                3 => mc.GT_LnXYZ(crdIndex, nativePositions[0], nativePositions[1], nativePositions[2], nativeVelocity, nativeAcceleration, 0, 0),
                4 => mc.GT_LnXYZA(crdIndex, nativePositions[0], nativePositions[1], nativePositions[2], nativePositions[3], nativeVelocity, nativeAcceleration, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(positions), positions.Length, "Linear interpolation supports 2 to 4 positions.")
            };

            ThrowIfFailed(result, $"Add linear interpolation to coordinate {crdIndex} failed");
        });
    }

    public void MoveArc(short crdIndex, double x, double y, double xCenter, double yCenter, short dir, double velocity, double acc)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        ValidateVelocity(velocity, nameof(velocity));
        short[] axes = GetInitializedCoordinateAxes(crdIndex, 2);
        ValidateCoordinateMotion(axes, new[] { x, y }, velocity, acc);

        Execute(() =>
        {
            SelectCard();
            AxisEngineeringConfig xEngineering = GetAxisEngineeringConfig(axes[0]);
            AxisEngineeringConfig yEngineering = GetAxisEngineeringConfig(axes[1]);
            ThrowIfFailed(
                mc.GT_ArcXYC(
                    crdIndex,
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(x, xEngineering)),
                    ToIntPosition(AxisEngineeringConverter.ToNativePosition(y, yEngineering)),
                    AxisEngineeringConverter.ToNativePosition(xCenter, xEngineering),
                    AxisEngineeringConverter.ToNativePosition(yCenter, yEngineering),
                    dir,
                    ToCoordinateVelocity(velocity, axes),
                    ToCoordinateAcceleration(acc, axes),
                    0,
                    0),
                $"Add arc interpolation to coordinate {crdIndex} failed");
        });
    }

    public void StartInterpolation(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_CrdStart(CoordinateMask(crdIndex), 0), $"Start coordinate {crdIndex} failed");
        });
    }

    public void StopCoordinateSystem(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_CrdStart(CoordinateMask(crdIndex), 1), $"Stop coordinate {crdIndex} failed");
        });
    }

    public override double GetPosition(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_GetPrfPos(axis, out double position, 1, out _), $"Get profile position axis {axis} failed");
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
            ThrowIfFailed(mc.GT_GetEncPos(axis, out double position, 1, out _), $"Get encoder position axis {axis} failed");
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
            ThrowIfFailed(mc.GT_GetPrfVel(axis, out double velocity, 1, out _), $"Get profile velocity axis {axis} failed");
            return AxisEngineeringConverter.FromNativeVelocity(velocity, GetAxisEngineeringConfig(axis));
        });
    }

    public override int GetStatus(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_GetSts(axis, out int status, 1, out _), $"Get status axis {axis} failed");
            return status;
        });
    }

    public override bool IsMoving(short axis)
    {
        return (GetStatus(axis) & 0x400) != 0;
    }

    public bool IsCrdMoving(short crdIndex)
    {
        EnsureReady();
        ValidateCoordinate(crdIndex);
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_CrdStatus(crdIndex, out short run, out _, 0), $"Get coordinate {crdIndex} status failed");
            return run != 0;
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
                    .Select((snapshot, index) => Math.Abs(snapshot.Position - targetPositions[index]) <= tolerance)
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

    public override bool IsPositiveLimit(short axis) => (GetStatus(axis) & 0x20) != 0;

    public override bool IsNegativeLimit(short axis) => (GetStatus(axis) & 0x40) != 0;

    public override bool IsAlarm(short axis) => (GetStatus(axis) & 0x02) != 0;

    public override MotionAxisSnapshot GetAxisSnapshot(short axis)
    {
        EnsureReady();
        ValidateAxis(axis);
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_GetPrfPos(axis, out double nativePosition, 1, out _), $"Get profile position axis {axis} failed");
            ThrowIfFailed(mc.GT_GetEncPos(axis, out double nativeEncoderPosition, 1, out _), $"Get encoder position axis {axis} failed");
            ThrowIfFailed(mc.GT_GetPrfVel(axis, out double nativeVelocity, 1, out _), $"Get profile velocity axis {axis} failed");
            ThrowIfFailed(mc.GT_GetSts(axis, out int status, 1, out _), $"Get status axis {axis} failed");
            ThrowIfFailed(mc.GT_HomeSts(axis, out ushort rawHomeStatus), $"Get home status axis {axis} failed");

            HomeState homeState = rawHomeStatus switch
            {
                0 when homingAxes.Contains(axis) => HomeState.Succeeded,
                0 when homedAxes.Contains(axis) => HomeState.Succeeded,
                0 => HomeState.Idle,
                1 => HomeState.Running,
                2 => HomeState.Succeeded,
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

            AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
            return new MotionAxisSnapshot(
                axis,
                AxisEngineeringConverter.FromNativePosition(nativePosition, engineering),
                AxisEngineeringConverter.FromNativePosition(nativeEncoderPosition, engineering),
                AxisEngineeringConverter.FromNativeVelocity(nativeVelocity, engineering),
                status,
                (status & 0x400) != 0,
                (status & 0x02) != 0,
                (status & 0x20) != 0,
                (status & 0x40) != 0,
                DateTimeOffset.Now,
                (status & 0x200) != 0,
                homeState);
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
                ThrowIfFailed(mc.GT_GetPrfPos(axis, out double nativePosition, 1, out _), $"Read planned position axis {axis} failed");
                ThrowIfFailed(mc.GT_GetEncPos(axis, out double nativeEncoderPosition, 1, out _), $"Read encoder position axis {axis} failed");
                ThrowIfFailed(mc.GT_GetPrfVel(axis, out double nativeVelocity, 1, out _), $"Read planned velocity axis {axis} failed");
                ThrowIfFailed(mc.GT_GetSts(axis, out int status, 1, out _), $"Read status axis {axis} failed");
                ThrowIfFailed(mc.GT_HomeSts(axis, out ushort rawHomeStatus), $"Read home status axis {axis} failed");

                HomeState homeState = rawHomeStatus switch
                {
                    0 when homingAxes.Contains(axis) => HomeState.Succeeded,
                    0 when homedAxes.Contains(axis) => HomeState.Succeeded,
                    0 => HomeState.Idle,
                    1 => HomeState.Running,
                    2 => HomeState.Succeeded,
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

                AxisEngineeringConfig engineering = GetAxisEngineeringConfig(axis);
                destination[i] = new MotionAxisSnapshot(
                    axis,
                    AxisEngineeringConverter.FromNativePosition(nativePosition, engineering),
                    AxisEngineeringConverter.FromNativePosition(nativeEncoderPosition, engineering),
                    AxisEngineeringConverter.FromNativeVelocity(nativeVelocity, engineering),
                    status,
                    (status & 0x400) != 0,
                    (status & 0x02) != 0,
                    (status & 0x20) != 0,
                    (status & 0x40) != 0,
                    DateTimeOffset.Now,
                    (status & 0x200) != 0,
                    homeState);
            }

        });
    }

    public override async Task<HomeStatus> WaitForHomeCompletedAsync(short axis, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.WaitForHomeCompletedAsync(axis, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Stop(axis);
            throw;
        }
    }

    public override async Task WaitForHomeCompletedAsync(short axis, CancellationToken cancellationToken = default)
        => await WaitForHomeCompletedAsync(axis, config.GetAxisConfig(axis).Home.Timeout, cancellationToken).ConfigureAwait(false);

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

    public void SetDoName(int channel, string name)
    {
        IoChannelGuard.ValidateChannel(channel, config.DoChannelCount, nameof(channel));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("DO name cannot be empty.", nameof(name));
        }

        doNames[channel] = name;
    }

    public IEnumerable<(int Index, string Name)> GetAllOutputs() => doNames.Select(pair => (pair.Key, pair.Value));

    public void SetDiName(int channel, string name)
    {
        IoChannelGuard.ValidateChannel(channel, config.DiChannelCount, nameof(channel));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("DI name cannot be empty.", nameof(name));
        }

        diNames[channel] = name;
    }

    public IEnumerable<(int Index, string Name)> GetAllInputs() => diNames.Select(pair => (pair.Key, pair.Value));

    public void WriteDoBit(int channel, bool state)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, config.DoChannelCount, nameof(channel));
        Execute(() =>
        {
            SelectCard();
            short physical = ToPhysicalIoValue(state);
            ThrowIfFailed(mc.GT_SetDoBit(mc.MC_GPO, ToGtsIoIndex(channel), physical), $"Write GPO channel {channel} failed");
        });
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
            ThrowIfFailed(mc.GT_GetDo(mc.MC_GPO, out int currentValue), "Read current GPO state before mask write failed");
            uint targetValue = unchecked((uint)currentValue);

            for (int channel = 0; channel < config.DoChannelCount; channel++)
            {
                ulong bitMask = 1UL << channel;
                if ((changedMask & bitMask) == 0)
                {
                    continue;
                }

                bool logicalState = (mask & bitMask) != 0;
                short physical = ToPhysicalIoValue(logicalState);
                uint gtsBit = 1u << channel;
                targetValue = physical == 0 ? targetValue & ~gtsBit : targetValue | gtsBit;
            }

            if (targetValue != unchecked((uint)currentValue))
            {
                ThrowIfFailed(mc.GT_SetDo(mc.MC_GPO, unchecked((int)targetValue)), "Write GPO mask failed");
            }
        });
    }

    public bool[] ReadAllDo()
    {
        EnsureReady();
        return ToLogicalBits(ReadRawDoValue(), config.DoChannelCount);
    }

    public bool ReadDiBit(int channel)
    {
        EnsureReady();
        IoChannelGuard.ValidateChannel(channel, config.DiChannelCount, nameof(channel));
        uint raw = ReadRawDiValue();
        return ToLogicalIoState((raw & (1u << channel)) != 0);
    }

    public bool[] ReadAllDi()
    {
        EnsureReady();
        return ToLogicalBits(ReadRawDiValue(), config.DiChannelCount);
    }

    public ulong ReadDiPortMask()
    {
        EnsureReady();
        var bits = ReadAllDi();
        ulong mask = 0;
        for (int channel = 0; channel < bits.Length; channel++)
        {
            if (bits[channel])
            {
                mask |= 1UL << channel;
            }
        }

        return mask;
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
            ThrowIfFailed(mc.GT_CompareStop(), "Stop compare module failed");
            var pulses = triggerPositions.Select(position => ToIntPosition(position * pulseScale)).ToArray();
            ThrowIfFailed(mc.GT_CompareData(axis, 1, 0, 0, pulseWidthUs, ref pulses[0], (short)pulses.Length, ref pulses[0], 0), $"Enable PSO axis {axis} failed");
        });
    }

    public void DisablePso()
    {
        EnsureReady();
        Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_CompareStop(), "Disable PSO failed");
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

    private readonly Dictionary<int, string> doNames = new();
    private readonly Dictionary<int, string> diNames = new();

    private uint ReadRawDiValue()
    {
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_GetDi(mc.MC_GPI, out int value), "Read GPI failed");
            return unchecked((uint)value);
        });
    }

    private uint ReadRawDoValue()
    {
        return Execute(() =>
        {
            SelectCard();
            ThrowIfFailed(mc.GT_GetDo(mc.MC_GPO, out int value), "Read GPO failed");
            return unchecked((uint)value);
        });
    }

    private bool[] ToLogicalBits(uint rawValue, int channelCount)
    {
        var bits = new bool[IoBitConverter.DefaultChannelCount];
        for (int channel = 0; channel < channelCount; channel++)
        {
            bits[channel] = ToLogicalIoState((rawValue & (1u << channel)) != 0);
        }

        return bits;
    }

    private bool ToLogicalIoState(bool physicalHigh)
    {
        return config.DigitalIoActiveLow ? !physicalHigh : physicalHigh;
    }

    private short ToPhysicalIoValue(bool logicalState)
    {
        bool physicalHigh = config.DigitalIoActiveLow ? !logicalState : logicalState;
        return (short)(physicalHigh ? 1 : 0);
    }

    private void CancelPulseTasks()
    {
        pulseScheduler.CancelAll();
    }

    private void CloseSdkNoThrow()
    {
        if (sdkOpened)
        {
            try
            {
                mc.GT_Close();
            }
            catch
            {
                // Preserve the original connection exception.
            }
        }

        sdkOpened = false;
        connected = false;
        homingAxes.Clear();
        homedAxes.Clear();
        coordinateAxes.Clear();
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
        ThrowIfFailed(mc.GT_SetCardNo(config.CardNo), $"Select Googol card {config.CardNo} failed");
    }

    private void EnsureReady()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new InvalidOperationException("Googol motion card is not connected.");
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
        if (crdIndex is < 1 or > mc.CRD_MAX)
        {
            throw new ArgumentOutOfRangeException(nameof(crdIndex), crdIndex, $"Coordinate index must be between 1 and {mc.CRD_MAX}.");
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
        GoogolAxisConfig axisConfig = config.GetAxisConfig(axis);
        if (axisConfig.MinimumPosition is double minimum && position < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"Axis {axis} position must be greater than or equal to {minimum}.");
        }

        if (axisConfig.MaximumPosition is double maximum && position > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, $"Axis {axis} position must be less than or equal to {maximum}.");
        }

        ValidateAxisVelocity(axis, velocity);
        ValidateMaximum(axis, acceleration, axisConfig.MaximumAcceleration, nameof(acceleration));
        ValidateMaximum(axis, deceleration, axisConfig.MaximumDeceleration, nameof(deceleration));
    }

    private void ValidateAxisVelocity(short axis, double velocity)
    {
        GoogolAxisConfig axisConfig = config.GetAxisConfig(axis);
        ValidateMaximum(axis, velocity, axisConfig.MaximumVelocity, nameof(velocity));
    }

    private static void ValidateMaximum(short axis, double value, double? maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Axis {axis} {parameterName} must be finite and greater than 0.");
        }

        if (maximum is double limit && value > limit)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Axis {axis} {parameterName} must not exceed {limit}.");
        }
    }

    private void ValidateCoordinateMotion(short[] axes, double[] positions, double velocity, double acceleration)
    {
        for (int index = 0; index < axes.Length; index++)
        {
            GoogolAxisConfig axisConfig = config.GetAxisConfig(axes[index]);
            double position = positions[index];
            if (axisConfig.MinimumPosition is double minimum && position < minimum
                || axisConfig.MaximumPosition is double maximum && position > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(positions), position, $"Axis {axes[index]} interpolation target is outside configured travel limits.");
            }

            ValidateAxisVelocity(axes[index], velocity);
            ValidateMaximum(axes[index], acceleration, axisConfig.MaximumAcceleration, nameof(acceleration));
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
        int positiveNative = ToIntPosition(AxisEngineeringConverter.ToNativePosition(positive, engineering));
        int negativeNative = ToIntPosition(AxisEngineeringConverter.ToNativePosition(negative, engineering));
        ThrowIfFailed(
            mc.GT_SetSoftLimit(axis, Math.Max(positiveNative, negativeNative), Math.Min(positiveNative, negativeNative)),
            $"Set soft limit axis {axis} failed");
    }

    private static int AxisMask(short axis) => 1 << (axis - 1);

    private static short CoordinateMask(short crdIndex) => (short)(1 << (crdIndex - 1));

    private static short ToGtsIoIndex(int zeroBasedChannel) => (short)(zeroBasedChannel + 1);

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
}
