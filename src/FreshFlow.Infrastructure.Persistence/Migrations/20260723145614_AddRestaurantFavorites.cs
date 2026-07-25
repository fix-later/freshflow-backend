using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRestaurantFavorites : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "restaurant_favorites",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                market_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_restaurant_favorites", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_restaurant_favorites_restaurant_id",
            table: "restaurant_favorites",
            column: "restaurant_id");

        migrationBuilder.CreateIndex(
            name: "ux_restaurant_favorites_restaurant_id_market_product_id",
            table: "restaurant_favorites",
            columns: new[] { "restaurant_id", "market_product_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "restaurant_favorites");
    }
}
