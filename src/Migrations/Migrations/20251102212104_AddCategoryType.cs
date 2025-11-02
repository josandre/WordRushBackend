using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordRush.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Crear tabla CategoryTypes
            migrationBuilder.CreateTable(
                name: "CategoryTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTypes", x => x.Id);
                });

            // 2. Crear tabla CategoryColumns
            migrationBuilder.CreateTable(
                name: "CategoryColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Column = table.Column<string>(type: "text", nullable: false),
                    CategoryTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryColumns_CategoryTypes_CategoryTypeId",
                        column: x => x.CategoryTypeId,
                        principalTable: "CategoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 3. Índice para la FK
            migrationBuilder.CreateIndex(
                name: "IX_CategoryColumns_CategoryTypeId",
                table: "CategoryColumns",
                column: "CategoryTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Borrar en orden inverso
            migrationBuilder.DropTable(
                name: "CategoryColumns");

            migrationBuilder.DropTable(
                name: "CategoryTypes");
        }
    }
}
