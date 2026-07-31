using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PollenForYouApi.Entities;

namespace PollenForYouApi.Data.Configurations;

/// <summary>
/// Catalog grouping with soft-delete global query filter.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasDefaultValue(true);

        builder.HasQueryFilter(c => c.IsActive);
    }
}
