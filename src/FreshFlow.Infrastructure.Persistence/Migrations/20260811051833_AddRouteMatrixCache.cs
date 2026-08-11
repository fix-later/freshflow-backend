using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRouteMatrixCache : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "route_matrix_cache",
            columns: table => new
            {
                pair_key = table.Column<string>(type: "text", nullable: false),
                profile = table.Column<string>(type: "text", nullable: false),
                from_lat = table.Column<decimal>(type: "numeric", nullable: false),
                from_lng = table.Column<decimal>(type: "numeric", nullable: false),
                to_lat = table.Column<decimal>(type: "numeric", nullable: false),
                to_lng = table.Column<decimal>(type: "numeric", nullable: false),
                distance_meters = table.Column<long>(type: "bigint", nullable: false),
                duration_seconds = table.Column<long>(type: "bigint", nullable: false),
                calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_route_matrix_cache", x => x.pair_key);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "route_matrix_cache");
    }
}
