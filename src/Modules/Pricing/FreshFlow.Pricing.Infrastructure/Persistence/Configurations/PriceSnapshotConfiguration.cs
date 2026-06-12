using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.Persistence.Configurations;

internal sealed class PriceSnapshotConfiguration : IEntityTypeConfiguration<PriceSnapshot>
{
    public void Configure(EntityTypeBuilder<PriceSnapshot> builder)
    {
        // price_snapshots is range-partitioned by month on RecordedAt.
        // Partition DDL is managed separately (PartitionMaintenanceJob).
        // We map the parent table only; EF handles reads/inserts via the parent.
        builder.ToTable("price_snapshots");
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.MarketProductId).IsRequired();
        builder.Property(ps => ps.Price)
            .IsRequired()
            .HasColumnType("numeric(12,2)");
        builder.Property(ps => ps.Quantity).IsRequired();
        builder.Property(ps => ps.RecordedBy);
        builder.Property(ps => ps.RecordedAt).IsRequired();

        builder.HasIndex(ps => ps.MarketProductId)
            .HasDatabaseName("idx_price_snapshots_market_product_id");

        builder.HasIndex(ps => ps.RecordedAt)
            .HasDatabaseName("idx_price_snapshots_recorded_at");
    }
}
