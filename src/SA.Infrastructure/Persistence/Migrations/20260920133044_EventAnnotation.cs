using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventAnnotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventAnnotation",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Impact = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAnnotation", x => new { x.Code, x.EventKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventAnnotation_UpdatedAt",
                table: "EventAnnotation",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventAnnotation");
        }
    }
}
