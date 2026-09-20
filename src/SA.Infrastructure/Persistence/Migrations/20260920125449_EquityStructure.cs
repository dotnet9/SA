using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquityStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HolderCount",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    HolderNum = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousHolderNum = table.Column<int>(type: "INTEGER", nullable: true),
                    HolderNumChange = table.Column<int>(type: "INTEGER", nullable: true),
                    HolderNumRatio = table.Column<double>(type: "REAL", nullable: true),
                    AvgHoldNum = table.Column<double>(type: "REAL", nullable: true),
                    AvgMarketCap = table.Column<double>(type: "REAL", nullable: true),
                    TotalMarketCap = table.Column<double>(type: "REAL", nullable: true),
                    TotalShares = table.Column<double>(type: "REAL", nullable: true),
                    ChangeReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ReportName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolderCount", x => new { x.Code, x.EndDate });
                });

            migrationBuilder.CreateTable(
                name: "PledgeStat",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TradeDate = table.Column<string>(type: "TEXT", nullable: false),
                    PledgeRatio = table.Column<double>(type: "REAL", nullable: true),
                    PledgeSharesWan = table.Column<double>(type: "REAL", nullable: true),
                    PledgeDealNum = table.Column<int>(type: "INTEGER", nullable: true),
                    PledgeMarketCapWan = table.Column<double>(type: "REAL", nullable: true),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Year1ChangePercent = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PledgeStat", x => new { x.Code, x.TradeDate });
                });

            migrationBuilder.CreateTable(
                name: "TopHolder",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFreeFloat = table.Column<bool>(type: "INTEGER", nullable: false),
                    HolderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    HoldNum = table.Column<double>(type: "REAL", nullable: false),
                    HoldRatio = table.Column<double>(type: "REAL", nullable: true),
                    FreeHoldRatio = table.Column<double>(type: "REAL", nullable: true),
                    HoldChange = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    HolderType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SharesType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    MarketCap = table.Column<double>(type: "REAL", nullable: true),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopHolder", x => new { x.Code, x.EndDate, x.IsFreeFloat, x.Rank });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HolderCount");

            migrationBuilder.DropTable(
                name: "PledgeStat");

            migrationBuilder.DropTable(
                name: "TopHolder");
        }
    }
}
