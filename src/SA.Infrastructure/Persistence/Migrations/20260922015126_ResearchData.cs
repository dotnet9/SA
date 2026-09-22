using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResearchData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcement",
                columns: table => new
                {
                    ArtCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    NoticeDate = table.Column<string>(type: "TEXT", nullable: false),
                    ColumnName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ColumnNames = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AnnType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcement", x => x.ArtCode);
                });

            migrationBuilder.CreateTable(
                name: "BusinessComposition",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ReportDate = table.Column<string>(type: "TEXT", nullable: false),
                    MainOpType = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Income = table.Column<double>(type: "REAL", nullable: true),
                    IncomeRatio = table.Column<double>(type: "REAL", nullable: true),
                    Cost = table.Column<double>(type: "REAL", nullable: true),
                    CostRatio = table.Column<double>(type: "REAL", nullable: true),
                    Profit = table.Column<double>(type: "REAL", nullable: true),
                    ProfitRatio = table.Column<double>(type: "REAL", nullable: true),
                    GrossProfitRatio = table.Column<double>(type: "REAL", nullable: true),
                    IsSubItem = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessComposition", x => new { x.Code, x.ReportDate, x.MainOpType, x.ItemName });
                });

            migrationBuilder.CreateTable(
                name: "BusinessProfile",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BusinessScope = table.Column<string>(type: "TEXT", nullable: true),
                    BusinessReview = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfile", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "ResearchReport",
                columns: table => new
                {
                    InfoCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OrgName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OrgShortName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Researcher = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PublishDate = table.Column<string>(type: "TEXT", nullable: false),
                    RatingName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    RatingChange = table.Column<int>(type: "INTEGER", nullable: true),
                    IndustryName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PredictThisYearEps = table.Column<double>(type: "REAL", nullable: true),
                    PredictThisYearPe = table.Column<double>(type: "REAL", nullable: true),
                    PredictNextYearEps = table.Column<double>(type: "REAL", nullable: true),
                    PredictNextYearPe = table.Column<double>(type: "REAL", nullable: true),
                    PredictNextTwoYearEps = table.Column<double>(type: "REAL", nullable: true),
                    PredictNextTwoYearPe = table.Column<double>(type: "REAL", nullable: true),
                    EncodeUrl = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchReport", x => x.InfoCode);
                });

            migrationBuilder.CreateTable(
                name: "ShareChange",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    TotalShares = table.Column<double>(type: "REAL", nullable: true),
                    LimitedShares = table.Column<double>(type: "REAL", nullable: true),
                    UnlimitedShares = table.Column<double>(type: "REAL", nullable: true),
                    ListedAShares = table.Column<double>(type: "REAL", nullable: true),
                    ChangeReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareChange", x => new { x.Code, x.EndDate });
                });

            migrationBuilder.CreateTable(
                name: "UpcomingUnlock",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    LiftDate = table.Column<string>(type: "TEXT", nullable: false),
                    LiftType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LiftShares = table.Column<double>(type: "REAL", nullable: true),
                    TotalSharesRatio = table.Column<double>(type: "REAL", nullable: true),
                    UnlimitedASharesRatio = table.Column<double>(type: "REAL", nullable: true),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpcomingUnlock", x => new { x.Code, x.LiftDate, x.LiftType });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_Code_ColumnName",
                table: "Announcement",
                columns: new[] { "Code", "ColumnName" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcement_Code_NoticeDate",
                table: "Announcement",
                columns: new[] { "Code", "NoticeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessComposition_Code_ReportDate_MainOpType",
                table: "BusinessComposition",
                columns: new[] { "Code", "ReportDate", "MainOpType" });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchReport_Code_PublishDate",
                table: "ResearchReport",
                columns: new[] { "Code", "PublishDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcement");

            migrationBuilder.DropTable(
                name: "BusinessComposition");

            migrationBuilder.DropTable(
                name: "BusinessProfile");

            migrationBuilder.DropTable(
                name: "ResearchReport");

            migrationBuilder.DropTable(
                name: "ShareChange");

            migrationBuilder.DropTable(
                name: "UpcomingUnlock");
        }
    }
}
