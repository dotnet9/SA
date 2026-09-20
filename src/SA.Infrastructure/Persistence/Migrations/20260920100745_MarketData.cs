using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectTaskLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    StartedAt = table.Column<string>(type: "TEXT", nullable: false),
                    CostMs = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    RowsWritten = table.Column<int>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RetryResult = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectTaskLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSourceStatus",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Domains = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    LastOkAt = table.Column<string>(type: "TEXT", nullable: true),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: true),
                    FailCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UptimePct = table.Column<double>(type: "REAL", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceStatus", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "IndexQuote",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Displayed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Change = table.Column<double>(type: "REAL", nullable: false),
                    Pct = table.Column<double>(type: "REAL", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: false),
                    Amount = table.Column<double>(type: "REAL", nullable: false),
                    AsOf = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexQuote", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Instrument",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Pinyin = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Board = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Industry = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsSt = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedOn = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instrument", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "MarketStat",
                columns: table => new
                {
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    LimitUp = table.Column<int>(type: "INTEGER", nullable: false),
                    LimitDown = table.Column<int>(type: "INTEGER", nullable: false),
                    MainNet = table.Column<double>(type: "REAL", nullable: false),
                    SuperLarge = table.Column<double>(type: "REAL", nullable: false),
                    Large = table.Column<double>(type: "REAL", nullable: false),
                    Medium = table.Column<double>(type: "REAL", nullable: false),
                    Small = table.Column<double>(type: "REAL", nullable: false),
                    FinanceBalance = table.Column<double>(type: "REAL", nullable: false),
                    LoanBalance = table.Column<double>(type: "REAL", nullable: false),
                    MarginDate = table.Column<string>(type: "TEXT", nullable: true),
                    FundFlowDate = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketStat", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "QuoteSnapshot",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Change = table.Column<double>(type: "REAL", nullable: false),
                    Pct = table.Column<double>(type: "REAL", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: false),
                    Amount = table.Column<double>(type: "REAL", nullable: false),
                    Turnover = table.Column<double>(type: "REAL", nullable: false),
                    VolRatio = table.Column<double>(type: "REAL", nullable: false),
                    Open = table.Column<double>(type: "REAL", nullable: false),
                    High = table.Column<double>(type: "REAL", nullable: false),
                    Low = table.Column<double>(type: "REAL", nullable: false),
                    PrevClose = table.Column<double>(type: "REAL", nullable: false),
                    MarketCap = table.Column<double>(type: "REAL", nullable: false),
                    FloatCap = table.Column<double>(type: "REAL", nullable: false),
                    Pe = table.Column<double>(type: "REAL", nullable: false),
                    PeTtm = table.Column<double>(type: "REAL", nullable: false),
                    Pb = table.Column<double>(type: "REAL", nullable: false),
                    AsOf = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteSnapshot", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Sector",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Pct = table.Column<double>(type: "REAL", nullable: false),
                    MainNet = table.Column<double>(type: "REAL", nullable: false),
                    UpCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DownCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaderName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LeaderCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Pe = table.Column<double>(type: "REAL", nullable: false),
                    AsOf = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sector", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TradingDay",
                columns: table => new
                {
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    IsOpen = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingDay", x => x.Date);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectTaskLog_StartedAt",
                table: "CollectTaskLog",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Instrument_Industry",
                table: "Instrument",
                column: "Industry");

            migrationBuilder.CreateIndex(
                name: "IX_Instrument_Name",
                table: "Instrument",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Instrument_Pinyin",
                table: "Instrument",
                column: "Pinyin");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteSnapshot_Amount",
                table: "QuoteSnapshot",
                column: "Amount");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteSnapshot_Pct",
                table: "QuoteSnapshot",
                column: "Pct");

            migrationBuilder.CreateIndex(
                name: "IX_Sector_Pct",
                table: "Sector",
                column: "Pct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectTaskLog");

            migrationBuilder.DropTable(
                name: "DataSourceStatus");

            migrationBuilder.DropTable(
                name: "IndexQuote");

            migrationBuilder.DropTable(
                name: "Instrument");

            migrationBuilder.DropTable(
                name: "MarketStat");

            migrationBuilder.DropTable(
                name: "QuoteSnapshot");

            migrationBuilder.DropTable(
                name: "Sector");

            migrationBuilder.DropTable(
                name: "TradingDay");
        }
    }
}
