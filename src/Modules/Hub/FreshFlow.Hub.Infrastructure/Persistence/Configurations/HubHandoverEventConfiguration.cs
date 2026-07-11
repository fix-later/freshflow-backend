using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubHandoverEventConfiguration : IEntityTypeConfiguration<HubHandoverEvent>
{
    public void Configure(EntityTypeBuilder<HubHandoverEvent> builder)
    {
        builder.ToTable("hub_handover_events", table =>
        {
            table.HasCheckConstraint(
                "ck_hub_handover_events_status",
                "status IN ('PENDING_CHECKOUT','CHECKED_OUT')");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(e => e.DeliveryRouteId)
            .HasColumnName("delivery_route_id")
            .IsRequired();

        builder.Property(e => e.DriverUserId)
            .HasColumnName("driver_user_id")
            .IsRequired();

        builder.Property(e => e.OutboundEventId)
            .HasColumnName("outbound_event_id");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasDefaultValue(HubHandoverEvent.StatusPendingCheckout)
            .IsRequired();

        builder.Property(e => e.HandedOverBy)
            .HasColumnName("handed_over_by")
            .IsRequired();

        builder.Property(e => e.HandedOverAt)
            .HasColumnName("handed_over_at")
            .IsRequired();

        builder.Property(e => e.DriverConfirmedAt)
            .HasColumnName("driver_confirmed_at");

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<HubEntity>()
            .WithMany()
            .HasForeignKey(e => e.HubId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hub_handover_events_hub");

        builder.HasOne<HubOutboundEvent>()
            .WithMany()
            .HasForeignKey(e => e.OutboundEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hub_handover_events_outbound_event");

        builder.HasIndex(e => e.HubId)
            .HasDatabaseName("idx_hub_handover_events_hub_id");

        builder.HasIndex(e => e.DeliveryRouteId)
            .HasDatabaseName("idx_hub_handover_events_delivery_route_id");

        builder.HasIndex(e => e.DriverUserId)
            .HasDatabaseName("idx_hub_handover_events_driver_user_id");

        builder.HasIndex(e => e.OutboundEventId)
            .HasDatabaseName("idx_hub_handover_events_outbound_event_id");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_hub_handover_events_created_at");
    }
}
