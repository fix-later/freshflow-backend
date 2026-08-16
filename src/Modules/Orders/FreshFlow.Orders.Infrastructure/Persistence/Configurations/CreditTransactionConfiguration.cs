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

        // Nullable — only populated for Settlement rows (see CreditTransaction ctor guard).
        // Explicit snake_case mapping (NOT ToString().ToLowerInvariant()) — BankTransfer's
        // PascalCase word boundary must become "bank_transfer" to match the API-facing value
        // CreditDtoMapper emits; a plain lowercase conversion would silently persist
        // "banktransfer" instead, which only "works" because Enum.Parse(ignoreCase: true)
        // happens to still round-trip it.
        builder.Property(t => t.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(20)
            .HasConversion(
                v => v == null ? null : ToSnakeCase(v.Value),
                v => v == null ? (PaymentMethod?)null : FromSnakeCase(v));

        builder.Property(t => t.Reference)
            .HasColumnName("reference")
            .HasMaxLength(200);

        builder.Property(t => t.RecordedByUserId)
            .HasColumnName("recorded_by_user_id");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(t => t.RestaurantId)
            .HasDatabaseName("idx_credit_transactions_restaurant_id");

        builder.HasIndex(t => t.OrderId)
            .HasDatabaseName("idx_credit_transactions_order_id");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("idx_credit_transactions_created_at");

        builder.HasIndex(t => new { t.RestaurantId, t.Reference })
            .IsUnique()
            .HasFilter("type = 'settlement' AND reference IS NOT NULL")
            .HasDatabaseName("uq_credit_transactions_settlement_reference");

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_credit_transactions_settlement_reference",
            "type <> 'settlement' OR (reference IS NOT NULL AND btrim(reference) <> '')"));
    }

    // Plain method calls (not inline switch/throw expressions) so the conversion lambdas
    // above stay valid EF Core conversion expression trees (switch expressions and throw
    // expressions are not supported inside an Expression<Func<...>>).
    private static string ToSnakeCase(PaymentMethod value) => value switch
    {
        PaymentMethod.BankTransfer => "bank_transfer",
        PaymentMethod.Manual => "manual",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PaymentMethod FromSnakeCase(string value) => value switch
    {
        "bank_transfer" => PaymentMethod.BankTransfer,
        "manual" => PaymentMethod.Manual,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
