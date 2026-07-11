using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubDiscrepancyConfiguration : IEntityTypeConfiguration<HubDiscrepancy>
{
    public void Configure(EntityTypeBuilder<HubDiscrepancy> builder)
    {
        builder.ToTable("hub_discrepancies", table =>
        {
            table.HasCheckConstraint(
                "ck_hub_discrepancies_affected_quantity_positive",
                "affected_quantity > 0");
            table.HasCheckConstraint(
                "ck_hub_discrepancies_condition_status",
                "condition_status IN ('MISSING', 'DAMAGED', 'PARTIAL')");
            table.HasCheckConstraint(
                "ck_hub_discrepancies_status",
                "status IN ('OPEN', 'ACKNOWLEDGED')");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(d => d.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(d => d.InboundEventId)
            .HasColumnName("inbound_event_id")
            .IsRequired();

        builder.Property(d => d.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(d => d.OrderItemId)
            .HasColumnName("order_item_id")
            .IsRequired();

        builder.Property(d => d.AffectedQuantity)
            .HasColumnName("affected_quantity")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(d => d.ConditionStatus)
            .HasColumnName("condition_status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue(HubDiscrepancy.StatusOpen)
            .IsRequired();

        builder.Property(d => d.AcknowledgedBy)
            .HasColumnName("acknowledged_by");

        builder.Property(d => d.AcknowledgedAt)
            .HasColumnName("acknowledged_at");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<HubEntity>()
            .WithMany()
            .HasForeignKey(d => d.HubId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hub_discrepancies_hub");

        builder.HasOne<HubInboundEvent>()
            .WithMany()
            .HasForeignKey(d => d.InboundEventId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hub_discrepancies_inbound_event");

        builder.HasIndex(d => d.HubId)
            .HasDatabaseName("idx_hub_discrepancies_hub_id");

        builder.HasIndex(d => d.InboundEventId)
            .HasDatabaseName("idx_hub_discrepancies_inbound_event_id");

        builder.HasIndex(d => d.OrderId)
            .HasDatabaseName("idx_hub_discrepancies_order_id");

        builder.HasIndex(d => d.OrderItemId)
            .HasDatabaseName("idx_hub_discrepancies_order_item_id");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("idx_hub_discrepancies_status");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("idx_hub_discrepancies_created_at");
    }
}
