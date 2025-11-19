using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WordRush.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGameStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TotalPlayedGame = table.Column<int>(type: "integer", nullable: false),
                    WonGames = table.Column<int>(type: "integer", nullable: false),
                    TotalStore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameStatistics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameStatistics_UserId",
                table: "GameStatistics",
                column: "UserId",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO ""GameStatistics"" (""UserId"", ""TotalPlayedGame"", ""WonGames"", ""TotalStore"")
                SELECT ""Id"", 0, 0, 0
                FROM ""Users""
                WHERE ""Id"" NOT IN (SELECT ""UserId"" FROM ""GameStatistics"")
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameStatistics");
        }
    }
}
