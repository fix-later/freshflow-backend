using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class ScheduledOrderConfiguration : IEntityTypeConfiguration<ScheduledOrder>
{
    public void Configure(EntityTypeBuilder<ScheduledOrder> builder)
    {
        builder.ToTable("scheduled_orders");
        builder.HasKey(s => s.Id);

        // RestaurantId references the Auth module's restaurants table by ID only — see
        // OrderConfiguration for the cross-module FK rationale.
        builder.Property(s => s.RestaurantId).IsRequired();

        builder.Property(s => s.RecurrenceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.FirstRunAt).IsRequired();
        builder.Property(s => s.LastExecutedAt);
        builder.Property(s => s.CancelledAt);
        builder.Property(s => s.Notes);

        // DeliveryAddressId references the Auth module's delivery_addresses table by ID only —
        // same cross-module FK rationale as OrderConfiguration. Nullable: backward compat for
        // schedules created before SCRUM-386 (see migration backfill).
        builder.Property(s => s.DeliveryAddressId);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.Ignore(s => s.IsActive);

        // Internal collection — ScheduledOrderItem has no independent existence outside its
        // schedule (same pattern as Order.Items in OrderConfiguration).
        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.ScheduledOrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_scheduled_order_items_scheduled_order");

        builder.Metadata.FindNavigation(nameof(ScheduledOrder.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.RestaurantId)
            .HasDatabaseName("idx_scheduled_orders_restaurant_id");

        builder.HasIndex(s => s.DeletedAt)
            .HasDatabaseName("IX_scheduled_orders_deleted_at");
    }
}
