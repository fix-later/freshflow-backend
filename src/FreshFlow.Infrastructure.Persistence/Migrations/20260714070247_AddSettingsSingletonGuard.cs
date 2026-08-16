using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSettingsSingletonGuard : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enforce singleton config tables at the DB level: a unique index on the constant
        // expression (true) permits at most one row, so a concurrent first-time insert race
        // fails cleanly (23505) instead of creating a second, nondeterministic settings row.
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX ix_operational_settings_singleton ON operational_settings ((true));");
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX ix_pricing_settings_singleton ON pricing_settings ((true));");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_operational_settings_singleton;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_pricing_settings_singleton;");
    }
}
