using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAnalyticsCrossModuleRows : Migration
{
    // Intentionally empty. Analytics is read-only: its cross-module rows are keyless and
    // mapped with ToSqlQuery, so they own no table and emit no DDL. They still belong in
    // the model snapshot — this migration exists only to carry them there. Without it the
    // snapshot stays behind the model silently, because has-pending-model-changes compares
    // schema and these rows produce no schema diff.
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
