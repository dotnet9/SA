using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Rating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RatingConsensus",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RatingOrgNum = table.Column<int>(type: "INTEGER", nullable: false),
                    BuyNum = table.Column<int>(type: "INTEGER", nullable: false),
                    AddNum = table.Column<int>(type: "INTEGER", nullable: false),
                    NeutralNum = table.Column<int>(type: "INTEGER", nullable: true),
                    ReduceNum = table.Column<int>(type: "INTEGER", nullable: true),
                    SaleNum = table.Column<int>(type: "INTEGER", nullable: true),
                    AimPriceMax = table.Column<double>(type: "REAL", nullable: true),
                    AimPriceMin = table.Column<double>(type: "REAL", nullable: true),
                    LongTermNum = table.Column<int>(type: "INTEGER", nullable: true),
                    Year1 = table.Column<int>(type: "INTEGER", nullable: true),
                    Eps1 = table.Column<double>(type: "REAL", nullable: true),
                    YearMark1 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    Year2 = table.Column<int>(type: "INTEGER", nullable: true),
                    Eps2 = table.Column<double>(type: "REAL", nullable: true),
                    YearMark2 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    Year3 = table.Column<int>(type: "INTEGER", nullable: true),
                    Eps3 = table.Column<double>(type: "REAL", nullable: true),
                    YearMark3 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    Year4 = table.Column<int>(type: "INTEGER", nullable: true),
                    Eps4 = table.Column<double>(type: "REAL", nullable: true),
                    YearMark4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    IndustryBoard = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingConsensus", x => x.Code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RatingConsensus");
        }
    }
}
