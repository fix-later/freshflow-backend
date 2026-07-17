using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderSummaryRowConfiguration : IEntityTypeConfiguration<OrderSummaryRow>
{
    public void Configure(EntityTypeBuilder<OrderSummaryRow> builder)
    {
        builder.HasNoKey();
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
            """);
    }
}

