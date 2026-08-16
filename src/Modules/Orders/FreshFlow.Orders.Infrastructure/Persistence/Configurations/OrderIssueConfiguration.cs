using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderIssueConfiguration : IEntityTypeConfiguration<OrderIssue>
{
    public void Configure(EntityTypeBuilder<OrderIssue> builder)
    {
        builder.ToTable("order_issues");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(i => i.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(i => i.OrderItemId)
            .HasColumnName("order_item_id");

        builder.Property(i => i.ReportedBy)
            .HasColumnName("reported_by")
            .IsRequired();

        builder.Property(i => i.IssueType)
            .HasColumnName("issue_type")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<OrderIssueType>(v, ignoreCase: true));

        builder.Property(i => i.AffectedQuantity)
            .HasColumnName("affected_quantity")
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(i => i.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasMaxLength(OrderIssue.MaxDescriptionLength);

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<OrderIssueStatus>(v, ignoreCase: true));

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Ignore(i => i.IsDeleted);

        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("idx_order_issues_order_id");

        builder.HasIndex(i => i.OrderItemId)
            .HasDatabaseName("idx_order_issues_order_item_id");

        builder.HasIndex(i => i.Status)
            .HasDatabaseName("idx_order_issues_status");

        builder.HasIndex(i => i.DeletedAt)
            .HasDatabaseName("idx_order_issues_deleted_at");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_order_issues_order");

        builder.HasOne<OrderItem>()
            .WithMany()
            .HasForeignKey(i => i.OrderItemId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_order_issues_order_item");
    }
}
