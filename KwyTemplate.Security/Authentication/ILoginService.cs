namespace KwyTemplate.Security.Authentication;

public interface ILoginService
{
    Task<LoginResult> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);

    void Logout();
}

