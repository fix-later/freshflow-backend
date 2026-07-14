using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductMarketRowConfiguration : IEntityTypeConfiguration<MarketProductMarketRow>
{
    public void Configure(EntityTypeBuilder<MarketProductMarketRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("market_products");
        builder.Property(row => row.Id).HasColumnName("Id");
        builder.Property(row => row.MarketId).HasColumnName("MarketId");
        builder.Property(row => row.CurrentPrice)
            .HasColumnName("CurrentPrice")
            .HasColumnType("numeric(12,2)");
        builder.Property(row => row.DeletedAt).HasColumnName("deleted_at");
    }
}
