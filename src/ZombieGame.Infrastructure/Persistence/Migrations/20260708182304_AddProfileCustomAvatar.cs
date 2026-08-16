using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZombieGame.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileCustomAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomAvatarData",
                table: "PlayerProfiles",
                type: "nvarchar(max)",
                maxLength: 512000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomAvatarData",
                table: "PlayerProfiles");
        }
    }
}
