using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderItemPackingCodeSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "packing_code_snapshot",
            table: "order_items",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE order_items AS oi
            SET packing_code_snapshot = pc."Code"
            FROM market_products AS mp
            INNER JOIN products AS p ON p."Id" = mp."ProductId"
            INNER JOIN packing_codes AS pc ON pc."Id" = p."PackingCodeId"
            WHERE oi."MarketProductId" = mp."Id"
              AND oi.packing_code_snapshot IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "packing_code_snapshot",
            table: "order_items");
    }
}
