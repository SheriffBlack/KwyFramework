using KwyTemplate.Security.Authentication;
using KwyTemplate.Security.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
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

            await EnsureDatabaseSchemaAsync(dbContext, cancellationToken).ConfigureAwait(false);
            await EnsureDefaultUsersAsync(dbContext, cancellationToken).ConfigureAwait(false);
            initialized = true;
        }
        finally
        {
            initializeSemaphore.Release();
        }
    }

    private static async Task EnsureDatabaseSchemaAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await HasLegacyUsersTableWithoutMigrationHistoryAsync(dbContext, cancellationToken).ConfigureAwait(false))
        {
            await MarkInitialMigrationAppliedAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return;
        }

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        if (!await TableExistsWithOwnConnectionAsync(dbContext, "Users", cancellationToken).ConfigureAwait(false))
        {
            await CreateUsersSchemaAsync(dbContext, cancellationToken).ConfigureAwait(false);
            await MarkInitialMigrationAppliedAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> HasLegacyUsersTableWithoutMigrationHistoryAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            bool hasUsers = await TableExistsAsync(dbContext, "Users", cancellationToken).ConfigureAwait(false);
            bool hasMigrationHistory = await TableExistsAsync(dbContext, "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false);
            return hasUsers && !hasMigrationHistory;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> TableExistsWithOwnConnectionAsync(
        SecurityDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TableExistsAsync(dbContext, tableName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> TableExistsAsync(
        SecurityDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null;
    }

    private static async Task CreateUsersSchemaAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"Users\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Users\" PRIMARY KEY AUTOINCREMENT, \"UserName\" TEXT COLLATE NOCASE NOT NULL, \"DisplayName\" TEXT NOT NULL, \"PasswordHash\" TEXT NOT NULL, \"PasswordSalt\" TEXT NOT NULL, \"Level\" INTEGER NOT NULL, \"IsEnabled\" INTEGER NOT NULL, \"CreatedAt\" TEXT NOT NULL);",
            cancellationToken).ConfigureAwait(false);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_UserName\" ON \"Users\" (\"UserName\");",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task MarkInitialMigrationAppliedAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string migrationId = "20260718000000_InitialSecurityCreate";

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);",
            cancellationToken).ConfigureAwait(false);

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
            [migrationId, "8.0.28"],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDefaultUsersAsync(
        SecurityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        DateTime createdAt = DateTime.UtcNow;
        var defaultUsers = new[]
        {
            new DefaultUserSeed("operator", "操作员", "operator123", SecurityUserLevel.Operator),
            //new DefaultUserSeed("engineer", "工程师", "engineer123", SecurityUserLevel.Engineer),
            //new DefaultUserSeed("admin", "管理员", "admin123", SecurityUserLevel.Admin)
            new DefaultUserSeed("engineer", "工程师", "1", SecurityUserLevel.Engineer),
            new DefaultUserSeed("admin", "管理员", "1", SecurityUserLevel.Admin)
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

