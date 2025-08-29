using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class StalkerAddedFactionsToWarzones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_bands_owner_id",
                table: "stalker_zone_ownerships");

            migrationBuilder.RenameColumn(
                name: "owner_id",
                table: "stalker_zone_ownerships",
                newName: "faction_id");

            migrationBuilder.RenameIndex(
                name: "IX_stalker_zone_ownerships_owner_id",
                table: "stalker_zone_ownerships",
                newName: "IX_stalker_zone_ownerships_faction_id");

            migrationBuilder.AddColumn<int>(
                name: "band_id",
                table: "stalker_zone_ownerships",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stalker_factions",
                columns: table => new
                {
                    stalker_factions_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    faction_proto_id = table.Column<string>(type: "text", nullable: false),
                    reward_points = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_factions", x => x.stalker_factions_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stalker_zone_ownerships_band_id",
                table: "stalker_zone_ownerships",
                column: "band_id");

            migrationBuilder.AddForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_bands_band_id",
                table: "stalker_zone_ownerships",
                column: "band_id",
                principalTable: "stalker_bands",
                principalColumn: "stalker_bands_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_factions_faction_id",
                table: "stalker_zone_ownerships",
                column: "faction_id",
                principalTable: "stalker_factions",
                principalColumn: "stalker_factions_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_bands_band_id",
                table: "stalker_zone_ownerships");

            migrationBuilder.DropForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_factions_faction_id",
                table: "stalker_zone_ownerships");

            migrationBuilder.DropTable(
                name: "stalker_factions");

            migrationBuilder.DropIndex(
                name: "IX_stalker_zone_ownerships_band_id",
                table: "stalker_zone_ownerships");

            migrationBuilder.DropColumn(
                name: "band_id",
                table: "stalker_zone_ownerships");

            migrationBuilder.RenameColumn(
                name: "faction_id",
                table: "stalker_zone_ownerships",
                newName: "owner_id");

            migrationBuilder.RenameIndex(
                name: "IX_stalker_zone_ownerships_faction_id",
                table: "stalker_zone_ownerships",
                newName: "IX_stalker_zone_ownerships_owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_stalker_zone_ownerships_stalker_bands_owner_id",
                table: "stalker_zone_ownerships",
                column: "owner_id",
                principalTable: "stalker_bands",
                principalColumn: "stalker_bands_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
