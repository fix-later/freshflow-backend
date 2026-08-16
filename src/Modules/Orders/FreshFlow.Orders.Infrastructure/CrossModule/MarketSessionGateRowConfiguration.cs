using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class MarketSessionGateRowConfiguration : IEntityTypeConfiguration<MarketSessionGateRow>
{
    public void Configure(EntityTypeBuilder<MarketSessionGateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""
            SELECT market_id AS "MarketId", service_date AS "ServiceDate", status AS "Status"
            FROM market_sessions
            WHERE deleted_at IS NULL
            """);
    }
}
