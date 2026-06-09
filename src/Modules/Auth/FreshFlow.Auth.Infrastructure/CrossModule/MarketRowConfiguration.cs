using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class MarketRowConfiguration : IEntityTypeConfiguration<MarketRow>
{
    public void Configure(EntityTypeBuilder<MarketRow> builder)
    {
        // MarketRow is a read-only projection onto the markets table owned by Catalog.Market.
        // Auth only reads Id + IsActive for validation; it does not own the schema.
        //
        // ToSqlQuery (instead of ToView) avoids an EF model conflict: Catalog.Market already
        // maps ToTable("markets"), and two entity types sharing the same store-object name
        // triggers an InvalidOperationException when AppDbContext builds its model.
        // A SQL-query-backed keyless entity has no named store object, so there is no clash.
        //
        // The WHERE "DeletedAt" IS NULL clause also ensures soft-deleted markets are excluded
        // from IsActiveMarketAsync checks without any additional filtering in callers.
        builder.HasNoKey();
        builder.ToSqlQuery("""SELECT "Id", "IsActive" FROM markets WHERE "DeletedAt" IS NULL""");
        builder.Property(m => m.Id);
        builder.Property(m => m.IsActive);
    }
}
