using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementBatchRowConfiguration : IEntityTypeConfiguration<ProcurementBatchRow>
{
    public void Configure(EntityTypeBuilder<ProcurementBatchRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                id AS "BatchId",
                batch_date AS "BatchDate",
                market_id AS "MarketId",
                status AS "Status",
                manifested_at AS "ManifestedAt",
                handed_off_at AS "HandedOffAt"
            FROM procurement_batches
            WHERE deleted_at IS NULL
            """);
    }
}

