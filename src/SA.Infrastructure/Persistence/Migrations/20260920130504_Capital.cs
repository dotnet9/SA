using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Capital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillboardRecord",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TradeDate = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Explain = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    ChangePercent = table.Column<double>(type: "REAL", nullable: true),
                    TurnoverRate = table.Column<double>(type: "REAL", nullable: true),
                    NetAmount = table.Column<double>(type: "REAL", nullable: true),
                    BuyAmount = table.Column<double>(type: "REAL", nullable: true),
                    SellAmount = table.Column<double>(type: "REAL", nullable: true),
                    DealAmount = table.Column<double>(type: "REAL", nullable: true),
                    Next1Change = table.Column<double>(type: "REAL", nullable: true),
                    Next5Change = table.Column<double>(type: "REAL", nullable: true),
                    Next10Change = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillboardRecord", x => new { x.Code, x.TradeDate, x.Reason });
                });

            migrationBuilder.CreateTable(
                name: "BlockTrade",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TradeDate = table.Column<string>(type: "TEXT", nullable: false),
                    DealPrice = table.Column<double>(type: "REAL", nullable: true),
                    PremiumRatio = table.Column<double>(type: "REAL", nullable: true),
                    DealVolume = table.Column<double>(type: "REAL", nullable: true),
                    DealAmount = table.Column<double>(type: "REAL", nullable: true),
                    BuyerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SellerName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockTrade", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundFlowDaily",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    MainNet = table.Column<double>(type: "REAL", nullable: false),
                    SuperLargeNet = table.Column<double>(type: "REAL", nullable: false),
                    LargeNet = table.Column<double>(type: "REAL", nullable: false),
                    MediumNet = table.Column<double>(type: "REAL", nullable: false),
                    SmallNet = table.Column<double>(type: "REAL", nullable: false),
                    MainRatio = table.Column<double>(type: "REAL", nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    ChangePercent = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundFlowDaily", x => new { x.Code, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "MarginDetail",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    FinanceBalance = table.Column<double>(type: "REAL", nullable: true),
                    FinanceBuy = table.Column<double>(type: "REAL", nullable: true),
                    FinanceNetBuy = table.Column<double>(type: "REAL", nullable: true),
                    LoanBalance = table.Column<double>(type: "REAL", nullable: true),
                    LoanVolume = table.Column<double>(type: "REAL", nullable: true),
                    TotalBalance = table.Column<double>(type: "REAL", nullable: true),
                    FinanceBalanceRatio = table.Column<double>(type: "REAL", nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    ChangePercent = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarginDetail", x => new { x.Code, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "NorthboundHolding",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    HoldDate = table.Column<string>(type: "TEXT", nullable: false),
                    DateType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    HoldShares = table.Column<double>(type: "REAL", nullable: true),
                    PreviousHoldShares = table.Column<double>(type: "REAL", nullable: true),
                    AddShares = table.Column<double>(type: "REAL", nullable: true),
                    AddSharesAmp = table.Column<double>(type: "REAL", nullable: true),
                    HoldMarketCap = table.Column<double>(type: "REAL", nullable: true),
                    OrgQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousOrgQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    FreeSharesRatio = table.Column<double>(type: "REAL", nullable: true),
                    TotalSharesRatio = table.Column<double>(type: "REAL", nullable: true),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NorthboundHolding", x => new { x.Code, x.HoldDate });
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockTrade_Code_TradeDate",
                table: "BlockTrade",
                columns: new[] { "Code", "TradeDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillboardRecord");

            migrationBuilder.DropTable(
                name: "BlockTrade");

            migrationBuilder.DropTable(
                name: "FundFlowDaily");

            migrationBuilder.DropTable(
                name: "MarginDetail");

            migrationBuilder.DropTable(
                name: "NorthboundHolding");
        }
    }
}
