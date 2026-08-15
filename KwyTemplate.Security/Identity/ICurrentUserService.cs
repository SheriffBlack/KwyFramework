namespace KwyTemplate.Security.Identity;

public interface ICurrentUserService
{
    CurrentUser? CurrentUser { get; }

    bool IsAuthenticated { get; }

    event EventHandler<CurrentUser?>? CurrentUserChanged;

    void SignIn(CurrentUser user);

    void SignOut();

    /// <summary>
    /// Restarts the elevated-user inactivity timeout after an authorized elevated operation.
    /// </summary>
    void RefreshElevatedSession();
}
