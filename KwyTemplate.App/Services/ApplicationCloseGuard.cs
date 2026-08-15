using Kwy.UI.WPF.Components.Dialogs;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Services;

/// <summary>
/// Centralizes application-close interlocks so every window-close entry follows the same policy.
/// </summary>
public sealed class ApplicationCloseGuard : IApplicationCloseGuard
{
    private readonly MachineBase machine;
    private readonly IDialogMessageService dialogMessageService;
    private readonly ILocalizationService localizationService;

    public ApplicationCloseGuard(
        MachineBase machine,
        IDialogMessageService dialogMessageService,
        ILocalizationService localizationService)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task<bool> CanCloseAsync()
    {
        if (machine.ProductionState == MachineProductionState.Stopped)
        {
            try
            {
                await machine.SetCheckCompletedAsync(false).ConfigureAwait(true);
                return true;
            }
            catch (Exception ex)
            {
                await dialogMessageService.ShowErrorAsync(
                    localizationService.T("Shell.Message.ExitCheckResetFailed", "无法复位 PLC 点检完成信号，程序不能关闭。") + Environment.NewLine + ex.Message,
                    localizationService.T("Shell.Title.ExitBlocked", "关闭程序")).ConfigureAwait(true);
                return false;
            }
        }

        await dialogMessageService.ShowWarningAsync(
            localizationService.T("Shell.Message.ExitRequiresStopped", "程序非停止状态，禁止关闭"),
            localizationService.T("Shell.Title.ExitBlocked", "关闭程序")).ConfigureAwait(true);
        return false;
    }

}
