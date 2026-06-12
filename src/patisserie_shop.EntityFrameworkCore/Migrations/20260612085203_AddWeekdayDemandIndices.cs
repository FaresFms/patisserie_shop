using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekdayDemandIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexFri",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexMon",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexSat",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexSun",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexThu",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexTue",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default

            migrationBuilder.AddColumn<decimal>(
                name: "WeekdayIndexWed",
                table: "IntelligenceProductVelocities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1m); // backfill: 1.0 = neutral weekday index (flat demand) per entity default
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekdayIndexFri",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexMon",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexSat",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexSun",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexThu",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexTue",
                table: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "WeekdayIndexWed",
                table: "IntelligenceProductVelocities");
        }
    }
}
