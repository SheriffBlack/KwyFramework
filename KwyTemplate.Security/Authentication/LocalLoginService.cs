using KwyTemplate.Contracts.Localization;
using KwyTemplate.Security.Data;
using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authentication;

internal sealed class LocalLoginService : ILoginService
{
    private readonly LocalUserStore userStore;
    private readonly PasswordHasher passwordHasher;
    private readonly ICurrentUserService currentUserService;
    private readonly ILocalizationService localizationService;

    public LocalLoginService(
        LocalUserStore userStore,
        PasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        ILocalizationService localizationService)
    {
        this.userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task<LoginResult> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return LoginResult.Failed(localizationService.T("Security.Login.UserNameRequired", "请输入用户名。"));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failed(localizationService.T("Security.Login.PasswordRequired", "请输入密码。"));
        }

        LocalUser? user = await userStore.FindByUserNameAsync(userName, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return LoginResult.Failed(localizationService.T("Security.Login.InvalidCredential", "用户名或密码错误。"));
        }

        if (!user.IsEnabled)
        {
            return LoginResult.Failed(localizationService.T("Security.Login.AccountDisabled", "账号已禁用。"));
        }

        if (!passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return LoginResult.Failed(localizationService.T("Security.Login.InvalidCredential", "用户名或密码错误。"));
        }

        var currentUser = new CurrentUser(user.Id, user.UserName, user.DisplayName, user.Level);
        currentUserService.SignIn(currentUser);
        return LoginResult.Success(currentUser);
    }

    public void Logout()
    {
        currentUserService.SignOut();
    }

}
