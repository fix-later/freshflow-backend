using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ReplaceMarketProductTagsWithTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tags",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                pins_to_top = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tags", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "market_product_tags",
            columns: table => new
            {
                market_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_product_tags", x => new { x.market_product_id, x.tag_id });
                table.ForeignKey(
                    name: "fk_market_product_tags_market_product",
                    column: x => x.market_product_id,
                    principalTable: "market_products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_market_product_tags_tag",
                    column: x => x.tag_id,
                    principalTable: "tags",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_market_product_tags_tag_id",
            table: "market_product_tags",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "idx_tags_name_unique",
            table: "tags",
            column: "name",
            unique: true,
            filter: "\"deleted_at\" IS NULL");

        // ── Data migration: carry forward the existing text[] tags into the new catalog +
        //    join tables before the column is dropped. Prod today has 0 tag rows, so this
        //    is effectively a no-op there — it exists for any lower environment with data.
        migrationBuilder.Sql("""
            INSERT INTO tags (id, name, pins_to_top, created_at, updated_at)
            SELECT gen_random_uuid(), t, (t = 'nổi bật'), now(), now()
            FROM (SELECT DISTINCT unnest(tags) AS t FROM market_products) s
            WHERE t <> '';
            """);

        migrationBuilder.Sql("""
            INSERT INTO market_product_tags (market_product_id, tag_id)
            SELECT mp."Id", tg.id
            FROM market_products mp
            CROSS JOIN LATERAL unnest(mp.tags) AS v(name)
            JOIN tags tg ON tg.name = v.name;
            """);

        // Seed the pin-to-top tag even if no product currently carries it, so the pin
        // mechanism has a usable catalog entry from day one.
        migrationBuilder.Sql("""
            INSERT INTO tags (id, name, pins_to_top, created_at, updated_at)
            SELECT gen_random_uuid(), 'nổi bật', true, now(), now()
            WHERE NOT EXISTS (SELECT 1 FROM tags WHERE name = 'nổi bật');
            """);

        migrationBuilder.DropIndex(
            name: "idx_market_products_tags",
            table: "market_products");

        migrationBuilder.DropColumn(
            name: "tags",
            table: "market_products");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "tags",
            table: "market_products",
            type: "text[]",
            nullable: false,
            defaultValueSql: "'{}'");

        // Backfill tags text[] from the join + catalog before the new tables are dropped.
        migrationBuilder.Sql("""
            UPDATE market_products mp
            SET tags = COALESCE(sub.names, '{}')
            FROM (
                SELECT mpt.market_product_id, array_agg(tg.name) AS names
                FROM market_product_tags mpt
                JOIN tags tg ON tg.id = mpt.tag_id
                GROUP BY mpt.market_product_id
            ) sub
            WHERE mp."Id" = sub.market_product_id;
            """);

        migrationBuilder.DropTable(
            name: "market_product_tags");

        migrationBuilder.DropTable(
            name: "tags");

        migrationBuilder.CreateIndex(
            name: "idx_market_products_tags",
            table: "market_products",
            column: "tags")
            .Annotation("Npgsql:IndexMethod", "GIN");
    }
}
