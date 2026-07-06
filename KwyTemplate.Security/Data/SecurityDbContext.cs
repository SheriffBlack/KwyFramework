using Microsoft.EntityFrameworkCore;

namespace KwyTemplate.Security.Data;

public sealed class SecurityDbContext : DbContext
{
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options)
        : base(options)
    {
    }

    public DbSet<LocalUser> Users => Set<LocalUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<LocalUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).ValueGeneratedOnAdd();

            entity.Property(user => user.UserName)
                .HasMaxLength(64)
                .UseCollation("NOCASE")
                .IsRequired();

            entity.HasIndex(user => user.UserName)
                .IsUnique();

            entity.Property(user => user.DisplayName)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.PasswordSalt).IsRequired();
            entity.Property(user => user.Level)
                .HasConversion<int>()
                .IsRequired();
            entity.Property(user => user.IsEnabled).IsRequired();
            entity.Property(user => user.CreatedAt).IsRequired();
        });
    }
}
