using System.Text.Json;
using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubInboundEventConfiguration : IEntityTypeConfiguration<HubInboundEvent>
{
    public void Configure(EntityTypeBuilder<HubInboundEvent> builder)
    {
        builder.ToTable("hub_inbound_events", table =>
        {
            table.HasCheckConstraint(
                "ck_hub_inbound_events_total_quantity_kg_positive",
                "total_quantity_kg > 0");
            table.HasCheckConstraint(
                "ck_hub_inbound_events_status",
                "status IN ('PENDING', 'ARRIVED_AT_HUB')");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(e => e.SourceMarketId)
            .HasColumnName("source_market_id");

        builder.Property(e => e.DeliveryRouteId)
            .HasColumnName("delivery_route_id");

        builder.Property(e => e.DeliveryScheduleId)
            .HasColumnName("delivery_schedule_id");

        var itemsComparer = new ValueComparer<IReadOnlyList<HubInboundItem>>(
            (a, b) => (a ?? Array.Empty<HubInboundItem>()).SequenceEqual(b ?? Array.Empty<HubInboundItem>()),
            v => (v ?? Array.Empty<HubInboundItem>())
                .Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => (IReadOnlyList<HubInboundItem>)(v ?? Array.Empty<HubInboundItem>()).ToList().AsReadOnly());

        builder.Property(e => e.Items)
            .HasColumnName("items")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<HubInboundItem>>(v, (JsonSerializerOptions?)null) ??
                    new List<HubInboundItem>())
            .Metadata.SetValueComparer(itemsComparer);

        builder.Property(e => e.TotalQuantityKg)
            .HasColumnName("total_quantity_kg")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(e => e.ArrivedAt)
            .HasColumnName("arrived_at")
            .IsRequired();

        builder.Property(e => e.RecordedBy)
            .HasColumnName("recorded_by");

        builder.Property(e => e.HubStaffUserId)
            .HasColumnName("hub_staff_user_id");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasDefaultValue(HubInboundEvent.StatusPending)
            .IsRequired();

        builder.Property(e => e.ConditionStatus)
            .HasColumnName("condition_status")
            .HasMaxLength(30)
            .HasDefaultValue(HubInboundEvent.ConditionOk)
            .IsRequired();

        builder.Property(e => e.DiscrepancyNotes)
            .HasColumnName("discrepancy_notes")
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
            .HasConstraintName("fk_hub_inbound_events_hub");

        builder.HasIndex(e => e.HubId)
            .HasDatabaseName("idx_hub_inbound_events_hub_id");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("idx_hub_inbound_events_status");

        builder.HasIndex(e => e.ArrivedAt)
            .HasDatabaseName("idx_hub_inbound_events_arrived_at");

        builder.HasIndex(e => e.DeliveryScheduleId)
            .HasDatabaseName("idx_hub_inbound_events_delivery_schedule_id");

        builder.HasIndex(e => new { e.HubId, e.DeliveryScheduleId })
            .IsUnique()
            .HasFilter("delivery_schedule_id IS NOT NULL AND deleted_at IS NULL")
            .HasDatabaseName("ux_hub_inbound_events_hub_delivery_schedule_active");
    }
}
