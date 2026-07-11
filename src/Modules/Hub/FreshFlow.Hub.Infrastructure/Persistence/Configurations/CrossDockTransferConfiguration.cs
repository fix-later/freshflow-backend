using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class CrossDockTransferConfiguration : IEntityTypeConfiguration<CrossDockTransfer>
{
    public void Configure(EntityTypeBuilder<CrossDockTransfer> builder)
    {
        builder.ToTable("cross_dock_transfers", table =>
        {
            table.HasCheckConstraint(
                "ck_cross_dock_transfers_status",
                "status IN ('pending', 'in_progress', 'completed')");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(t => t.InboundEventId)
            .HasColumnName("inbound_event_id")
            .IsRequired();

        builder.Property(t => t.OutboundRouteId)
            .HasColumnName("outbound_route_id")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(CrossDockTransfer.StatusPending)
            .IsRequired();

        builder.Property(t => t.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<HubEntity>()
            .WithMany()
            .HasForeignKey(t => t.HubId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cross_dock_transfers_hub");

        builder.HasOne<HubInboundEvent>()
            .WithMany()
            .HasForeignKey(t => t.InboundEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cross_dock_transfers_inbound_event");

        builder.HasIndex(t => t.HubId)
            .HasDatabaseName("idx_cross_dock_transfers_hub_id");

        builder.HasIndex(t => t.InboundEventId)
            .HasDatabaseName("idx_cross_dock_transfers_inbound_event_id");

        builder.HasIndex(t => t.OutboundRouteId)
            .HasDatabaseName("idx_cross_dock_transfers_outbound_route_id");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("idx_cross_dock_transfers_status");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("idx_cross_dock_transfers_created_at");
    }
}
