using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantOwnerRowConfiguration : IEntityTypeConfiguration<RestaurantOwnerRow>
{
    public void Configure(EntityTypeBuilder<RestaurantOwnerRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id" AS "RestaurantId", "UserId" AS "UserId" FROM restaurants""");
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.UserId);
    }
}
