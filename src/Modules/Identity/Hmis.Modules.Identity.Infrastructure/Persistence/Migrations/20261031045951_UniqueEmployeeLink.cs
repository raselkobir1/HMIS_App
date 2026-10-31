using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueEmployeeLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                schema: "auth",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                schema: "auth",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "\"EmployeeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                schema: "auth",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                schema: "auth",
                table: "Users",
                column: "EmployeeId");
        }
    }
}
