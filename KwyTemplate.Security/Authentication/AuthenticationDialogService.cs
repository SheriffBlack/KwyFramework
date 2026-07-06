using Kwy.MVVM.Dialogs;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authentication;

internal sealed class AuthenticationDialogService : IAuthenticationDialogService
{
    private readonly IDialogService dialogService;
    private readonly ICurrentUserService currentUserService;

    public AuthenticationDialogService(
        IDialogService dialogService,
        ICurrentUserService currentUserService)
    {
        this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        this.currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<CurrentUser?> ShowLoginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IDialogResult result = await dialogService.ShowDialogAsync(ViewNames.LoginView).ConfigureAwait(true);
        return result.Result == ButtonResult.OK
            ? currentUserService.CurrentUser
            : null;
    }
}
