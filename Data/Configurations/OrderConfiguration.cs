using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Unified single-ledger order mapping: structured OrderNumber, state machine status,
/// workspace claim columns, optimistic concurrency RowVersion, lazy-eviction expiry,
/// and the composite (Status, ExpiresAt) queue index.
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        // Optional checkout idempotency key — unique only when present, so clients
        // that don't send one (legacy) never collide (same pattern as NormalizedEmail).
        builder.HasIndex(o => o.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasIndex(o => new { o.Status, o.ExpiresAt });

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.IdempotencyKey)
            .HasMaxLength(100);

        builder.Property(o => o.CustomerName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(o => o.CustomerMessengerUsername)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.ReceiverName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(o => o.ReceiverContactNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.DeliveryAddress)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(o => o.MessageOnCard)
            .HasMaxLength(500);

        builder.Property(o => o.Status)
            .HasMaxLength(30)
            .HasDefaultValue(OrderStatuses.Pending);

        builder.Property(o => o.TotalPrice)
            .HasPrecision(18, 2);

        builder.Property(o => o.ExpiresAt)
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        // NOTE: Only ONE of the two Orders->AspNetUsers FKs may use a cascading action
        // (CASCADE/SET NULL). SQL Server error 1785 forbids multiple cascade paths from
        // the same table to the same parent. ClaimedByUserId stays SET NULL (transient
        // workspace lock, per DBML); SettledByAdminId is NO ACTION to preserve the
        // immutable audit trail. Users are soft-deleted (IsActive) so hard-deletes are
        // never expected in practice.
        builder.HasOne(o => o.ClaimedBy)
            .WithMany(u => u.ClaimedOrders)
            .HasForeignKey(o => o.ClaimedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.SettledBy)
            .WithMany(u => u.SettledOrders)
            .HasForeignKey(o => o.SettledByAdminId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
