using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using Kwy.UI;

namespace Kwy.UI.WPF.Components.Dialogs;

internal static class DialogMessageParameterNames
{
    public const string Message = nameof(DialogMessageViewModel.Message);
}

internal sealed class DialogMessageViewModel : BindableBase, IDialogAware
{
    private DialogMessageIcon dialogIcon;
    private string message = string.Empty;
    private string title = string.Empty;
    private bool showCancelButton;

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public DialogMessageIcon DialogIcon
    {
        get => dialogIcon;
        private set
        {
            if (SetProperty(ref dialogIcon, value))
            {
                RaisePropertyChanged(nameof(IconResource));
                RaisePropertyChanged(nameof(IsIconVisible));
            }
        }
    }

    public string Message
    {
        get => message;
        private set => SetProperty(ref message, value);
    }

    public bool ShowCancelButton
    {
        get => showCancelButton;
        private set => SetProperty(ref showCancelButton, value);
    }

    public string? IconResource => DialogIcon switch
    {
        DialogMessageIcon.Success => IconNames.IconSuccess,
        DialogMessageIcon.Info => IconNames.IconInfo,
        DialogMessageIcon.Error => IconNames.IconError,
        DialogMessageIcon.Question => IconNames.IconQuestion,
        DialogMessageIcon.Warning => IconNames.IconWarning,
        _ => null
    };

    public bool IsIconVisible => DialogIcon != DialogMessageIcon.None;

    public event Action<IDialogResult>? RequestClose;

    public DelegateCommand ConfirmCommand { get; }

    public DelegateCommand CancelCommand { get; }

    public DialogMessageViewModel()
    {
        ConfirmCommand = new DelegateCommand(() => Close(ButtonResult.OK));
        CancelCommand = new DelegateCommand(() => Close(ButtonResult.Cancel));
    }

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        var options = parameters.GetValueOrDefault<DialogMessageOptions>() ?? new DialogMessageOptions();

        Message = parameters.GetValueOrDefault<string>(DialogMessageParameterNames.Message) ?? string.Empty;
        Title = options.Title ?? string.Empty;
        DialogIcon = options.Icon;
        ShowCancelButton = options.ShowCancelButton;
    }

    private void Close(ButtonResult result)
        => RequestClose?.Invoke(new DialogResult(result));
}
