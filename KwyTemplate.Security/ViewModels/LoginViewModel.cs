using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Data;
using System.Collections.ObjectModel;

namespace KwyTemplate.Security.ViewModels;

public sealed class LoginViewModel : BindableBase, IDialogAware
{
    private readonly ILoginService loginService;
    private readonly LocalUserStore userStore;

    public LoginViewModel(
        ILoginService loginService,
        LocalUserStore userStore)
    {
        this.loginService = loginService ?? throw new ArgumentNullException(nameof(loginService));
        this.userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
    }

    public string Title => "用户登录";

    public event Action<IDialogResult>? RequestClose;

    public ObservableCollection<string> UserNames { get; } = new();

    private string userName = "admin";

    public string UserName
    {
        get => userName;
        set => SetProperty(ref userName, value);
    }

    private string password = string.Empty;

    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    private string message = "默认账号：admin / admin123";

    public string Message
    {
        get => message;
        private set => SetProperty(ref message, value);
    }

    private AsyncDelegateCommand? loginCommand;

    public AsyncDelegateCommand LoginCommand
        => loginCommand ??= new AsyncDelegateCommand(ExecuteLoginAsync);

    private async Task ExecuteLoginAsync()
    {
        Message = string.Empty;
        LoginResult result = await loginService.LoginAsync(UserName, Password, DestroyToken).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            Message = result.ErrorMessage ?? "登录失败。";
            return;
        }

        Password = string.Empty;
        RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
    }

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
        Password = string.Empty;
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        _ = LoadUserNamesAsync();
    }

    private async Task LoadUserNamesAsync()
    {
        IReadOnlyList<string> users = await userStore.GetUserNamesAsync(DestroyToken).ConfigureAwait(true);
        UserNames.Clear();
        foreach (string user in users)
        {
            UserNames.Add(user);
        }

        if (UserNames.Count > 0
            && (string.IsNullOrWhiteSpace(UserName) || !UserNames.Contains(UserName)))
        {
            UserName = UserNames[0];
        }
    }
}
