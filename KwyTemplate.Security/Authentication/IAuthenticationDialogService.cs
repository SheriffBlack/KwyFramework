using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authentication;

public interface IAuthenticationDialogService
{
    Task<CurrentUser?> ShowLoginAsync(CancellationToken cancellationToken = default);
}

