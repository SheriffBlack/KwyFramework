namespace KwyTemplate.Contracts.Services;

/// <summary>
/// Provides the application-level policy that determines whether the main window may close.
/// </summary>
public interface IApplicationCloseGuard
{
    Task<bool> CanCloseAsync();
}
