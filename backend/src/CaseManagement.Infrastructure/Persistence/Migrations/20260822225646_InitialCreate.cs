using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CaseManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CreatedAt",
                table: "Cases",
                column: "CreatedAt",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "Title", "OrganizationName", "Status", "Priority", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OrganizationName",
                table: "Cases",
                column: "OrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Priority_CreatedAt",
                table: "Cases",
                columns: new[] { "Priority", "CreatedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Title", "OrganizationName", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status_CreatedAt",
                table: "Cases",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Title", "OrganizationName", "Priority", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cases");
        }
    }
}
