using KwyTemplate.App.Runtime;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.Machines;
using KwyTemplate.MES.Abstract.Events;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// MES 连接 Feature。
/// App 启动后主动连接 MES，并把连接状态写入 MesConnectionStatus，供 HomeView 显示在线/离线状态。
/// </summary>
public sealed class MesConnectionFeature : IMachineRuntimeFeature
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);
    private readonly IMesConnection mesConnection;
    private readonly MesConnectionStatus status;
    private readonly StartupProgressService startupProgress;
    private readonly ILocalizationService localizationService;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private bool disposed;

    public MesConnectionFeature(
        IMesConnection mesConnection,
        MesConnectionStatus status,
        StartupProgressService startupProgress,
        ILocalizationService localizationService)
    {
        this.mesConnection = mesConnection ?? throw new ArgumentNullException(nameof(mesConnection));
        this.status = status ?? throw new ArgumentNullException(nameof(status));
        this.startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.mesConnection.StateChanged += OnMesStateChanged;
    }

    public bool CanAttach(MachineBase machine) => true;

    public void Start(MachineBase machine)
    {
        if (disposed || worker is { IsCompleted: false })
        {
            return;
        }

        status.State = mesConnection.State;
        stopCts = new CancellationTokenSource();
        worker = ConnectAsync(stopCts.Token);
    }

    public void Stop()
    {
        CancellationTokenSource? cts = stopCts;
        Task? runningWorker = worker;
        stopCts = null;
        worker = null;

        if (cts != null)
        {
            cts.Cancel();
            try
            {
                runningWorker?.Wait(StopTimeout);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
            {
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        mesConnection.StateChanged -= OnMesStateChanged;
        Stop();
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            startupProgress.Report(localizationService.T("Startup.Mes.Connecting", "Connecting MES..."), 80);
            MesResult result = await mesConnection.ConnectAsync(cancellationToken).ConfigureAwait(false);
            status.Message = result.Message;
            status.State = mesConnection.State;
            startupProgress.Report(result.IsSuccess
                ? localizationService.T("Startup.Mes.Connected", "MES connected.")
                : localizationService.T("Startup.Mes.ConnectionFailed", "MES connection failed."), 95);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            status.State = MesConnectionState.Faulted;
            status.Message = ex.Message;
            startupProgress.Report(localizationService.T("Startup.Mes.ConnectionAbnormal", "MES connection abnormal."), 95);
        }
    }

    private void OnMesStateChanged(object? sender, MesStateChangedEventArgs e)
    {
        status.State = e.State;
        status.Message = e.Message ?? string.Empty;
    }

}



