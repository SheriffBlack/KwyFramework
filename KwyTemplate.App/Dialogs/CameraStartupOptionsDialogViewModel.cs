using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Dialogs;

internal static class CameraStartupOptionsDialogParameterNames
{
    public const string Options = nameof(CameraStartupOptions);
}

internal sealed record CameraStartupOptionsDialogRequest(string Title, string Message, string CameraALabel, string CameraBLabel, string ConfirmButtonText, string CancelButtonText, bool IsCameraAEnabled, bool IsCameraBEnabled);

internal sealed class CameraStartupOptionsDialogViewModel : BindableBase, IDialogAware
{
    private string title = string.Empty;
    private string message = string.Empty;
    private string cameraALabel = string.Empty;
    private string cameraBLabel = string.Empty;
    private string confirmButtonText = string.Empty;
    private string cancelButtonText = string.Empty;
    private bool isCameraAEnabled;
    private bool isCameraBEnabled;

    public string Title { get => title; private set => SetProperty(ref title, value); }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public string CameraALabel { get => cameraALabel; private set => SetProperty(ref cameraALabel, value); }
    public string CameraBLabel { get => cameraBLabel; private set => SetProperty(ref cameraBLabel, value); }
    public string ConfirmButtonText { get => confirmButtonText; private set => SetProperty(ref confirmButtonText, value); }
    public string CancelButtonText { get => cancelButtonText; private set => SetProperty(ref cancelButtonText, value); }
    public bool IsCameraAEnabled { get => isCameraAEnabled; set => SetProperty(ref isCameraAEnabled, value); }
    public bool IsCameraBEnabled { get => isCameraBEnabled; set => SetProperty(ref isCameraBEnabled, value); }

    public event Action<IDialogResult>? RequestClose;
    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand CancelCommand { get; }

    public CameraStartupOptionsDialogViewModel()
    {
        ConfirmCommand = new DelegateCommand(() => Close(ButtonResult.OK));
        CancelCommand = new DelegateCommand(() => Close(ButtonResult.Cancel));
    }

    public bool CanCloseDialog() => true;
    public void OnDialogClosed() { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        CameraStartupOptionsDialogRequest request = parameters.GetValueOrDefault<CameraStartupOptionsDialogRequest>()
            ?? throw new InvalidOperationException("Camera startup dialog request is missing.");
        Title = request.Title;
        Message = request.Message;
        CameraALabel = request.CameraALabel;
        CameraBLabel = request.CameraBLabel;
        ConfirmButtonText = request.ConfirmButtonText;
        CancelButtonText = request.CancelButtonText;
        IsCameraAEnabled = request.IsCameraAEnabled;
        IsCameraBEnabled = request.IsCameraBEnabled;
    }

    private void Close(ButtonResult result)
    {
        var parameters = new DialogParameters().AddValue(new CameraStartupOptions(IsCameraAEnabled, IsCameraBEnabled), CameraStartupOptionsDialogParameterNames.Options);
        RequestClose?.Invoke(new DialogResult(result, parameters));
    }
}
