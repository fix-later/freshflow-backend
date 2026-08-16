using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PricingModuleInit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "market_products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                CurrentQuantity = table.Column<int>(type: "integer", nullable: false),
                ReservedQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_products", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "price_snapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MarketProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                RecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_price_snapshots", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_market_products_market_id",
            table: "market_products",
            column: "MarketId");

        migrationBuilder.CreateIndex(
            name: "idx_market_products_market_product_unique",
            table: "market_products",
            columns: new[] { "MarketId", "ProductId" },
            unique: true,
            filter: "\"DeletedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_market_products_DeletedAt",
            table: "market_products",
            column: "DeletedAt");

        migrationBuilder.CreateIndex(
            name: "idx_price_snapshots_market_product_id",
            table: "price_snapshots",
            column: "MarketProductId");

        migrationBuilder.CreateIndex(
            name: "idx_price_snapshots_recorded_at",
            table: "price_snapshots",
            column: "RecordedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "market_products");

        migrationBuilder.DropTable(
            name: "price_snapshots");
    }
}
