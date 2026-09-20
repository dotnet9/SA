using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Watchlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchGroup",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchGroup_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchItem",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    GroupId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchItem", x => new { x.UserId, x.Code });
                    table.ForeignKey(
                        name: "FK_WatchItem_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchItem_WatchGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "WatchGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchGroup_UserId_SortOrder",
                table: "WatchGroup",
                columns: new[] { "UserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchItem_GroupId",
                table: "WatchItem",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchItem");

            migrationBuilder.DropTable(
                name: "WatchGroup");
        }
    }
}
