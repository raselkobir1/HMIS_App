using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Ipd.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIpd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ipd");

            migrationBuilder.CreateTable(
                name: "Admissions",
                schema: "ipd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendingDoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyContactRelation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AdmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DischargeOrderedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DischargeOrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DischargeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FinalDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HospitalCourse = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DischargeMedications = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DischargeAdvice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DischargedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedWithDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReleaseOverrideReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ReleaseOverrideBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedCategories",
                schema: "ipd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SuggestedDeposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedCategories", x => x.Id);
                    table.CheckConstraint("CK_BedCategories_Amounts", "\"DailyRate\" >= 0 AND \"SuggestedDeposit\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Wards",
                schema: "ipd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Floor = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Beds",
                schema: "ipd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WardId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Room = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beds_BedCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "ipd",
                        principalTable: "BedCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Beds_Wards_WardId",
                        column: x => x.WardId,
                        principalSchema: "ipd",
                        principalTable: "Wards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BedAssignments",
                schema: "ipd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BedId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BedAssignments_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalSchema: "ipd",
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BedAssignments_Beds_BedId",
                        column: x => x.BedId,
                        principalSchema: "ipd",
                        principalTable: "Beds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_AdmissionNumber",
                schema: "ipd",
                table: "Admissions",
                column: "AdmissionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_AttendingDoctorId",
                schema: "ipd",
                table: "Admissions",
                column: "AttendingDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_PatientId_Active",
                schema: "ipd",
                table: "Admissions",
                column: "PatientId",
                unique: true,
                filter: "\"Status\" <> 'Discharged'");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_PatientId_AdmittedAt",
                schema: "ipd",
                table: "Admissions",
                columns: new[] { "PatientId", "AdmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_Status_AdmittedAt",
                schema: "ipd",
                table: "Admissions",
                columns: new[] { "Status", "AdmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BedAssignments_AdmissionId",
                schema: "ipd",
                table: "BedAssignments",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_BedAssignments_BedId_Open",
                schema: "ipd",
                table: "BedAssignments",
                column: "BedId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_CategoryId",
                schema: "ipd",
                table: "Beds",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_Status",
                schema: "ipd",
                table: "Beds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_WardId_BedNumber",
                schema: "ipd",
                table: "Beds",
                columns: new[] { "WardId", "BedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wards_Code",
                schema: "ipd",
                table: "Wards",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BedAssignments",
                schema: "ipd");

            migrationBuilder.DropTable(
                name: "Admissions",
                schema: "ipd");

            migrationBuilder.DropTable(
                name: "Beds",
                schema: "ipd");

            migrationBuilder.DropTable(
                name: "BedCategories",
                schema: "ipd");

            migrationBuilder.DropTable(
                name: "Wards",
                schema: "ipd");
        }
    }
}
