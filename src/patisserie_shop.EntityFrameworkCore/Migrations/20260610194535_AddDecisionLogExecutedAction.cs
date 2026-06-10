using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionLogExecutedAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IntelligenceDecisionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Pending");

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutedActionId",
                table: "IntelligenceDecisionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedActionType",
                table: "IntelligenceDecisionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutedActionId",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "ExecutedActionType",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IntelligenceDecisionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
