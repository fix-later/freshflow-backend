using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class MarketRowConfiguration : IEntityTypeConfiguration<MarketRow>
{
    public void Configure(EntityTypeBuilder<MarketRow> builder)
    {
        // MarketRow is a read-only projection onto the markets table owned by Catalog.Market.
        // Auth only reads Id + IsActive for validation; it does not own the schema.
        // HasNoKey + ToView suppresses any schema migration generation for this type.
        builder.HasNoKey();
        builder.ToView("markets");
        builder.Property(m => m.Id);
        builder.Property(m => m.IsActive);
    }
}
