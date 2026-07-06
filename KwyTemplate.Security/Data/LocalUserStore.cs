using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace KwyTemplate.Security.Data;

public sealed class LocalUserStore
{
    private readonly IDbContextFactory<SecurityDbContext> dbContextFactory;
    private readonly PasswordHasher passwordHasher;
    private readonly SemaphoreSlim initializeSemaphore = new(1, 1);
    private bool initialized;

    public LocalUserStore(
        IDbContextFactory<SecurityDbContext> dbContextFactory,
        PasswordHasher passwordHasher)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<LocalUser?> FindByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        string normalizedUserName = userName.Trim();
        await using SecurityDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserName == normalizedUserName, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetUserNamesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using SecurityDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsEnabled)
            .OrderBy(user => user.UserName)
            .Select(user => user.UserName)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => EnsureInitializedAsync(cancellationToken);

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        await initializeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }

            Directory.CreateDirectory(SecurityDataPaths.DataDirectory);

            await using SecurityDbContext dbContext = await dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            await dbContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            await EnsureDefaultUsersAsync(dbContext, cancellationToken).ConfigureAwait(false);
            initialized = true;
        }
        finally
        {
            initializeSemaphore.Release();
        }
    }

    private async Task EnsureDefaultUsersAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        DateTime createdAt = DateTime.UtcNow;
        var defaultUsers = new[]
        {
            new DefaultUserSeed("operator", "操作员", "operator123", SecurityUserLevel.Operator),
            new DefaultUserSeed("engineer", "工程师", "engineer123", SecurityUserLevel.Engineer),
            new DefaultUserSeed("admin", "管理员", "admin123", SecurityUserLevel.Admin)
        };

        foreach (DefaultUserSeed user in defaultUsers)
        {
            await EnsureDefaultUserAsync(
                    dbContext,
                    user.UserName,
                    user.DisplayName,
                    user.PlainPassword,
                    user.Level,
                    createdAt,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDefaultUserAsync(
        SecurityDbContext dbContext,
        string userName,
        string displayName,
        string plainPassword,
        SecurityUserLevel level,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Users
            .AnyAsync(user => user.UserName == userName, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        AddDefaultUser(dbContext, userName, displayName, plainPassword, level, createdAt);
    }

    private void AddDefaultUser(
        SecurityDbContext dbContext,
        string userName,
        string displayName,
        string plainPassword,
        SecurityUserLevel level,
        DateTime createdAt)
    {
        var password = passwordHasher.Hash(plainPassword);
        dbContext.Users.Add(new LocalUser
        {
            UserName = userName,
            DisplayName = displayName,
            PasswordHash = password.Hash,
            PasswordSalt = password.Salt,
            Level = level,
            IsEnabled = true,
            CreatedAt = createdAt
        });
    }

    private sealed record DefaultUserSeed(
        string UserName,
        string DisplayName,
        string PlainPassword,
        SecurityUserLevel Level);
}
