using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiMorph.Orchestrator.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage8Automation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Repositories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastScanAt",
                table: "Repositories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Providers",
                table: "Repositories",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "stripe");

            migrationBuilder.AddColumn<string>(
                name: "ScheduleCron",
                table: "Repositories",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookBranches",
                table: "Repositories",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AutomationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GitHubOwner = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GitHubRepo = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatePullRequest = table.Column<bool>(type: "INTEGER", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    ScanJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderFeedStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastChangedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderFeedStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationJobs_DedupeKey",
                table: "AutomationJobs",
                column: "DedupeKey");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationJobs_Status",
                table: "AutomationJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderFeedStates_Provider",
                table: "ProviderFeedStates",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationJobs");

            migrationBuilder.DropTable(
                name: "ProviderFeedStates");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LastScanAt",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "Providers",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "ScheduleCron",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "WebhookBranches",
                table: "Repositories");
        }
    }
}
