using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CatalogModuleInit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Note: FK_user_market_assignments_markets_MarketId is intentionally preserved —
        // the EF model no longer tracks it (to avoid a cross-module project reference from
        // Auth.Infrastructure → Catalog.Domain), but the DB constraint remains intact.

        migrationBuilder.AddColumn<string>(
            name: "Address",
            table: "markets",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "markets",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "markets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Latitude",
            table: "markets",
            type: "numeric(9,6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Location",
            table: "markets",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Longitude",
            table: "markets",
            type: "numeric(9,6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "markets",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "markets",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.CreateTable(
            name: "product_categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_categories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "units_of_measurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_units_of_measurement", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                category = table.Column<string>(type: "text", nullable: true),
                unit = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.Id);
                table.ForeignKey(
                    name: "FK_products_product_categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "product_categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_products_units_of_measurement_UnitId",
                    column: x => x.UnitId,
                    principalTable: "units_of_measurement",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_markets_is_active",
            table: "markets",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_markets_DeletedAt",
            table: "markets",
            column: "DeletedAt");

        migrationBuilder.CreateIndex(
            name: "IX_product_categories_IsActive",
            table: "product_categories",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_product_categories_Name",
            table: "product_categories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_products_CategoryId",
            table: "products",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_products_DeletedAt",
            table: "products",
            column: "DeletedAt");

        migrationBuilder.CreateIndex(
            name: "IX_products_Name",
            table: "products",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_products_UnitId",
            table: "products",
            column: "UnitId");

        migrationBuilder.CreateIndex(
            name: "IX_units_of_measurement_IsActive",
            table: "units_of_measurement",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_units_of_measurement_Name",
            table: "units_of_measurement",
            column: "Name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "products");
        migrationBuilder.DropTable(name: "product_categories");
        migrationBuilder.DropTable(name: "units_of_measurement");

        migrationBuilder.DropIndex(name: "idx_markets_is_active", table: "markets");
        migrationBuilder.DropIndex(name: "IX_markets_DeletedAt", table: "markets");

        migrationBuilder.DropColumn(name: "Address", table: "markets");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "markets");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "markets");
        migrationBuilder.DropColumn(name: "Latitude", table: "markets");
        migrationBuilder.DropColumn(name: "Location", table: "markets");
        migrationBuilder.DropColumn(name: "Longitude", table: "markets");
        migrationBuilder.DropColumn(name: "Name", table: "markets");
        migrationBuilder.DropColumn(name: "UpdatedAt", table: "markets");

        // Note: FK_user_market_assignments_markets_MarketId is not touched here because
        // it was preserved through Up() and remains intact throughout.
    }
}
