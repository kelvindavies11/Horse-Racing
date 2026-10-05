using HorseRacing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseRacing.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HorseRacingDbContext))]
[Migration("20261005130000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "horses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                foaled_on = table.Column<DateOnly>(type: "date", nullable: false),
                country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_horses", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "racecourses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_racecourses", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "races",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                racecourse_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                scheduled_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_races", x => x.id);
                table.ForeignKey(
                    name: "fk_races_racecourses_racecourse_id",
                    column: x => x.racecourse_id,
                    principalTable: "racecourses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "runners",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                race_id = table.Column<Guid>(type: "uuid", nullable: false),
                horse_id = table.Column<Guid>(type: "uuid", nullable: false),
                cloth_number = table.Column<int>(type: "integer", nullable: false),
                declared_odds = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_runners", x => x.id);
                table.ForeignKey(
                    name: "fk_runners_horses_horse_id",
                    column: x => x.horse_id,
                    principalTable: "horses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_runners_races_race_id",
                    column: x => x.race_id,
                    principalTable: "races",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_horses_identity",
            table: "horses",
            columns: new[] { "name", "foaled_on", "country_code" });
        migrationBuilder.CreateIndex(
            name: "ux_racecourses_name",
            table: "racecourses",
            column: "name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_races_racecourse_start",
            table: "races",
            columns: new[] { "racecourse_id", "scheduled_start_utc" });
        migrationBuilder.CreateIndex(
            name: "ix_runners_horse_id",
            table: "runners",
            column: "horse_id");
        migrationBuilder.CreateIndex(
            name: "ux_runners_race_cloth_number",
            table: "runners",
            columns: new[] { "race_id", "cloth_number" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_runners_race_horse",
            table: "runners",
            columns: new[] { "race_id", "horse_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "runners");
        migrationBuilder.DropTable(name: "horses");
        migrationBuilder.DropTable(name: "races");
        migrationBuilder.DropTable(name: "racecourses");
    }
}
