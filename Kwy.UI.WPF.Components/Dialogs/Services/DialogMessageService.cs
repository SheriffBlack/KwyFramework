using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Components;

namespace Kwy.UI.WPF.Components.Dialogs;

internal sealed class DialogMessageService : IDialogMessageService
{
    private const string DialogViewName = nameof(DialogMessageView);
    private readonly IDialogService dialogService;

    public DialogMessageService(IDialogService dialogService)
    {
        this.dialogService = dialogService;
    }

    public async Task<ButtonResult> ShowAsync(string message, DialogMessageOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        options ??= new DialogMessageOptions();

        var parameters = new DialogParameters()
            .AddValue(message, DialogMessageParameterNames.Message)
            .AddValue(options);

        var result = await dialogService.ShowDialogAsync(DialogViewName, parameters);
        return result.Result;
    }

    public async Task<bool> ShowMessageAsync(
        string message,
        DialogMessageIcon icon = DialogMessageIcon.None,
        string? title = null)
        => await ShowAsync(message, new DialogMessageOptions
        {
            Icon = icon,
            Title = title,
            ShowCancelButton = true
        }) == ButtonResult.OK;

    public async Task<bool> ShowConfirmAsync(string message, string? title = null)
        => await ShowAsync(message, new DialogMessageOptions
        {
            Icon = DialogMessageIcon.Question,
            Title = title ?? "确认操作",
            ShowCancelButton = true
        }) == ButtonResult.OK;

    public async Task<bool> ShowWarningAsync(string message, string? title = null)
        => await ShowAsync(message, new DialogMessageOptions
        {
            Icon = DialogMessageIcon.Warning,
            Title = title ?? "系统警告"
        }) == ButtonResult.OK;

    public async Task<bool> ShowErrorAsync(string message, string? title = null)
        => await ShowAsync(message, new DialogMessageOptions
        {
            Icon = DialogMessageIcon.Error,
            Title = title ?? "发生错误"
        }) == ButtonResult.OK;

    public async Task<bool> ShowInfoAsync(string message, string? title = null)
        => await ShowAsync(message, new DialogMessageOptions
        {
            Icon = DialogMessageIcon.Info,
            Title = title ?? "系统提示"
        }) == ButtonResult.OK;
}
