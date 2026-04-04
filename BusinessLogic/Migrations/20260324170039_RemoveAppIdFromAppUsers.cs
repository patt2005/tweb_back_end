using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Groz_Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAppIdFromAppUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AppUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppId",
                table: "AppUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
