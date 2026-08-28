using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiMorph.Orchestrator.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage3ScanAndPullRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanJobs_Repositories_RepositoryId",
                table: "ScanJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "ScanJobs",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "ScanJobs",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PullRequestNumber",
                table: "ScanJobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PullRequestUrl",
                table: "ScanJobs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryPath",
                table: "ScanJobs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScanJobs_Repositories_RepositoryId",
                table: "ScanJobs",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanJobs_Repositories_RepositoryId",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "PullRequestNumber",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "PullRequestUrl",
                table: "ScanJobs");

            migrationBuilder.DropColumn(
                name: "RepositoryPath",
                table: "ScanJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "ScanJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScanJobs_Repositories_RepositoryId",
                table: "ScanJobs",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
