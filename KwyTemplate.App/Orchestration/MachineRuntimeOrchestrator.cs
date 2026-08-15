using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 机台运行时特性编排器。
/// App 启动后统一启动所有适用于当前机型的后台 Feature，App 退出或释放时统一停止。
/// Feature 只承载和页面无关、但需要随机台生命周期常驻的业务，例如 PLC 停机信号、工控机在线、Reel 扫码。
/// </summary>
public sealed class MachineRuntimeOrchestrator : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    private readonly MachineBase machine;
    private readonly IEnumerable<IMachineRuntimeFeature> features;
    private readonly List<IMachineRuntimeFeature> activeFeatures = [];
    private bool disposed;

    public MachineRuntimeOrchestrator(MachineBase machine, IEnumerable<IMachineRuntimeFeature> features)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.features = features ?? throw new ArgumentNullException(nameof(features));
    }

    /// <summary>
    /// 启动当前 Machine 支持的所有运行时 Feature。
    /// 每个 Feature 通过 CanAttach 判断自己是否适用于当前机型，避免在这里写具体机型判断。
    /// </summary>
    public void Start()
    {
        if (disposed || activeFeatures.Count > 0)
        {
            return;
        }

        machine.StartRuntimeAsync().GetAwaiter().GetResult();

        foreach (IMachineRuntimeFeature feature in features)
        {
            if (!feature.CanAttach(machine))
            {
                continue;
            }

            feature.Start(machine);
            activeFeatures.Add(feature);
        }
    }

    /// <summary>
    /// 停止已经启动的 Feature。程序退出时会走到这里，用于释放轮询任务并复位必要信号。
    /// </summary>
    public void Stop()
    {
        // 工控机在线等安全下线信号必须先复位，不能被后续最长 5 秒的工单信号复位阻塞。
        foreach (IMachineRuntimeFeature feature in activeFeatures.Where(static feature => feature.ShutdownOrder < 0).OrderBy(static feature => feature.ShutdownOrder))
        {
            feature.Stop();
        }

        if (machine is IMachineWorkOrderStartSignalMachine workOrderStartSignalMachine)
        {
            try
            {
                workOrderStartSignalMachine.ResetWorkOrderStartSignalsAsync().Wait(StopTimeout);
            }
            catch
            {
            }
        }
        foreach (IMachineRuntimeFeature feature in activeFeatures.Where(static feature => feature.ShutdownOrder >= 0).OrderBy(static feature => feature.ShutdownOrder))
        {
            feature.Stop();
        }

        activeFeatures.Clear();

        try
        {
            machine.StopRuntimeAsync().Wait(StopTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
    }
}

