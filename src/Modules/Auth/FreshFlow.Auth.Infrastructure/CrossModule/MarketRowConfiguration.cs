using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class MarketRowConfiguration : IEntityTypeConfiguration<MarketRow>
{
    public void Configure(EntityTypeBuilder<MarketRow> builder)
    {
        builder.ToTable("markets");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.IsActive).IsRequired().HasDefaultValue(true);
    }
}
