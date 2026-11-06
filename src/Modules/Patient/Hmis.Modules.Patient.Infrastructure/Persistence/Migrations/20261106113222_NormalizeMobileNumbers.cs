using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Patient.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMobileNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mobile numbers are stored as digits only (Q-PAT-4) so duplicate detection and search compare like with like.
            migrationBuilder.Sql("""
                UPDATE patient."Patients"
                SET "MobileNumber" = regexp_replace("MobileNumber", '[^0-9]', '', 'g')
                WHERE "MobileNumber" ~ '[^0-9]';
                """);

            // Emergency contacts live in a JSON array; each contact's mobileNumber gets the same treatment.
            migrationBuilder.Sql("""
                UPDATE patient."Patients" p
                SET "EmergencyContacts" = (
                    SELECT COALESCE(jsonb_agg(
                        jsonb_set(c.value, '{mobileNumber}', to_jsonb(regexp_replace(COALESCE(c.value->>'mobileNumber', ''), '[^0-9]', '', 'g')))
                        ORDER BY c.ordinality), '[]'::jsonb)
                    FROM jsonb_array_elements(p."EmergencyContacts") WITH ORDINALITY AS c(value, ordinality))
                WHERE jsonb_typeof(p."EmergencyContacts") = 'array'
                  AND EXISTS (
                    SELECT 1 FROM jsonb_array_elements(p."EmergencyContacts") AS e
                    WHERE COALESCE(e->>'mobileNumber', '') ~ '[^0-9]');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data normalization only; the original formatting is not recoverable and digits-only values remain valid.
        }
    }
}
