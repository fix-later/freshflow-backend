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
        //
        // Partitioning + FK compatibility:
        //   PostgreSQL 12+ supports FK constraints FROM a partitioned table TO a
        //   non-partitioned table (price_snapshots → market_products). The FK is
        //   inherited by all monthly child partitions automatically.
        //   The schema DDL (docs/03-database-schema.md) explicitly includes this FK.
        builder.ToTable("price_snapshots");
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.MarketProductId).IsRequired();
        builder.Property(ps => ps.Price)
            .IsRequired()
            .HasColumnType("numeric(12,2)");
        builder.Property(ps => ps.Quantity).IsRequired();
        builder.Property(ps => ps.RecordedBy);
        builder.Property(ps => ps.RecordedAt).IsRequired();

        // FK: PriceSnapshot.MarketProductId → MarketProduct.Id
        // No navigation properties on either side — use shadow FK registration.
        // ON DELETE CASCADE: removing a market_product cascades to its price history.
        builder.HasOne<MarketProduct>()
            .WithMany()
            .HasForeignKey(ps => ps.MarketProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_price_snapshots_market_product");

        builder.HasIndex(ps => ps.MarketProductId)
            .HasDatabaseName("idx_price_snapshots_market_product_id");

        builder.HasIndex(ps => ps.RecordedAt)
            .HasDatabaseName("idx_price_snapshots_recorded_at");
    }
}
