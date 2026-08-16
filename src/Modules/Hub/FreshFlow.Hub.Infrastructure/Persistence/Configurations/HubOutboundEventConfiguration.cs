using System.Text.Json;
using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubOutboundEventConfiguration : IEntityTypeConfiguration<HubOutboundEvent>
{
    public void Configure(EntityTypeBuilder<HubOutboundEvent> builder)
    {
        builder.ToTable("hub_outbound_events", table =>
        {
            table.HasCheckConstraint(
                "ck_hub_outbound_events_total_quantity_kg_positive",
                "total_quantity_kg > 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(e => e.DestinationRouteId)
            .HasColumnName("destination_route_id")
            .IsRequired();

        var itemsComparer = new ValueComparer<IReadOnlyList<HubOutboundItem>>(
            (a, b) => (a ?? Array.Empty<HubOutboundItem>()).SequenceEqual(b ?? Array.Empty<HubOutboundItem>()),
            v => (v ?? Array.Empty<HubOutboundItem>())
                .Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => (IReadOnlyList<HubOutboundItem>)(v ?? Array.Empty<HubOutboundItem>()).ToList().AsReadOnly());

        builder.Property(e => e.Items)
            .HasColumnName("items")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<HubOutboundItem>>(v, (JsonSerializerOptions?)null) ??
                    new List<HubOutboundItem>())
            .Metadata.SetValueComparer(itemsComparer);

        builder.Property(e => e.TotalQuantityKg)
            .HasColumnName("total_quantity_kg")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(e => e.DispatchedAt)
            .HasColumnName("dispatched_at")
            .IsRequired();

        builder.Property(e => e.RecordedBy)
            .HasColumnName("recorded_by");

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
            .HasConstraintName("fk_hub_outbound_events_hub");

        builder.HasIndex(e => e.HubId)
            .HasDatabaseName("idx_hub_outbound_events_hub_id");

        builder.HasIndex(e => e.DestinationRouteId)
            .HasDatabaseName("idx_hub_outbound_events_destination_route_id");

        builder.HasIndex(e => e.DispatchedAt)
            .HasDatabaseName("idx_hub_outbound_events_dispatched_at");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_hub_outbound_events_created_at");
    }
}
