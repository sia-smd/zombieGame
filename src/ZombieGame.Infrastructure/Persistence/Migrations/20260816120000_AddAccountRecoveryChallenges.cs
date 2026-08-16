using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZombieGame.Infrastructure.Persistence;

#nullable disable

namespace ZombieGame.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260816120000_AddAccountRecoveryChallenges")]
    public partial class AddAccountRecoveryChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountRecoveryChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRecoveryChallenges_Users_GuestPlayerId",
                        column: x => x.GuestPlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountRecoveryChallenges_Users_TargetPlayerId",
                        column: x => x.TargetPlayerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryChallenges_TargetPlayerId",
                table: "AccountRecoveryChallenges",
                column: "TargetPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryChallenges_GuestPlayerId_TargetPlayerId_Purpose_ConsumedAt",
                table: "AccountRecoveryChallenges",
                columns: new[] { "GuestPlayerId", "TargetPlayerId", "Purpose", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRecoveryChallenges");
        }
    }
}
