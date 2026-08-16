using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class ScheduledOrderItemConfiguration : IEntityTypeConfiguration<ScheduledOrderItem>
{
    public void Configure(EntityTypeBuilder<ScheduledOrderItem> builder)
    {
        builder.ToTable("scheduled_order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ScheduledOrderId).IsRequired();

        // MarketProductId references the Pricing module's market_products table by ID only —
        // same cross-module FK rationale as OrderItemConfiguration.
        builder.Property(i => i.MarketProductId).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // No deleted_at — like order_items (see OrderItemConfiguration), an item has no
        // independent existence outside its schedule; edits go through ReplaceItems, which
        // relies on EF's cascade-orphan delete to drop the old rows outright.
        builder.Ignore(i => i.DeletedAt);
        builder.Ignore(i => i.IsDeleted);

        builder.HasIndex(i => i.ScheduledOrderId)
            .HasDatabaseName("idx_scheduled_order_items_scheduled_order_id");

        builder.HasIndex(i => i.MarketProductId)
            .HasDatabaseName("idx_scheduled_order_items_market_product_id");
    }
}
