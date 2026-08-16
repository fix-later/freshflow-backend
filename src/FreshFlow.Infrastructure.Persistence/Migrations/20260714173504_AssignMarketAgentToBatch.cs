using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AssignMarketAgentToBatch : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assigned_agent_user_id",
            table: "procurement_batches",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "assigned_at",
            table: "procurement_batches",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_procurement_batches_assigned_agent",
            table: "procurement_batches",
            column: "assigned_agent_user_id",
            filter: "\"assigned_agent_user_id\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_procurement_batches_assigned_agent",
            table: "procurement_batches");

        migrationBuilder.DropColumn(
            name: "assigned_agent_user_id",
            table: "procurement_batches");

        migrationBuilder.DropColumn(
            name: "assigned_at",
            table: "procurement_batches");
    }
}
