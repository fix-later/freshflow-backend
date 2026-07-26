using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class HubByMarketRowConfiguration : IEntityTypeConfiguration<HubByMarketRow>
{
    public void Configure(EntityTypeBuilder<HubByMarketRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT id AS "HubId", market_id AS "MarketId"
            FROM hubs
            WHERE deleted_at IS NULL AND is_active = TRUE AND market_id IS NOT NULL
            """);
        builder.Property(row => row.HubId);
        builder.Property(row => row.MarketId);
    }
}
