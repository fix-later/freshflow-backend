using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

internal sealed class MarketRowConfiguration : IEntityTypeConfiguration<MarketRow>
{
    public void Configure(EntityTypeBuilder<MarketRow> builder)
    {
        // Read-only projection onto the markets table owned by Catalog.Market.
        // ToSqlQuery avoids a model conflict with Catalog's ToTable("markets").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id", "Name", "Location", "Address", "IsActive" FROM markets WHERE "DeletedAt" IS NULL""");
        builder.Property(m => m.Id);
        builder.Property(m => m.Name);
        builder.Property(m => m.Location);
        builder.Property(m => m.Address);
        builder.Property(m => m.IsActive);
    }
}
