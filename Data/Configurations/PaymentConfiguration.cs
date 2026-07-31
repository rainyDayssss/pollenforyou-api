using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Payment ledger mapping: settlement stage/method strings, precision amount, and
/// audit FK to the verifying admin.
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.PaymentStage)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.PaymentMethod)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.AmountPaid)
            .HasPrecision(18, 2);

        builder.Property(p => p.TransactionReference)
            .HasMaxLength(100);

        builder.Property(p => p.PaidAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(p => p.VerifiedBy)
            .WithMany(u => u.VerifiedPayments)
            .HasForeignKey(p => p.VerifiedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
