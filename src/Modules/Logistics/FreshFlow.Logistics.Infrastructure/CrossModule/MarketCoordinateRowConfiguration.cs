using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketCoordinateRowConfiguration : IEntityTypeConfiguration<MarketCoordinateRow>
{
    public void Configure(EntityTypeBuilder<MarketCoordinateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""SELECT "Id", "Name", "Latitude", "Longitude" FROM markets WHERE "DeletedAt" IS NULL""");
        builder.Property(m => m.Id);
        builder.Property(m => m.Name);
        builder.Property(m => m.Latitude);
        builder.Property(m => m.Longitude);
    }
}
