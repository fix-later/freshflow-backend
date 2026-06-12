using FreshFlow.Pricing.Domain.Entities;
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
        builder.Property(mp => mp.UpdatedAt).IsRequired();
        builder.Property(mp => mp.DeletedAt);

        // Ignore computed properties — not stored in DB
        builder.Ignore(mp => mp.AvailableQuantity);
        builder.Ignore(mp => mp.IsOutOfStock);

        builder.HasIndex(mp => new { mp.MarketId, mp.ProductId })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL")
            .HasDatabaseName("idx_market_products_market_product_unique");

        builder.HasIndex(mp => mp.MarketId)
            .HasDatabaseName("idx_market_products_market_id");

        builder.HasIndex(mp => mp.DeletedAt);
    }
}
