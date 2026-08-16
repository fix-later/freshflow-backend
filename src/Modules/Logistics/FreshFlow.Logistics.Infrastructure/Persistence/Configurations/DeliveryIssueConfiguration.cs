using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryIssueConfiguration : IEntityTypeConfiguration<DeliveryIssue>
{
    public void Configure(EntityTypeBuilder<DeliveryIssue> builder)
    {
        builder.ToTable(
            "delivery_issues",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_delivery_issues_type",
                    "issue_type IN ('undeliverable','damaged','customer_rejected','other')");
                table.HasCheckConstraint(
                    "ck_delivery_issues_status",
                    "status IN ('open','resolved')");
            });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(i => i.DeliveryId)
            .HasColumnName("delivery_id")
            .IsRequired();

        builder.Property(i => i.IssueType)
            .HasColumnName("issue_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.ReportedBy)
            .HasColumnName("reported_by")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<Delivery>()
            .WithMany()
            .HasForeignKey(i => i.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_delivery_issues_delivery");

        builder.HasIndex(i => i.DeliveryId)
            .HasDatabaseName("idx_delivery_issues_delivery_id");
    }
}
