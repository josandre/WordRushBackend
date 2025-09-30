using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordRush.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          migrationBuilder.Sql("INSERT INTO \"Roles\" (\"Name\", \"NormalizedName\") VALUES ('HOST', 'HOST')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
