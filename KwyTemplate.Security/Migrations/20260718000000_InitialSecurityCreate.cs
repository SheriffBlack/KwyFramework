using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KwyTemplate.Security.Migrations;

/// <inheritdoc />
public partial class InitialSecurityCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Users_UserName",
            table: "Users",
            column: "UserName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Users");
    }
}