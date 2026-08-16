using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class ProcurementExceptionRowConfiguration
    : IEntityTypeConfiguration<ProcurementExceptionRow>
{
    public void Configure(EntityTypeBuilder<ProcurementExceptionRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                procurement_batch_id AS "ProcurementBatchId",
                type AS "Type"
            FROM procurement_exceptions
            WHERE deleted_at IS NULL
            """);
    }
}
