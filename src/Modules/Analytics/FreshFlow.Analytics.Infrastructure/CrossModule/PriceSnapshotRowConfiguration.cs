using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class PriceSnapshotRowConfiguration : IEntityTypeConfiguration<PriceSnapshotRow>
{
    public void Configure(EntityTypeBuilder<PriceSnapshotRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                "MarketProductId" AS "MarketProductId",
                date_trunc('hour', "RecordedAt", 'UTC') AS "BucketStartUtc",
                MIN("Price") AS "MinPrice",
                MAX("Price") AS "MaxPrice",
                AVG("Price") AS "AvgPrice",
                COUNT(*)::integer AS "SnapshotCount",
                stddev_samp("Price") AS "PriceVolatility"
            FROM price_snapshots
            GROUP BY "MarketProductId", date_trunc('hour', "RecordedAt", 'UTC')
            """);
    }
}
