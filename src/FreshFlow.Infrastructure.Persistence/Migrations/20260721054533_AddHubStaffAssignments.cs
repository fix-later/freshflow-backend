using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubStaffAssignments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hub_staff_assignments",
            columns: table => new
            {
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_staff_assignments", x => new { x.hub_id, x.user_id });
                table.ForeignKey(
                    name: "fk_hub_staff_assignments_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_hub_staff_assignments_user_id",
            table: "hub_staff_assignments",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hub_staff_assignments");
    }
}
