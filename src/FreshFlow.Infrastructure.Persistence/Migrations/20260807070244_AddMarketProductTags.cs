using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMarketProductTags : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "tags",
            table: "market_products",
            type: "text[]",
            nullable: false,
            defaultValueSql: "'{}'");

        // Data migration: carry forward is_featured=true as the "nổi bật" tag before the
        // column is dropped, so existing pinned listings keep their pin-to-top behavior.
        migrationBuilder.Sql(
            "UPDATE market_products SET tags = ARRAY['nổi bật'] WHERE is_featured = true;");

        migrationBuilder.DropColumn(
            name: "is_featured",
            table: "market_products");

        migrationBuilder.CreateIndex(
            name: "idx_market_products_tags",
            table: "market_products",
            column: "tags")
            .Annotation("Npgsql:IndexMethod", "GIN");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_market_products_tags",
            table: "market_products");

        migrationBuilder.DropColumn(
            name: "tags",
            table: "market_products");

        migrationBuilder.AddColumn<bool>(
            name: "is_featured",
            table: "market_products",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
