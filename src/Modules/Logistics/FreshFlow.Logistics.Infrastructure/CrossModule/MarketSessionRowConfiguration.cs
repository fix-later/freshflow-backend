using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionRowConfiguration : IEntityTypeConfiguration<MarketSessionRow>
{
    public void Configure(EntityTypeBuilder<MarketSessionRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""
            SELECT ms.id AS "Id", ms.hub_id AS "HubId",
                   ms.service_date AS "ServiceDate", ms.status AS "Status"
            FROM market_sessions ms
            WHERE ms.hub_id IS NOT NULL AND ms.deleted_at IS NULL
            """);
    }
}
