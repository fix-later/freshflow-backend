using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Fix_CatalogUniqueIndexFilters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_units_of_measurement_Name",
            table: "units_of_measurement");

        migrationBuilder.DropIndex(
            name: "IX_product_categories_Name",
            table: "product_categories");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "products",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_units_of_measurement_Name",
            table: "units_of_measurement",
            column: "Name",
            unique: true,
            filter: "\"DeletedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_product_categories_Name",
            table: "product_categories",
            column: "Name",
            unique: true,
            filter: "\"DeletedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_units_of_measurement_Name",
            table: "units_of_measurement");

        migrationBuilder.DropIndex(
            name: "IX_product_categories_Name",
            table: "product_categories");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "products",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(1000)",
            oldMaxLength: 1000,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_units_of_measurement_Name",
            table: "units_of_measurement",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_product_categories_Name",
            table: "product_categories",
            column: "Name",
            unique: true);
    }
}
