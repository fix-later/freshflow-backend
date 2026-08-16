using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketAgentUserRowConfiguration
    : IEntityTypeConfiguration<MarketAgentUserRow>
{
    public void Configure(EntityTypeBuilder<MarketAgentUserRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT u."Id", u."RoleId", r."Name" AS "RoleName", u."Email", u."FullName",
                   u."IsActive", u."DeletedAt"
            FROM users AS u
            INNER JOIN roles AS r ON u."RoleId" = r."Id"
            """);
        builder.Property(row => row.Id);
        builder.Property(row => row.RoleId);
        builder.Property(row => row.RoleName);
        builder.Property(row => row.Email);
        builder.Property(row => row.FullName);
        builder.Property(row => row.IsActive);
        builder.Property(row => row.DeletedAt);
    }
}
