using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichCampusAndHqMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Campuses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Campuses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Campuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EstablishedOn",
                table: "Campuses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InChargeDesignation",
                table: "Campuses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InChargeEmail",
                table: "Campuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InChargeName",
                table: "Campuses",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InChargePhone",
                table: "Campuses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Campuses",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Campuses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Campuses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentCapacity",
                table: "Campuses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Campuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "EstablishedOn",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "InChargeDesignation",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "InChargeEmail",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "InChargeName",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "InChargePhone",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "StudentCapacity",
                table: "Campuses");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Campuses");
        }
    }
}
