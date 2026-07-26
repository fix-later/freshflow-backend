using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class MarketRowConfiguration : IEntityTypeConfiguration<MarketRow>
{
    public void Configure(EntityTypeBuilder<MarketRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id", "IsActive" FROM markets WHERE "DeletedAt" IS NULL""");
        builder.Property(row => row.Id);
        builder.Property(row => row.IsActive);
    }
}
