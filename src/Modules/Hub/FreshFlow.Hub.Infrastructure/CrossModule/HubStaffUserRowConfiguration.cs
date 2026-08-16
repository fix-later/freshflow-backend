using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubStaffUserRowConfiguration
    : IEntityTypeConfiguration<HubStaffUserRow>
{
    public void Configure(EntityTypeBuilder<HubStaffUserRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                u."Id" AS "UserId",
                r."Name" AS "RoleName",
                u."IsActive" AS "IsActive",
                u."DeletedAt" AS "DeletedAt"
            FROM users AS u
            INNER JOIN roles AS r ON u."RoleId" = r."Id"
            """);
        builder.Property(row => row.UserId);
        builder.Property(row => row.RoleName);
        builder.Property(row => row.IsActive);
        builder.Property(row => row.DeletedAt);
    }
}
