using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiMorph.Orchestrator.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage6PatchMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatchCount",
                table: "ScanJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PatchMode",
                table: "ScanJobs",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "detect-only");

            migrationBuilder.AddColumn<string>(
                name: "PatchesJson",
                table: "ScanJobs",
                type: "TEXT",
                maxLength: 16384,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatchCount",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "PatchMode",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "PatchesJson",
                table: "ScanJobs");
        }
    }
}
