using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class CreditStatementConfiguration : IEntityTypeConfiguration<CreditStatement>
{
    public void Configure(EntityTypeBuilder<CreditStatement> builder)
    {
        builder.ToTable("credit_statements");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired();

        builder.Property(s => s.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(s => s.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(s => s.OpeningBalance)
            .HasColumnName("opening_balance")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(s => s.ClosingBalance)
            .HasColumnName("closing_balance")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(s => s.TotalCharges)
            .HasColumnName("total_charges")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(s => s.TotalSettlements)
            .HasColumnName("total_settlements")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(s => s.TotalRefunds)
            .HasColumnName("total_refunds")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(s => s.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        // Append-only + idempotent: exactly one statement per (restaurant, period).
        builder.HasIndex(s => new { s.RestaurantId, s.PeriodStart })
            .IsUnique()
            .HasDatabaseName("uq_credit_statements_restaurant_id_period_start");

        // Internal collection — CreditStatementLine has no independent existence outside
        // its statement, and a statement is append-only once generated.
        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey(l => l.CreditStatementId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_credit_statement_lines_statement");

        builder.Metadata.FindNavigation(nameof(CreditStatement.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
