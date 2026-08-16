using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionTrackingOrderRowConfiguration
    : IEntityTypeConfiguration<MarketSessionTrackingOrderRow>
{
    public void Configure(EntityTypeBuilder<MarketSessionTrackingOrderRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT o.market_session_id                         AS "MarketSessionId",
                   o."Id"                                     AS "OrderId",
                   o."RestaurantId"                           AS "RestaurantId",
                   r."Name"                                   AS "RestaurantName",
                   o."Status"                                 AS "Status",
                   o.subtotal_amount                           AS "SubtotalAmount",
                   o.vat_amount                                AS "VatAmount",
                   o.delivery_fee                              AS "DeliveryFee",
                   o."TotalAmount"                            AS "TotalAmount",
                   o.confirmed_at                             AS "ConfirmedAt",
                   oi."Id"                                    AS "OrderItemId",
                   oi."MarketProductId"                       AS "MarketProductId",
                   oi."ProductNameSnapshot"                   AS "ProductName",
                   oi."Quantity"                              AS "Quantity",
                   COALESCE(oi."LockedUnitPrice", oi."UnitPrice") AS "UnitPrice",
                   COALESCE(oi."LockedTotal", oi."Quantity" * oi."UnitPrice") AS "Subtotal"
            FROM orders o
            JOIN restaurants r ON r."Id" = o."RestaurantId"
            LEFT JOIN order_items oi ON oi."OrderId" = o."Id"
            WHERE o.market_session_id IS NOT NULL
              AND o.deleted_at IS NULL
            """);
    }
}
