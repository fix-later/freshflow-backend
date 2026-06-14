using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Fix_PricingPersistenceConfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_market_products_market_product_unique",
            table: "market_products");

        migrationBuilder.RenameColumn(
            name: "DeletedAt",
            table: "market_products",
            newName: "deleted_at");

        migrationBuilder.RenameIndex(
            name: "IX_market_products_DeletedAt",
            table: "market_products",
            newName: "IX_market_products_deleted_at");

        migrationBuilder.CreateIndex(
            name: "idx_market_products_market_product_unique",
            table: "market_products",
            columns: new[] { "MarketId", "ProductId" },
            unique: true,
            filter: "\"deleted_at\" IS NULL");

        migrationBuilder.AddForeignKey(
            name: "fk_price_snapshots_market_product",
            table: "price_snapshots",
            column: "MarketProductId",
            principalTable: "market_products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_price_snapshots_market_product",
            table: "price_snapshots");

        migrationBuilder.DropIndex(
            name: "idx_market_products_market_product_unique",
            table: "market_products");

        migrationBuilder.RenameColumn(
            name: "deleted_at",
            table: "market_products",
            newName: "DeletedAt");

        migrationBuilder.RenameIndex(
            name: "IX_market_products_deleted_at",
            table: "market_products",
            newName: "IX_market_products_DeletedAt");

        migrationBuilder.CreateIndex(
            name: "idx_market_products_market_product_unique",
            table: "market_products",
            columns: new[] { "MarketId", "ProductId" },
            unique: true,
            filter: "\"DeletedAt\" IS NULL");
    }
}
