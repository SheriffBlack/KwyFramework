using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Dialogs;

internal sealed class CameraStartupOptionsDialogService : ICameraStartupOptionsDialogService
{
    private readonly IDialogService dialogService;
    private readonly ILocalizationService localizationService;

    public CameraStartupOptionsDialogService(IDialogService dialogService, ILocalizationService localizationService)
    {
        this.dialogService = dialogService;
        this.localizationService = localizationService;
    }

    public async Task<CameraStartupOptions?> ShowAsync(bool isCameraAEnabled, bool isCameraBEnabled)
    {
        var request = new CameraStartupOptionsDialogRequest(
            localizationService.T("Home.Title.CameraStartup", "相机启用确认"),
            localizationService.T("Home.Message.CameraStartup", "请选择本次生产需要启用的相机工位。"),
            localizationService.T("Home.Field.CameraAEnabled", "A面相机开启"),
            localizationService.T("Home.Field.CameraBEnabled", "B面相机开启"),
            localizationService.T("Common.Confirm", "确定"),
            localizationService.T("Common.Cancel", "取消"),
            isCameraAEnabled,
            isCameraBEnabled);
        IDialogResult result = await dialogService.ShowDialogAsync(nameof(CameraStartupOptionsDialogView), new DialogParameters().AddValue(request));
        return result.Result == ButtonResult.OK
            ? result.Parameters.GetValueOrDefault<CameraStartupOptions>(CameraStartupOptionsDialogParameterNames.Options)
            : null;
    }
}
