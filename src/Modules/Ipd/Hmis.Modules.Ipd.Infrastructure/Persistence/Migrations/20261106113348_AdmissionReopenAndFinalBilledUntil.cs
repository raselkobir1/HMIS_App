using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Ipd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionReopenAndFinalBilledUntil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalBilledUntil",
                schema: "ipd",
                table: "Admissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                schema: "ipd",
                table: "Admissions",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                schema: "ipd",
                table: "Admissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenedBy",
                schema: "ipd",
                table: "Admissions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalBilledUntil",
                schema: "ipd",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                schema: "ipd",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                schema: "ipd",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "ReopenedBy",
                schema: "ipd",
                table: "Admissions");
        }
    }
}
