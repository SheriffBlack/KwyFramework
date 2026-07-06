using KwyTemplate.Security.Options;

namespace KwyTemplate.Security.Identity;

internal sealed class CurrentUserService : ICurrentUserService, IDisposable
{
    private static readonly CurrentUser OperatorUser =
        new(0, "operator", "操作员", SecurityUserLevel.Operator);

    private readonly object syncRoot = new();
    private readonly SecuritySessionOptions sessionOptions;
    private CancellationTokenSource? sessionTimeoutCts;
    private CurrentUser currentUser = OperatorUser;
    private int sessionVersion;

    public CurrentUserService(SecuritySessionOptions sessionOptions)
    {
        this.sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
    }

    public CurrentUser? CurrentUser
    {
        get
        {
            lock (syncRoot)
            {
                return currentUser;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (syncRoot)
            {
                return currentUser.Level > SecurityUserLevel.Operator;
            }
        }
    }

    public event EventHandler<CurrentUser?>? CurrentUserChanged;

    public void SignIn(CurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        lock (syncRoot)
        {
            currentUser = user;
            sessionVersion++;
            ResetSessionTimeoutLocked();
            StartSessionTimeoutLocked(user, sessionVersion);
        }

        CurrentUserChanged?.Invoke(this, user);
    }

    public void SignOut()
    {
        CurrentUser user;
        lock (syncRoot)
        {
            user = SwitchToOperatorLocked();
        }

        CurrentUserChanged?.Invoke(this, user);
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            ResetSessionTimeoutLocked();
        }
    }

    private void StartSessionTimeoutLocked(CurrentUser user, int version)
    {
        TimeSpan duration = sessionOptions.ElevatedUserSessionDuration;
        if (user.Level <= SecurityUserLevel.Operator || duration <= TimeSpan.Zero)
        {
            return;
        }

        sessionTimeoutCts = new CancellationTokenSource();
        _ = FallbackToOperatorAfterDelayAsync(user, version, duration, sessionTimeoutCts.Token);
    }

    private async Task FallbackToOperatorAfterDelayAsync(
        CurrentUser user,
        int version,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        CurrentUser? fallbackUser = null;
        lock (syncRoot)
        {
            if (sessionVersion == version &&
                currentUser.Id == user.Id &&
                currentUser.Level == user.Level)
            {
                fallbackUser = SwitchToOperatorLocked();
            }
        }

        if (fallbackUser != null)
        {
            CurrentUserChanged?.Invoke(this, fallbackUser);
        }
    }

    private CurrentUser SwitchToOperatorLocked()
    {
        currentUser = OperatorUser;
        sessionVersion++;
        ResetSessionTimeoutLocked();
        return currentUser;
    }

    private void ResetSessionTimeoutLocked()
    {
        sessionTimeoutCts?.Cancel();
        sessionTimeoutCts?.Dispose();
        sessionTimeoutCts = null;
    }
}
