using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Finance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EarningsForecast",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReportDate = table.Column<string>(type: "TEXT", nullable: false),
                    Caliber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ForecastType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    NetProfitMin = table.Column<double>(type: "REAL", nullable: true),
                    NetProfitMax = table.Column<double>(type: "REAL", nullable: true),
                    ChangeMin = table.Column<double>(type: "REAL", nullable: true),
                    ChangeMax = table.Column<double>(type: "REAL", nullable: true),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarningsForecast", x => new { x.Code, x.ReportDate });
                });

            migrationBuilder.CreateTable(
                name: "FinancialReport",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReportDate = table.Column<string>(type: "TEXT", nullable: false),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Quarter = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Revenue = table.Column<double>(type: "REAL", nullable: true),
                    RevenueYoy = table.Column<double>(type: "REAL", nullable: true),
                    NetProfit = table.Column<double>(type: "REAL", nullable: true),
                    NetProfitYoy = table.Column<double>(type: "REAL", nullable: true),
                    DeductedEps = table.Column<double>(type: "REAL", nullable: true),
                    Eps = table.Column<double>(type: "REAL", nullable: true),
                    Roe = table.Column<double>(type: "REAL", nullable: true),
                    Bps = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlowPerShare = table.Column<double>(type: "REAL", nullable: true),
                    GrossMargin = table.Column<double>(type: "REAL", nullable: true),
                    RevenueQoq = table.Column<double>(type: "REAL", nullable: true),
                    NetProfitQoq = table.Column<double>(type: "REAL", nullable: true),
                    DividendPlan = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DividendYield = table.Column<double>(type: "REAL", nullable: true),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: true),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialReport", x => new { x.Code, x.ReportDate });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EarningsForecast");

            migrationBuilder.DropTable(
                name: "FinancialReport");
        }
    }
}
