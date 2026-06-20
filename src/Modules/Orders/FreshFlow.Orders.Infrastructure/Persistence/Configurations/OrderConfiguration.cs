using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        // RestaurantId references the Auth module's restaurants table by ID only — no EF
        // navigation/FK declared here to avoid an EF model conflict with Auth's own
        // ToTable("restaurants") mapping (cross-module FKs are DB-constraint-only, see
        // docs/03-database-schema.md fk_orders_restaurant).
        builder.Property(o => o.RestaurantId).IsRequired();
        builder.Property(o => o.OrderGroupId);
        builder.Property(o => o.ScheduledOrderId);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.PaymentStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.ScheduledFor);
        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("numeric(14,2)");
        builder.Property(o => o.Notes);
        builder.Property(o => o.CancelledAt);
        builder.Property(o => o.CancellationReason);
        builder.Property(o => o.ConfirmedReceiptAt)
            .HasColumnName("confirmed_receipt_at");

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired().IsConcurrencyToken();
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");

        // Internal collection — OrderItem has no independent existence outside its Order.
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_order_items_order");

        builder.Metadata.FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.RestaurantId, o.Status })
            .HasDatabaseName("idx_orders_restaurant_id_status");

        builder.HasIndex(o => o.OrderGroupId)
            .HasDatabaseName("idx_orders_order_group_id");

        builder.HasIndex(o => o.ScheduledFor)
            .HasDatabaseName("idx_orders_scheduled_for");

        builder.HasIndex(o => o.ScheduledOrderId)
            .HasDatabaseName("idx_orders_scheduled_order_id");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("idx_orders_status");

        builder.HasIndex(o => o.DeletedAt)
            .HasDatabaseName("IX_orders_deleted_at");
    }
}
