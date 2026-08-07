using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Pricing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.Persistence.Configurations;

internal sealed class MarketProductConfiguration : IEntityTypeConfiguration<MarketProduct>
{
    public void Configure(EntityTypeBuilder<MarketProduct> builder)
    {
        builder.ToTable("market_products");
        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.MarketId).IsRequired();
        builder.Property(mp => mp.ProductId).IsRequired();
        builder.Property(mp => mp.CurrentPrice)
            .IsRequired()
            .HasColumnType("numeric(12,2)");
        builder.Property(mp => mp.CurrentQuantity).IsRequired();
        builder.Property(mp => mp.ReservedQuantity).IsRequired().HasDefaultValue(0);
        builder.Property(mp => mp.UpdatedBy);
        builder.Property(mp => mp.CreatedAt).IsRequired();
        builder.Property(mp => mp.UpdatedAt).IsRequired().IsConcurrencyToken();

        // Map to snake_case column name so the partial unique index filter ("deleted_at" IS NULL)
        // and the soft-delete query filter reference the correct DB column name.
        builder.Property(mp => mp.DeletedAt).HasColumnName("deleted_at");

        // Ignore computed properties — not stored in DB
        builder.Ignore(mp => mp.AvailableQuantity);
        builder.Ignore(mp => mp.IsOutOfStock);
        builder.Ignore(mp => mp.IsFeatured);

        builder.HasIndex(mp => new { mp.MarketId, mp.ProductId })
            .IsUnique()
            .HasFilter("\"deleted_at\" IS NULL")
            .HasDatabaseName("idx_market_products_market_product_unique");

        builder.HasIndex(mp => mp.MarketId)
            .HasDatabaseName("idx_market_products_market_id");

        builder.HasIndex(mp => mp.DeletedAt)
            .HasDatabaseName("IX_market_products_deleted_at");

        // Skip-navigation many-to-many through an explicit join entity (not a shadow join) so
        // TagRepository can bulk-clear assignments (ExecuteDeleteAsync) without a second seam.
        builder.HasMany(mp => mp.Tags)
            .WithMany()
            .UsingEntity<MarketProductTagLink>(
                j => j.HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey(l => l.TagId)
                    .HasConstraintName("fk_market_product_tags_tag")
                    .OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne<MarketProduct>()
                    .WithMany()
                    .HasForeignKey(l => l.MarketProductId)
                    .HasConstraintName("fk_market_product_tags_market_product")
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("market_product_tags");
                    j.HasKey(l => new { l.MarketProductId, l.TagId });
                    j.Property(l => l.MarketProductId).HasColumnName("market_product_id");
                    j.Property(l => l.TagId).HasColumnName("tag_id");
                    j.HasIndex(l => l.TagId).HasDatabaseName("idx_market_product_tags_tag_id");
                });
    }
}
