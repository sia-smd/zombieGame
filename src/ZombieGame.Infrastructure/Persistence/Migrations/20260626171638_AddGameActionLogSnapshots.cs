using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZombieGame.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameActionLogSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StateSnapshotJson",
                table: "GameActionLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StateSnapshotJson",
                table: "GameActionLogs");
        }
    }
}
