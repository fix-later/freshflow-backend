using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("credit_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(t => t.OrderId)
            .HasColumnName("order_id");

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<CreditTransactionType>(v, ignoreCase: true));

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(t => t.BalanceAfter)
            .HasColumnName("balance_after")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(t => t.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(t => t.RestaurantId)
            .HasDatabaseName("idx_credit_transactions_restaurant_id");

        builder.HasIndex(t => t.OrderId)
            .HasDatabaseName("idx_credit_transactions_order_id");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("idx_credit_transactions_created_at");
    }
}
