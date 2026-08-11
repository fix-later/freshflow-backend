using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class CreditStatementLineConfiguration : IEntityTypeConfiguration<CreditStatementLine>
{
    public void Configure(EntityTypeBuilder<CreditStatementLine> builder)
    {
        builder.ToTable("credit_statement_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(l => l.CreditStatementId)
            .HasColumnName("credit_statement_id")
            .IsRequired();

        builder.Property(l => l.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired();

        builder.Property(l => l.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<CreditTransactionType>(v, ignoreCase: true));

        builder.Property(l => l.Amount)
            .HasColumnName("amount")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(l => l.BalanceAfter)
            .HasColumnName("balance_after")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(l => l.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(l => l.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(l => l.Reference)
            .HasColumnName("reference")
            .HasMaxLength(200);

        builder.Property(l => l.OrderId)
            .HasColumnName("order_id");

        builder.Property(l => l.PaymentMethod)
            .HasColumnName("payment_method")
            .HasMaxLength(20)
            .HasConversion(
                v => v == null ? null : ToSnakeCase(v.Value),
                v => v == null ? (PaymentMethod?)null : FromSnakeCase(v));

        builder.HasIndex(l => l.CreditStatementId)
            .HasDatabaseName("idx_credit_statement_lines_statement_id");
    }

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
