using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCreditAlertLevel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing restaurant_credit rows predate this column — default them to
        // "none" (CreditAlertLevel.None's persisted form), not "", so
        // Enum.Parse in RestaurantCreditConfiguration's HasConversion doesn't throw
        // the first time an existing account is read back after this migration runs.
        migrationBuilder.AddColumn<string>(
            name: "last_alerted_level",
            table: "restaurant_credit",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "none");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "last_alerted_level",
            table: "restaurant_credit");
    }
}
