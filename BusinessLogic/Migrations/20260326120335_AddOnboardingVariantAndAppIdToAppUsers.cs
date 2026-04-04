using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Groz_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingVariantAndAppIdToAppUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppId",
                table: "AppUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OnboardingVariant",
                table: "AppUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "OnboardingVariant",
                table: "AppUsers");
        }
    }
}
