using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMarketSessionResources : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "planned_capacity_kg",
            table: "market_sessions",
            type: "numeric(12,3)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "market_session_agents",
            columns: table => new
            {
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_session_agents", x => new { x.session_id, x.user_id });
                table.ForeignKey(
                    name: "FK_market_session_agents_market_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "market_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_market_session_agents_user",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "market_session_vehicles",
            columns: table => new
            {
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_session_vehicles", x => new { x.session_id, x.vehicle_id });
                table.ForeignKey(
                    name: "FK_market_session_vehicles_market_sessions_session_id",
                    column: x => x.session_id,
                    principalTable: "market_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_market_session_vehicles_vehicle",
                    column: x => x.vehicle_id,
                    principalTable: "vehicles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_market_session_agents_user_id",
            table: "market_session_agents",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_market_session_vehicles_vehicle_id",
            table: "market_session_vehicles",
            column: "vehicle_id");

        migrationBuilder.Sql(
            """
            INSERT INTO market_session_agents (session_id, user_id, assigned_by, assigned_at)
            SELECT session.id, assignment."UserId", NULL, NOW()
            FROM market_sessions AS session
            JOIN user_market_assignments AS assignment
              ON assignment."MarketId" = session.market_id
            JOIN users AS u ON u."Id" = assignment."UserId"
            JOIN roles AS r ON r."Id" = u."RoleId"
            WHERE session.status <> 'Closed'
              AND session.deleted_at IS NULL
              AND u."IsActive" = TRUE
              AND u."DeletedAt" IS NULL
              AND r."Name" = 'market_agent'
            ON CONFLICT DO NOTHING;

            INSERT INTO market_session_vehicles (session_id, vehicle_id, assigned_by, assigned_at)
            SELECT session.id, vehicle.id, NULL, NOW()
            FROM market_sessions AS session
            JOIN vehicles AS vehicle ON vehicle.hub_id = session.hub_id
            WHERE session.status <> 'Closed'
              AND session.deleted_at IS NULL
              AND vehicle.is_available = TRUE
              AND vehicle.deleted_at IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM delivery_routes AS route
                  WHERE route.vehicle_id = vehicle.id
                    AND route.service_date = session.service_date
                    AND route.status <> 'cancelled'
                    AND route.deleted_at IS NULL)
            ON CONFLICT DO NOTHING;

            UPDATE market_sessions AS session
            SET planned_capacity_kg = selected.capacity_kg
            FROM (
                SELECT assignment.session_id, SUM(vehicle.capacity_kg) AS capacity_kg
                FROM market_session_vehicles AS assignment
                JOIN vehicles AS vehicle ON vehicle.id = assignment.vehicle_id
                GROUP BY assignment.session_id
            ) AS selected
            WHERE session.id = selected.session_id
              AND session.planned_capacity_kg IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "market_session_agents");

        migrationBuilder.DropTable(
            name: "market_session_vehicles");

        migrationBuilder.DropColumn(
            name: "planned_capacity_kg",
            table: "market_sessions");
    }
}
