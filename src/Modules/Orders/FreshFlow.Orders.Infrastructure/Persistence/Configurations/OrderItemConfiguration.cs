using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderId).IsRequired();

        // MarketProductId references the Pricing module's market_products table by ID only —
        // no EF navigation/FK declared here (cross-module FKs are DB-constraint-only, see
        // docs/03-database-schema.md fk_order_items_market_product).
        builder.Property(i => i.MarketProductId).IsRequired();

        builder.Property(i => i.ProductNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(i => i.PackingCodeSnapshot)
            .HasColumnName("packing_code_snapshot")
            .HasMaxLength(50);
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired().HasColumnType("numeric(12,2)");
        builder.Property(i => i.LockedUnitPrice).HasColumnType("numeric(12,2)");
        builder.Property(i => i.LockedTotal).HasColumnType("numeric(14,2)");
        builder.Property(i => i.VatRateCode).HasColumnName("vat_rate_code").HasMaxLength(10);
        builder.Property(i => i.VatRatePercent).HasColumnName("vat_rate_percent").HasPrecision(5, 2);
        builder.Property(i => i.LockedVatAmount).HasColumnName("locked_vat_amount").HasColumnType("numeric(14,2)");
        builder.Property(i => i.ActualQuantity).HasColumnType("numeric(10,2)");
        builder.Property(i => i.ActualUnitPrice).HasColumnType("numeric(12,2)");

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // No deleted_at column — order_items has no soft delete (DDL design decision:
        // removing an item means cancelling the whole order; see docs/03-database-schema.md).
        builder.Ignore(i => i.DeletedAt);
        builder.Ignore(i => i.IsDeleted);

        // Computed in the domain, not persisted as a column — DB-side it's a GENERATED
        // ALWAYS AS column; EF treats it as not-mapped here for v1 (no read requirement yet).
        builder.Ignore(i => i.Subtotal);

        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("idx_order_items_order_id");

        builder.HasIndex(i => i.MarketProductId)
            .HasDatabaseName("idx_order_items_market_product_id");
    }
}
