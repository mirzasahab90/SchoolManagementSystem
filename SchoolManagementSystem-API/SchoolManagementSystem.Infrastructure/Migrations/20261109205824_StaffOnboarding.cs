using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Staff_Cnic",
                table: "Staff");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Staff",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "Staff",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Staff",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContractSigned",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractSignedOn",
                table: "Staff",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Staff",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyName",
                table: "Staff",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyPhone",
                table: "Staff",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyRelation",
                table: "Staff",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExperienceYears",
                table: "Staff",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Staff",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaveReason",
                table: "Staff",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LeftOn",
                table: "Staff",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrientationDone",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrientationDoneOn",
                table: "Staff",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "Staff",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousEmployer",
                table: "Staff",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Qualification",
                table: "Staff",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Staff",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassTeacherStaffId",
                table: "Sections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDocuments_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampusId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AcademicSessionId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_AcademicSessions_AcademicSessionId",
                        column: x => x.AcademicSessionId,
                        principalTable: "AcademicSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeachingAssignments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Cnic",
                table: "Staff",
                column: "Cnic",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Cnic] IS NOT NULL AND [Status] <> 'Resigned' AND [Status] <> 'Terminated'");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_UserId",
                table: "Staff",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_ClassTeacherStaffId",
                table: "Sections",
                column: "ClassTeacherStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDocuments_StaffId",
                table: "StaffDocuments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_AcademicSessionId",
                table: "TeachingAssignments",
                column: "AcademicSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_SectionId_SubjectId",
                table: "TeachingAssignments",
                columns: new[] { "SectionId", "SubjectId" },
                unique: true,
                filter: "[EndedOn] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_StaffId",
                table: "TeachingAssignments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignments_SubjectId",
                table: "TeachingAssignments",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Staff_ClassTeacherStaffId",
                table: "Sections",
                column: "ClassTeacherStaffId",
                principalTable: "Staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Staff_Users_UserId",
                table: "Staff",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Staff_ClassTeacherStaffId",
                table: "Sections");

            migrationBuilder.DropForeignKey(
                name: "FK_Staff_Users_UserId",
                table: "Staff");

            migrationBuilder.DropTable(
                name: "StaffDocuments");

            migrationBuilder.DropTable(
                name: "TeachingAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Staff_Cnic",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_Staff_UserId",
                table: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_Sections_ClassTeacherStaffId",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "ContractSigned",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "ContractSignedOn",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "EmergencyName",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "EmergencyPhone",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "EmergencyRelation",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "ExperienceYears",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "LeaveReason",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "LeftOn",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "OrientationDone",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "OrientationDoneOn",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "PreviousEmployer",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "Qualification",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "ClassTeacherStaffId",
                table: "Sections");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Cnic",
                table: "Staff",
                column: "Cnic",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Cnic] IS NOT NULL");
        }
    }
}
