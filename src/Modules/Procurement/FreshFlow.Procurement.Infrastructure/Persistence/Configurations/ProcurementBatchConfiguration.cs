using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProcurementBatchConfiguration : IEntityTypeConfiguration<ProcurementBatch>
{
    public void Configure(EntityTypeBuilder<ProcurementBatch> builder)
    {
        builder.ToTable("procurement_batches");
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(batch => batch.BatchDate)
            .HasColumnName("batch_date")
            .IsRequired();
        builder.Property(batch => batch.MarketId)
            .HasColumnName("market_id")
            .IsRequired();
        builder.Property(batch => batch.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ProcurementBatchStatus.Built)
            .IsRequired();
        builder.Property(batch => batch.ManifestedAt)
            .HasColumnName("manifested_at");
        builder.Property(batch => batch.AssignedAgentUserId)
            .HasColumnName("assigned_agent_user_id");
        builder.Property(batch => batch.AssignedAt)
            .HasColumnName("assigned_at");
        builder.Property(batch => batch.TotalItemCount)
            .HasColumnName("total_item_count")
            .IsRequired();
        builder.Property(batch => batch.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(batch => batch.UpdatedAt)
            .HasColumnName("updated_at")
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(batch => batch.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasMany(batch => batch.Items)
            .WithOne()
            .HasForeignKey(item => item.ProcurementBatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_procurement_batch_items_batch");
        builder.Metadata.FindNavigation(nameof(ProcurementBatch.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(batch => batch.Orders)
            .WithOne()
            .HasForeignKey(order => order.ProcurementBatchId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_procurement_batch_orders_batch");
        builder.Metadata.FindNavigation(nameof(ProcurementBatch.Orders))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(batch => batch.AssignedAgentUserId)
            .HasFilter("\"assigned_agent_user_id\" IS NOT NULL")
            .HasDatabaseName("ix_procurement_batches_assigned_agent");

        builder.HasIndex(batch => new { batch.BatchDate, batch.MarketId })
            .HasDatabaseName("idx_procurement_batches_batch_date_market_id");
    }
}
