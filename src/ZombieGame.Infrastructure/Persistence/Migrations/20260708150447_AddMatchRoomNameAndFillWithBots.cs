using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZombieGame.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRoomNameAndFillWithBots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FillWithBots",
                table: "Matches",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Matches",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FillWithBots",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Matches");
        }
    }
}
