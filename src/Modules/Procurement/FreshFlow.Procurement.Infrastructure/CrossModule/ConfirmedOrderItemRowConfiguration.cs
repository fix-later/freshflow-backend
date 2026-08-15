using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderItemRowConfiguration : IEntityTypeConfiguration<ConfirmedOrderItemRow>
{
    public void Configure(EntityTypeBuilder<ConfirmedOrderItemRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("order_items");
        builder.Property(row => row.OrderId).HasColumnName("OrderId");
        builder.Property(row => row.MarketProductId).HasColumnName("MarketProductId");
        builder.Property(row => row.ProductNameSnapshot).HasColumnName("ProductNameSnapshot");
        builder.Property(row => row.Quantity).HasColumnName("Quantity");
        builder.Property(row => row.UnitPrice).HasColumnName("UnitPrice");
        builder.Property(row => row.LockedTotal).HasColumnName("LockedTotal");
    }
}
