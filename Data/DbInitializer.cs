using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data;

/// <summary>
/// One-shot startup seeding (not a background worker — AGENT.md §14 forbids
/// timer-based sweeps, but a single bootstrap pass at boot is fine): ensures the
/// two operational roles exist and creates the default superadmin account from
/// the <c>DefaultAdmin</c> configuration section when it is missing. Idempotent.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DbInitializer).FullName!);

        try
        {
            await EnsureRolesAsync(roleManager, logger);

            var email = configuration["DefaultAdmin:Email"] ?? "superadmin@pollenforyou.com";
            var password = configuration["DefaultAdmin:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                password = "Superadmin@2026";
                logger.LogWarning(
                    "DefaultAdmin:Password is not configured; using the development default. Override it (and rotate the Jwt:Key) before production.");
            }

            // FindByEmailAsync respects the IsActive query filter, so this only
            // sees active accounts — exactly the bootstrap condition we care about.
            if (await userManager.FindByEmailAsync(email) is { } existing)
            {
                await EnsureSuperadminRoleAsync(userManager, existing, logger);
                return;
            }

            try
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    IsActive = true
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    logger.LogError(
                        "Failed to seed default superadmin: {Errors}",
                        string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                await EnsureSuperadminRoleAsync(userManager, user, logger);
                logger.LogInformation(
                    "Seeded default superadmin account for '{Email}'. Change its password after first login.", email);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // A soft-deleted account already holds this email: the app-layer
                // validator can't see it, so only the DB unique index on
                // NormalizedEmail caught the collision. Nothing to seed.
                logger.LogWarning(
                    "Default superadmin '{Email}' already exists on a soft-deleted account; skipping seed.", email);
            }
        }
        catch (SqlException ex)
        {
            // Most likely an unmigrated/missing database — fail fast, but say so.
            logger.LogError(ex,
                "Database seeding failed. If the database is missing or unmigrated, run 'dotnet ef database update' first.");
            throw;
        }
        catch (DbUpdateException ex)
        {
            // Non-unique DB failure (e.g., deadlock, connection drop) — surface with the same hint.
            logger.LogError(ex,
                "Database seeding failed. If the database is missing or unmigrated, run 'dotnet ef database update' first.");
            throw;
        }
    }

    private static async Task EnsureRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        // The roles are normally seeded via HasData on a fresh migration, but this
        // guarantees they exist even when an admin user was created first.
        foreach (var roleName in new[] { UserRoles.Admin, UserRoles.Superadmin })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            if (roleResult.Succeeded)
            {
                logger.LogInformation("Seeded role '{RoleName}'.", roleName);
            }
            else
            {
                logger.LogError(
                    "Failed to seed role '{RoleName}': {Errors}",
                    roleName,
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task EnsureSuperadminRoleAsync(
        UserManager<ApplicationUser> userManager, ApplicationUser user, ILogger logger)
    {
        // UserStore.AddToRoleAsync skips memberships that already exist, so this
        // is idempotent.
        var result = await userManager.AddToRoleAsync(user, UserRoles.Superadmin);
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to assign '{Role}' to '{Email}': {Errors}",
                UserRoles.Superadmin,
                user.Email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.GetBaseException() is SqlException { Number: 2601 or 2627 };
    }
}
