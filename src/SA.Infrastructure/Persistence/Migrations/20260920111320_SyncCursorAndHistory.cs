using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncCursorAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncCursor",
                columns: table => new
                {
                    Dataset = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LastDate = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCursor", x => new { x.Dataset, x.Code });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncCursor");
        }
    }
}
