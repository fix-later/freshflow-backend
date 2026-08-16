using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubDiscrepancyStatusRowConfiguration : IEntityTypeConfiguration<HubDiscrepancyStatusRow>
{
    public void Configure(EntityTypeBuilder<HubDiscrepancyStatusRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT order_id AS "OrderId"
            FROM hub_discrepancies
            WHERE status = 'OPEN' AND deleted_at IS NULL
            """);
        builder.Property(d => d.OrderId);
    }
}
