using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderSummaryRowConfiguration : IEntityTypeConfiguration<OrderSummaryRow>
{
    public void Configure(EntityTypeBuilder<OrderSummaryRow> builder)
    {
        builder.HasNoKey();
        // Draft orders are shopping carts, not placed orders: they would inflate order counts,
        // dilute the average order value and skew the cancellation-rate denominator.
        builder.ToSqlQuery(
            """
            SELECT
                "Id" AS "OrderId",
                "RestaurantId" AS "RestaurantId",
                "Status" AS "Status",
                "TotalAmount" AS "TotalAmount",
                "CreatedAt" AS "CreatedAt",
                "CancelledAt" AS "CancelledAt"
            FROM orders
            WHERE "deleted_at" IS NULL
              AND "Status" <> 'Draft'
            """);
    }
}

