using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data;

/// <summary>
/// Application database context. Identity tables (int keys) plus the unified
/// single-ledger domain tables, all mapped strictly via EF Core Fluent API
/// configurations (no Data Annotations).
/// </summary>
public class PfyDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public PfyDbContext(DbContextOptions<PfyDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(PfyDbContext).Assembly);
    }
}
