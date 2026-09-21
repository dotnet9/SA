using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FundamentalMetric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundamentalMetric",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReportDate = table.Column<string>(type: "TEXT", nullable: false),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    OrgType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: true),
                    RoeWeighted = table.Column<double>(type: "REAL", nullable: true),
                    RoeDeducted = table.Column<double>(type: "REAL", nullable: true),
                    GrossMargin = table.Column<double>(type: "REAL", nullable: true),
                    NetMargin = table.Column<double>(type: "REAL", nullable: true),
                    Roic = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlowToRevenue = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlow = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlowToNetProfit = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlowToOperatingProfit = table.Column<double>(type: "REAL", nullable: true),
                    FreeCashFlow = table.Column<double>(type: "REAL", nullable: true),
                    DebtRatio = table.Column<double>(type: "REAL", nullable: true),
                    CurrentRatio = table.Column<double>(type: "REAL", nullable: true),
                    QuickRatio = table.Column<double>(type: "REAL", nullable: true),
                    InterestDebtRatio = table.Column<double>(type: "REAL", nullable: true),
                    InterestCoverageRatio = table.Column<double>(type: "REAL", nullable: true),
                    LiquidationRatio = table.Column<double>(type: "REAL", nullable: true),
                    InventoryTurnoverDays = table.Column<double>(type: "REAL", nullable: true),
                    ReceivableTurnoverDays = table.Column<double>(type: "REAL", nullable: true),
                    AssetTurnoverDays = table.Column<double>(type: "REAL", nullable: true),
                    RevenueYoy = table.Column<double>(type: "REAL", nullable: true),
                    NetProfitYoy = table.Column<double>(type: "REAL", nullable: true),
                    DeductedNetProfitYoy = table.Column<double>(type: "REAL", nullable: true),
                    Eps = table.Column<double>(type: "REAL", nullable: true),
                    EpsDeducted = table.Column<double>(type: "REAL", nullable: true),
                    Bps = table.Column<double>(type: "REAL", nullable: true),
                    OperatingCashFlowPerShare = table.Column<double>(type: "REAL", nullable: true),
                    RndExpense = table.Column<double>(type: "REAL", nullable: true),
                    RndExpenseRatio = table.Column<double>(type: "REAL", nullable: true),
                    RndPersonnel = table.Column<double>(type: "REAL", nullable: true),
                    Revenue = table.Column<double>(type: "REAL", nullable: true),
                    NetProfit = table.Column<double>(type: "REAL", nullable: true),
                    TotalAssets = table.Column<double>(type: "REAL", nullable: true),
                    TotalEquity = table.Column<double>(type: "REAL", nullable: true),
                    Liability = table.Column<double>(type: "REAL", nullable: true),
                    TotalShare = table.Column<double>(type: "REAL", nullable: true),
                    FreeShare = table.Column<double>(type: "REAL", nullable: true),
                    StaffNumber = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundamentalMetric", x => new { x.Code, x.ReportDate });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalMetric_Code_ReportDate",
                table: "FundamentalMetric",
                columns: new[] { "Code", "ReportDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalMetric_ReportDate",
                table: "FundamentalMetric",
                column: "ReportDate");

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalMetric_ReportType_ReportDate",
                table: "FundamentalMetric",
                columns: new[] { "ReportType", "ReportDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundamentalMetric");
        }
    }
}
