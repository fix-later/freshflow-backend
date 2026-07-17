using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementBatchItemRowConfiguration
    : IEntityTypeConfiguration<ProcurementBatchItemRow>
{
    public void Configure(EntityTypeBuilder<ProcurementBatchItemRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                procurement_batch_id AS "ProcurementBatchId",
                reference_unit_price AS "ReferenceUnitPrice",
                actual_quantity AS "ActualQuantity",
                actual_unit_price AS "ActualUnitPrice"
            FROM procurement_batch_items
            WHERE deleted_at IS NULL
            """);
    }
}
