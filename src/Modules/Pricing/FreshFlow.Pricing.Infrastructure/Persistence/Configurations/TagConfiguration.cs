using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Name).HasColumnName("name").IsRequired().HasMaxLength(30);
        builder.Property(t => t.PinsToTop).HasColumnName("pins_to_top").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasFilter("\"deleted_at\" IS NULL")
            .HasDatabaseName("idx_tags_name_unique");

        // Excludes soft-deleted tags from every query/navigation projection automatically —
        // load-bearing for MarketProduct.Tags (skip-nav) and MarketProductReader's pin/filter.
        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
