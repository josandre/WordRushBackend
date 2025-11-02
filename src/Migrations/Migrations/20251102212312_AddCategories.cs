using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordRush.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
          migrationBuilder.Sql(@"
                INSERT INTO ""CategoryColumns"" (""Column"", ""CategoryTypeId"")
                VALUES
                ('Name', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default')),
                ('Country or City', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default')),
                ('Animal', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default')),
                ('Fruit or Food', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default')),
                ('Color', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default')),
                ('Object', (SELECT ""Id"" FROM ""CategoryTypes"" WHERE ""Name"" = 'Default'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
