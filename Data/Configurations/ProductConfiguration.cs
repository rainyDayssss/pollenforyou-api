using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Catalog item mapping: unique ProductCode, precision decimal price, soft-delete
/// query filter, and restricted Category relationship.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasIndex(p => p.ProductCode).IsUnique();

        builder.Property(p => p.ProductCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.BasePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.HasQueryFilter(p => p.IsActive);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
