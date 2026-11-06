using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.HR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEmployeeMobileNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Employee mobile numbers are stored as digits only, matching the Patient module.
            migrationBuilder.Sql("""
                UPDATE hr."Employees"
                SET "MobileNumber" = regexp_replace("MobileNumber", '[^0-9]', '', 'g')
                WHERE "MobileNumber" ~ '[^0-9]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data normalization only; the original formatting is not recoverable and digits-only values remain valid.
        }
    }
}
