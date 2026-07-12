using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable(
            "deliveries",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_deliveries_status",
                    "status IN ('pending','arrived','delivered','failed')");
                table.HasCheckConstraint(
                    "ck_deliveries_sequence_number",
                    "sequence_number > 0");
            });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(d => d.DeliveryRouteId)
            .HasColumnName("delivery_route_id")
            .IsRequired();

        builder.Property(d => d.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(d => d.SequenceNumber)
            .HasColumnName("sequence_number")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.EstimatedArrival)
            .HasColumnName("estimated_arrival");

        builder.Property(d => d.ActualArrival)
            .HasColumnName("actual_arrival");

        builder.Property(d => d.FailureReason)
            .HasColumnName("failure_reason");

        builder.Property(d => d.ProofUrl)
            .HasColumnName("proof_url")
            .HasMaxLength(512);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<DeliveryRoute>()
            .WithMany()
            .HasForeignKey(d => d.DeliveryRouteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_deliveries_delivery_route");

        builder.HasIndex(d => d.DeliveryRouteId)
            .HasDatabaseName("idx_deliveries_delivery_route_id");

        builder.HasIndex(d => d.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_deliveries_order_id");
    }
}
