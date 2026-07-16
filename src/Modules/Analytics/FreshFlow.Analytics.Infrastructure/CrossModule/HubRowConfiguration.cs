using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubRowConfiguration : IEntityTypeConfiguration<HubRow>
{
    public void Configure(EntityTypeBuilder<HubRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                id AS "HubId",
                name AS "HubName"
            FROM hubs
            WHERE deleted_at IS NULL
            """);
    }
}
