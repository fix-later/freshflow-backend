using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProcurementBatchOrderConfiguration : IEntityTypeConfiguration<ProcurementBatchOrder>
{
    public void Configure(EntityTypeBuilder<ProcurementBatchOrder> builder)
    {
        builder.ToTable("procurement_batch_orders");
        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(order => order.ProcurementBatchId)
            .HasColumnName("procurement_batch_id")
            .IsRequired();
        builder.Property(order => order.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(order => order.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(order => order.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(order => order.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(order => order.ProcurementBatchId)
            .HasDatabaseName("idx_procurement_batch_orders_batch_id");
        builder.HasIndex(order => order.OrderId)
            .IsUnique()
            .HasFilter("\"deleted_at\" IS NULL")
            .HasDatabaseName("ux_procurement_batch_orders_order_active");
    }
}
