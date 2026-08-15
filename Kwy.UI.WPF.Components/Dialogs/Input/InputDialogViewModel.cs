using System.Globalization;
using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;

namespace Kwy.UI.WPF.Components.Dialogs;

internal static class InputDialogParameterNames
{
    public const string Value = nameof(InputDialogViewModel.InputText);
}

internal sealed class InputDialogViewModel : BindableBase, IDialogAware
{
    private InputDialogOptions options = new();
    private string title = string.Empty;
    private string message = string.Empty;
    private string label = "输入";
    private string inputText = string.Empty;
    private string validationMessage = string.Empty;
    private string confirmButtonText = "确定";
    private string cancelButtonText = "取消";
    private bool showCancelButton = true;
    private bool showContentTitle = true;
    private string unit = string.Empty;

    public string Title
    {
        get => title;
        private set
        {
            if (SetProperty(ref title, value))
            {
                RaisePropertyChanged(nameof(IsTitleVisible));
            }
        }
    }

    public bool IsTitleVisible => showContentTitle && !string.IsNullOrWhiteSpace(Title);

    public bool ShowContentTitle
    {
        get => showContentTitle;
        private set
        {
            if (SetProperty(ref showContentTitle, value))
            {
                RaisePropertyChanged(nameof(IsTitleVisible));
            }
        }
    }

    public string Message
    {
        get => message;
        private set
        {
            if (SetProperty(ref message, value))
            {
                RaisePropertyChanged(nameof(IsMessageVisible));
            }
        }
    }

    public bool IsMessageVisible => !string.IsNullOrWhiteSpace(Message);

    public string Label
    {
        get => label;
        private set => SetProperty(ref label, value);
    }

    public string InputText
    {
        get => inputText;
        set
        {
            if (SetProperty(ref inputText, value))
            {
                ValidationMessage = string.Empty;
            }
        }
    }

    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (SetProperty(ref validationMessage, value))
            {
                RaisePropertyChanged(nameof(IsValidationVisible));
            }
        }
    }

    public bool IsValidationVisible => !string.IsNullOrWhiteSpace(ValidationMessage);

    public string ConfirmButtonText
    {
        get => confirmButtonText;
        private set => SetProperty(ref confirmButtonText, value);
    }

    public string CancelButtonText
    {
        get => cancelButtonText;
        private set => SetProperty(ref cancelButtonText, value);
    }

    public bool ShowCancelButton
    {
        get => showCancelButton;
        private set => SetProperty(ref showCancelButton, value);
    }

    public string Unit
    {
        get => unit;
        private set
        {
            if (SetProperty(ref unit, value))
            {
                RaisePropertyChanged(nameof(IsUnitVisible));
            }
        }
    }

    public bool IsUnitVisible => !string.IsNullOrWhiteSpace(Unit);

    public event Action<IDialogResult>? RequestClose;

    public DelegateCommand ConfirmCommand { get; }

    public DelegateCommand CancelCommand { get; }

    public InputDialogViewModel()
    {
        ConfirmCommand = new DelegateCommand(Confirm);
        CancelCommand = new DelegateCommand(() => Close(ButtonResult.Cancel));
    }

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        options = parameters.GetValueOrDefault<InputDialogOptions>() ?? new InputDialogOptions();

        Title = options.Title ?? string.Empty;
        ShowContentTitle = options.ShowContentTitle;
        Message = options.Message ?? string.Empty;
        Label = string.IsNullOrWhiteSpace(options.Label) ? "输入" : options.Label;
        InputText = options.DefaultValue ?? string.Empty;
        ConfirmButtonText = string.IsNullOrWhiteSpace(options.ConfirmButtonText)
            ? GetResourceText("Common.Confirm", "确定")
            : options.ConfirmButtonText;
        CancelButtonText = string.IsNullOrWhiteSpace(options.CancelButtonText)
            ? GetResourceText("Common.Cancel", "取消")
            : options.CancelButtonText;
        ShowCancelButton = options.ShowCancelButton;
        Unit = options.Unit ?? string.Empty;
        ValidationMessage = string.Empty;
    }

    private void Confirm()
    {
        if (!ValidateInput())
        {
            return;
        }

        Close(ButtonResult.OK);
    }

    private bool ValidateInput()
    {
        if (options.InputType != InputDialogType.Number)
        {
            return true;
        }

        if (!TryParseDecimal(InputText, out decimal value))
        {
            ValidationMessage = "请输入有效数值。";
            return false;
        }

        if (options.Minimum.HasValue && value < options.Minimum.Value)
        {
            ValidationMessage = $"输入值不能小于 {options.Minimum.Value.ToString(CultureInfo.CurrentCulture)}。";
            return false;
        }

        if (options.Maximum.HasValue && value > options.Maximum.Value)
        {
            ValidationMessage = $"输入值不能大于 {options.Maximum.Value.ToString(CultureInfo.CurrentCulture)}。";
            return false;
        }

        InputText = value.ToString(CultureInfo.CurrentCulture);
        return true;
    }

    private static bool TryParseDecimal(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static string GetResourceText(string key, string fallback)
        => System.Windows.Application.Current?.TryFindResource(key) as string ?? fallback;

    private void Close(ButtonResult result)
    {
        var parameters = new DialogParameters().AddValue(InputText, InputDialogParameterNames.Value);
        RequestClose?.Invoke(new DialogResult(result, parameters));
    }
}

