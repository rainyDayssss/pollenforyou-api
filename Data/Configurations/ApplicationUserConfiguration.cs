using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Identity user mapping: soft-delete flag, audit timestamp, and refresh-token
/// relationship (cascade delete, per refined schema).
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.PasswordHash).HasMaxLength(256);

        builder.Property(u => u.IsActive).HasDefaultValue(true);

        // Enforce the DBML's literal "Email unique" constraint at the database level.
        // Identity's default EmailIndex is non-unique (uniqueness is otherwise app-layer
        // only via User.RequireUniqueEmail). Reusing the "EmailIndex" database name makes
        // EF Core merge this into Identity's existing index rather than adding a duplicate.
        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasQueryFilter(u => u.IsActive);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
