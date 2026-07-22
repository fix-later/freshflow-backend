using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FixUserPhoneUniqueIndexSoftDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_Phone",
            table: "users");

        migrationBuilder.CreateIndex(
            name: "IX_users_Phone",
            table: "users",
            column: "Phone",
            unique: true,
            filter: "\"Phone\" IS NOT NULL AND \"DeletedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_users_Phone",
            table: "users");

        migrationBuilder.CreateIndex(
            name: "IX_users_Phone",
            table: "users",
            column: "Phone",
            unique: true,
            filter: "\"Phone\" IS NOT NULL");
    }
}
