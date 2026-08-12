using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketCodeRowConfiguration : IEntityTypeConfiguration<MarketCodeRow>
{
    public void Configure(EntityTypeBuilder<MarketCodeRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT "Id", "Code", "Name"
            FROM markets
            WHERE "DeletedAt" IS NULL
            """);
        builder.Property(row => row.Id);
        builder.Property(row => row.Code);
        builder.Property(row => row.Name);
    }
}
