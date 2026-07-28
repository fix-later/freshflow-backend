using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubSortingProgressConfiguration : IEntityTypeConfiguration<HubSortingProgress>
{
    public void Configure(EntityTypeBuilder<HubSortingProgress> builder)
    {
        builder.ToTable("hub_sorting_progress", table =>
        {
            table.HasCheckConstraint(
                "ck_hub_sorting_progress_status",
                "status IN ('PENDING', 'SORTED')");
            table.HasCheckConstraint(
                "ck_hub_sorting_progress_qty_nonnegative",
                "sorted_quantity_kg >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(p => p.ServiceDate)
            .HasColumnName("service_date")
            .HasColumnType("date")
            .IsRequired();

        // RouteId/OrderItemId are cross-module ids (Logistics/Orders) -- no DB FK.
        builder.Property(p => p.RouteId)
            .HasColumnName("route_id");

        builder.Property(p => p.OrderItemId)
            .HasColumnName("order_item_id")
            .IsRequired();

        builder.Property(p => p.SortedQuantityKg)
            .HasColumnName("sorted_quantity_kg")
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HubSortingProgress.StatusPending)
            .IsRequired();

        builder.Property(p => p.SortedByUserId)
            .HasColumnName("sorted_by_user_id");

        builder.Property(p => p.SortedAt)
            .HasColumnName("sorted_at");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(p => new { p.HubId, p.ServiceDate, p.OrderItemId })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_hub_sorting_progress_hub_date_item_active");
    }
}
