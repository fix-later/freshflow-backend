using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class OrderLookupRowConfiguration : IEntityTypeConfiguration<OrderLookupRow>
{
    public void Configure(EntityTypeBuilder<OrderLookupRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT oi."Id" AS "OrderItemId", oi."OrderId" AS "OrderId" FROM order_items oi""");
        builder.Property(o => o.OrderItemId);
        builder.Property(o => o.OrderId);
    }
}
