using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordRush.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          migrationBuilder.Sql("INSERT INTO \"CategoryTypes\" (\"Name\") VALUES ('Default')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
