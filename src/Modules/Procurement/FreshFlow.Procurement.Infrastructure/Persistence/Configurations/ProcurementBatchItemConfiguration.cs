using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProcurementBatchItemConfiguration : IEntityTypeConfiguration<ProcurementBatchItem>
{
    public void Configure(EntityTypeBuilder<ProcurementBatchItem> builder)
    {
        builder.ToTable("procurement_batch_items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(item => item.ProcurementBatchId)
            .HasColumnName("procurement_batch_id")
            .IsRequired();
        builder.Property(item => item.MarketProductId)
            .HasColumnName("market_product_id")
            .IsRequired();
        builder.Property(item => item.ProductNameSnapshot)
            .HasColumnName("product_name_snapshot")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.TotalQuantity)
            .HasColumnName("total_quantity")
            .IsRequired();
        builder.Property(item => item.ReferenceUnitPrice)
            .HasColumnName("reference_unit_price")
            .HasColumnType("numeric(12,2)");
        builder.Property(item => item.ActualQuantity)
            .HasColumnName("actual_quantity");
        builder.Property(item => item.ActualUnitPrice)
            .HasColumnName("actual_unit_price")
            .HasColumnType("numeric(12,2)");
        builder.Property(item => item.PurchasedAt)
            .HasColumnName("purchased_at");
        builder.Property(item => item.AssignedAgentUserId)
            .HasColumnName("assigned_agent_user_id");
        builder.Property(item => item.AssignedAt)
            .HasColumnName("assigned_at");
        builder.Property(item => item.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(item => item.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(item => item.ProcurementBatchId)
            .HasDatabaseName("idx_procurement_batch_items_batch_id");

        builder.HasIndex(item => item.AssignedAgentUserId)
            .HasFilter("\"assigned_agent_user_id\" IS NOT NULL")
            .HasDatabaseName("idx_procurement_batch_items_assigned_agent");
    }
}
