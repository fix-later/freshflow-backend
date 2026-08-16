using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderClaimConfiguration : IEntityTypeConfiguration<OrderClaim>
{
    public void Configure(EntityTypeBuilder<OrderClaim> builder)
    {
        builder.ToTable("order_claims", table =>
        {
            table.HasCheckConstraint("ck_order_claims_amount_positive", "\"Amount\" > 0");
        });
        builder.HasKey(claim => claim.Id);

        builder.Property(claim => claim.OrderId).IsRequired();
        builder.Property(claim => claim.RestaurantId).IsRequired();
        builder.Property(claim => claim.Amount)
            .HasColumnType("numeric(14,2)")
            .IsRequired();
        builder.Property(claim => claim.Reason)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(claim => claim.ProofImageUrl)
            .HasMaxLength(2_000);
        builder.Property(claim => claim.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(claim => claim.CreatedBy).IsRequired();
        builder.Property(claim => claim.CreatedAt).IsRequired();
        builder.Property(claim => claim.ReviewedBy);
        builder.Property(claim => claim.ReviewedAt);
        builder.Property(claim => claim.DecisionNote).HasMaxLength(1_000);
        builder.Property(claim => claim.RefundTransactionId);
        builder.Property(claim => claim.UpdatedAt)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(claim => claim.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(claim => claim.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_claims_order");

        builder.HasOne<CreditTransaction>()
            .WithMany()
            .HasForeignKey(claim => claim.RefundTransactionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_claims_refund_transaction");

        builder.HasIndex(claim => new { claim.RestaurantId, claim.Status, claim.CreatedAt })
            .HasDatabaseName("idx_order_claims_restaurant_status_created_at");
        builder.HasIndex(claim => claim.OrderId)
            .HasDatabaseName("idx_order_claims_order_id");
        builder.HasIndex(claim => claim.RefundTransactionId)
            .IsUnique()
            .HasFilter("\"RefundTransactionId\" IS NOT NULL")
            .HasDatabaseName("ux_order_claims_refund_transaction_id");
        builder.HasIndex(claim => claim.DeletedAt)
            .HasDatabaseName("IX_order_claims_deleted_at");
    }
}
