using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Seeds the two operational roles ("Admin", "Superadmin") used by the access
/// policies defined in the SRS.
/// </summary>
public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public const int AdminRoleId = 1;
    public const int SuperadminRoleId = 2;

    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        // NOTE: ConcurrencyStamp MUST be hardcoded. IdentityRole's constructor generates
        // a new Guid each time, which would make HasData produce a different model on
        // every build and break migration snapshot matching (PendingModelChangesWarning).
        builder.HasData(
            new ApplicationRole
            {
                Id = AdminRoleId,
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "6f0a1b2c-3d4e-4f5a-8b9c-0d1e2f3a4b5c"
            },
            new ApplicationRole
            {
                Id = SuperadminRoleId,
                Name = "Superadmin",
                NormalizedName = "SUPERADMIN",
                ConcurrencyStamp = "7f0a1b2c-3d4e-4f5a-8b9c-0d1e2f3a4b5c"
            });
    }
}
