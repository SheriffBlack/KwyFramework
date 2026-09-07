using System.Windows;
using KwyTemplate.App.Runtime;
using KwyTemplate.Device;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Scanners;
using KwyTemplate.Flow.Machines;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// Cyntec Reel 扫码流程。
/// PLC 上升沿和 HomeView 手动按钮都复用这里，避免扫码枪、MES 调用和 UI 赋值逻辑分散。
/// </summary>
public sealed class CyntecReelScanWorkflow : ICyntecReelScanWorkflow
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim scanGate = new(1, 1);
    private readonly MachineBase machine;
    private readonly IMachineDeviceContext devices;
    private readonly IProductionContext productionContext;
    private readonly IMesReelService mesReelService;

    public CyntecReelScanWorkflow(
        MachineBase machine,
        IMachineDeviceContext devices,
        IProductionContext productionContext,
        IMesReelService mesReelService)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.mesReelService = mesReelService ?? throw new ArgumentNullException(nameof(mesReelService));
    }

    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!await scanGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            IBarcodeScannerDevice scanner = await GetScannerAsync(cancellationToken).ConfigureAwait(false);
            await scanner.TriggerScanAsync(cancellationToken).ConfigureAwait(false);
            string barcode = await scanner.WaitForCodeAsync(ScanTimeout, cancellationToken).ConfigureAwait(false);

            await UpdateUiAsync(() =>
            {
                productionContext.BarcodeContent = barcode;
                productionContext.ReelScanState = ReelScanState.None;
            }).ConfigureAwait(false);

            MesResult<MesReelScanResult> result = await mesReelService.ScanReelAsync(
                new MesReelScanRequest(
                    CreateMesContext(),
                    productionContext.WorkOrderNo,
                    barcode,
                    barcode),
                cancellationToken).ConfigureAwait(false);

            await ApplyMesResultAsync(result).ConfigureAwait(false);
        }
        catch
        {
            await UpdateUiAsync(() => productionContext.ReelScanState = ReelScanState.Failure).ConfigureAwait(false);
            throw;
        }
        finally
        {
            scanGate.Release();
        }
    }

    private async Task<IBarcodeScannerDevice> GetScannerAsync(CancellationToken cancellationToken)
    {
        if (!devices.TryGet(DeviceIds.MainScanner, out IBarcodeScannerDevice? scanner) || scanner == null)
        {
            throw new InvalidOperationException("未找到 Reel 扫码枪设备。");
        }

        if (!scanner.IsConnected)
        {
            await scanner.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        return scanner;
    }

    private MesRequestContext CreateMesContext()
        => new(
            string.IsNullOrWhiteSpace(productionContext.EquipmentNo) ? machine.MachineId : productionContext.EquipmentNo,
            machine.MachineName,
            productionContext.OperatorNo,
            WorkOrderNo: productionContext.WorkOrderNo);

    private Task ApplyMesResultAsync(MesResult<MesReelScanResult> result)
        => UpdateUiAsync(() =>
        {
            if (result.Exchange?.ReturnCode == 0 || result.IsSuccess)
            {
                MesReelScanResult? data = result.Data;
                productionContext.ReelTpNo = data?.ReelId ?? string.Empty;
                productionContext.ReelWorkOrderNo = data?.MatNo ?? string.Empty;
                productionContext.ReelId = data?.TpNo ?? string.Empty;
                productionContext.ReelScanState = ReelScanState.Success;
            }
            else
            {
                productionContext.ReelScanState = ReelScanState.Failure;
            }
        });

    private static Task UpdateUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }
}