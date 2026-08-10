using System.Globalization;
using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;

namespace Kwy.UI.WPF.Components.Dialogs;

internal sealed class InputDialogService : IInputDialogService
{
    private const string DialogViewName = nameof(InputDialogView);
    private readonly IDialogService dialogService;

    public InputDialogService(IDialogService dialogService)
    {
        this.dialogService = dialogService;
    }

    public async Task<InputDialogResult> ShowAsync(InputDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parameters = new DialogParameters().AddValue(options);
        IDialogResult result = await dialogService.ShowDialogAsync(DialogViewName, parameters);
        string? value = result.Parameters.GetValueOrDefault<string>(InputDialogParameterNames.Value);
        return new InputDialogResult(result.Result, value);
    }

    public Task<InputDialogResult> ShowTextAsync(string message, string? title = null, string? defaultValue = null)
        => ShowAsync(new InputDialogOptions
        {
            Title = title ?? "输入",
            Message = message,
            DefaultValue = defaultValue,
            InputType = InputDialogType.Text
        });

    public Task<InputDialogResult> ShowNumberAsync(
        string message,
        string? title = null,
        decimal? defaultValue = null,
        decimal? minimum = null,
        decimal? maximum = null,
        string? unit = null)
        => ShowAsync(new InputDialogOptions
        {
            Title = title ?? "数值输入",
            Message = message,
            Label = "数值",
            DefaultValue = defaultValue?.ToString(CultureInfo.CurrentCulture),
            InputType = InputDialogType.Number,
            Minimum = minimum,
            Maximum = maximum,
            Unit = unit
        });
}
