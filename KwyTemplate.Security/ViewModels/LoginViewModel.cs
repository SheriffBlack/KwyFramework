using System.Collections.ObjectModel;
using Kwy.MVVM.Core;
using Kwy.MVVM.Dialogs;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Data;

namespace KwyTemplate.Security.ViewModels;

public sealed class LoginViewModel : BindableBase, IDialogAware
{
    private readonly ILoginService loginService;
    private readonly LocalUserStore userStore;
    private readonly ILocalizationService localizationService;
    private string userName = "operator";
    private string password = string.Empty;
    private string message;
    private bool isDefaultMessageVisible = true;
    private AsyncDelegateCommand? loginCommand;

    public LoginViewModel(
        ILoginService loginService,
        LocalUserStore userStore,
        ILocalizationService localizationService)
    {
        this.loginService = loginService ?? throw new ArgumentNullException(nameof(loginService));
        this.userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.localizationService.LanguageChanged += OnLanguageChanged;
        message = localizationService.T("Security.Login.DefaultAccount", "默认账号：操作员 / 1");
    }

    public string Title => localizationService.T("Security.Login.Title", "用户登录");

    public event Action<IDialogResult>? RequestClose;

    public ObservableCollection<string> UserNames { get; } = new();

    public string UserName
    {
        get => userName;
        set => SetProperty(ref userName, value);
    }

    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    public string Message
    {
        get => message;
        private set => SetProperty(ref message, value);
    }

    public AsyncDelegateCommand LoginCommand
        => loginCommand ??= new AsyncDelegateCommand(ExecuteLoginAsync);

    private async Task ExecuteLoginAsync()
    {
        try
        {
            isDefaultMessageVisible = false;
            Message = string.Empty;
            LoginResult result = await loginService.LoginAsync(UserName, Password, DestroyToken).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                Message = result.ErrorMessage ?? localizationService.T("Security.Login.Failed", "登录失败。");
                return;
            }

            Password = string.Empty;
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
        }
        catch (Exception ex)
        {
            isDefaultMessageVisible = false;
            Message = localizationService.TF("Security.Login.Exception", "登录异常：{0}", ex.Message);
        }
    }

    public bool CanCloseDialog() => true;

    public void OnDialogClosed()
    {
        Password = string.Empty;
        localizationService.LanguageChanged -= OnLanguageChanged;
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        _ = LoadUserNamesAsync();
    }

    private async Task LoadUserNamesAsync()
    {
        try
        {
            IReadOnlyList<string> users = await userStore.GetUserNamesAsync(DestroyToken).ConfigureAwait(true);
            UserNames.Clear();
            foreach (string user in users.OrderBy(GetUserDisplayOrder).ThenBy(static user => user, StringComparer.OrdinalIgnoreCase))
            {
                UserNames.Add(user);
            }

            string? defaultUser = UserNames.FirstOrDefault(static user => IsUser(user, "operator", "操作员"))
                ?? UserNames.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(defaultUser))
            {
                UserName = defaultUser;
            }
        }
        catch (Exception ex)
        {
            isDefaultMessageVisible = false;
            Message = localizationService.TF("Security.Login.LoadUsersFailed", "加载用户失败：{0}", ex.Message);
        }
    }

    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        RaisePropertyChanged(nameof(Title));
        if (isDefaultMessageVisible)
        {
            Message = localizationService.T("Security.Login.DefaultAccount", "默认账号：操作员 / 1");
        }
    }

    private static int GetUserDisplayOrder(string userName)
    {
        if (IsUser(userName, "operator", "操作员"))
        {
            return 0;
        }

        if (IsUser(userName, "engineer", "工程师"))
        {
            return 1;
        }

        if (IsUser(userName, "admin", "管理员"))
        {
            return 2;
        }

        return 100;
    }

    private static bool IsUser(string userName, string accountName, string displayName)
        => string.Equals(userName, accountName, StringComparison.OrdinalIgnoreCase)
           || string.Equals(userName, displayName, StringComparison.OrdinalIgnoreCase);
}
