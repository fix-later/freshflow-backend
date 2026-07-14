using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class ProcurementExceptionConfiguration
    : IEntityTypeConfiguration<ProcurementException>
{
    public void Configure(EntityTypeBuilder<ProcurementException> builder)
    {
        builder.ToTable("procurement_exceptions");
        builder.HasKey(exception => exception.Id);

        builder.Property(exception => exception.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();
        builder.Property(exception => exception.ProcurementBatchId)
            .HasColumnName("procurement_batch_id")
            .IsRequired();
        builder.Property(exception => exception.MarketProductId)
            .HasColumnName("market_product_id")
            .IsRequired();
        builder.Property(exception => exception.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(exception => exception.ReportedQuantity)
            .HasColumnName("reported_quantity")
            .IsRequired();
        builder.Property(exception => exception.Note)
            .HasColumnName("note")
            .HasMaxLength(500);
        builder.Property(exception => exception.ProofImageUrl)
            .HasColumnName("proof_image_url")
            .HasMaxLength(500);
        builder.Property(exception => exception.ReportedByUserId)
            .HasColumnName("reported_by_user_id")
            .IsRequired();
        builder.Property(exception => exception.ReportedAt)
            .HasColumnName("reported_at")
            .IsRequired();
        builder.Property(exception => exception.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(exception => exception.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
        builder.Property(exception => exception.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(exception => exception.ProcurementBatchId)
            .HasDatabaseName("idx_procurement_exceptions_batch_id");
    }
}
