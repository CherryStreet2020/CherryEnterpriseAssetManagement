using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillToLocation",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ShipToLocation",
                table: "PurchaseOrders");

            migrationBuilder.AddColumn<int>(
                name: "DeliverToLocationId",
                table: "PurchaseRequisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliverToSiteId",
                table: "PurchaseRequisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillToAddress",
                table: "PurchaseOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillToSiteId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultShipToLocationId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToAddress",
                table: "PurchaseOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShipToSiteId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                table: "Locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsAssetInstallation",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                table: "Locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Criticality",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentAssetCount",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightFeet",
                table: "Locations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HierarchyLevel",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HierarchyPath",
                table: "Locations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOperational",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Locations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Locations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAssetCapacity",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rack",
                table: "Locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyRequirements",
                table: "Locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SafetyZone",
                table: "Locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Shelf",
                table: "Locations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SquareFootage",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiteId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsShortYear = table.Column<bool>(type: "boolean", nullable: false),
                    NumberOfPeriods = table.Column<int>(type: "integer", nullable: false),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    HasAdjustmentPeriod = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYears_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Address1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateProvince = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SiteManager = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ManagerEmail = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManagerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MainPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Fax = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SquareFootage = table.Column<int>(type: "integer", nullable: true),
                    NumberOfBuildings = table.Column<int>(type: "integer", nullable: true),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: true),
                    IsPrimarySite = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    OperatingHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumberOfShifts = table.Column<int>(type: "integer", nullable: false),
                    ShiftPattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Is24x7 = table.Column<bool>(type: "boolean", nullable: false),
                    AssetCapacity = table.Column<int>(type: "integer", nullable: true),
                    CurrentAssetCount = table.Column<int>(type: "integer", nullable: false),
                    ProductionCapacity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LoadingDocks = table.Column<int>(type: "integer", nullable: true),
                    ParkingSpaces = table.Column<int>(type: "integer", nullable: true),
                    EmergencyContact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HasFireSuppression = table.Column<bool>(type: "boolean", nullable: false),
                    HasSecuritySystem = table.Column<bool>(type: "boolean", nullable: false),
                    HasClimateControl = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FiscalYearId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsAdjustmentPeriod = table.Column<bool>(type: "boolean", nullable: false),
                    DaysInPeriod = table.Column<int>(type: "integer", nullable: false),
                    DepreciationCalculated = table.Column<bool>(type: "boolean", nullable: false),
                    DepreciationPosted = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalPeriods_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiscalPeriods_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    FiscalPeriodId = table.Column<int>(type: "integer", nullable: false),
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    RunDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssetsProcessed = table.Column<int>(type: "integer", nullable: false),
                    TotalDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReversedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_FiscalPeriods_FiscalPeriodId",
                        column: x => x.FiscalPeriodId,
                        principalTable: "FiscalPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationRunDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepreciationRunId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    BeginningBookValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EndingBookValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    YtdDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LtdDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MethodUsed = table.Column<int>(type: "integer", nullable: false),
                    RemainingLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationRunDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationRunDetails_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepreciationRunDetails_DepreciationRuns_DepreciationRunId",
                        column: x => x.DepreciationRunId,
                        principalTable: "DepreciationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_DeliverToLocationId",
                table: "PurchaseRequisitions",
                column: "DeliverToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_DeliverToSiteId",
                table: "PurchaseRequisitions",
                column: "DeliverToSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BillToSiteId",
                table: "PurchaseOrders",
                column: "BillToSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DefaultShipToLocationId",
                table: "PurchaseOrders",
                column: "DefaultShipToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ShipToSiteId",
                table: "PurchaseOrders",
                column: "ShipToSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SiteId",
                table: "Locations",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_SiteId",
                table: "Assets",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRunDetails_AssetId",
                table: "DepreciationRunDetails",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRunDetails_DepreciationRunId",
                table: "DepreciationRunDetails",
                column: "DepreciationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_BookId",
                table: "DepreciationRuns",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_CompanyId",
                table: "DepreciationRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_FiscalPeriodId",
                table: "DepreciationRuns",
                column: "FiscalPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_CompanyId",
                table: "FiscalPeriods",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_FiscalYearId",
                table: "FiscalPeriods",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_CompanyId",
                table: "FiscalYears",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CompanyId",
                table: "Sites",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Sites_SiteId",
                table: "Assets",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Sites_SiteId",
                table: "Locations",
                column: "SiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Locations_DefaultShipToLocationId",
                table: "PurchaseOrders",
                column: "DefaultShipToLocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Sites_BillToSiteId",
                table: "PurchaseOrders",
                column: "BillToSiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Sites_ShipToSiteId",
                table: "PurchaseOrders",
                column: "ShipToSiteId",
                principalTable: "Sites",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Locations_DeliverToLocationId",
                table: "PurchaseRequisitions",
                column: "DeliverToLocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_Sites_DeliverToSiteId",
                table: "PurchaseRequisitions",
                column: "DeliverToSiteId",
                principalTable: "Sites",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Sites_SiteId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Sites_SiteId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Locations_DefaultShipToLocationId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Sites_BillToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Sites_ShipToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Locations_DeliverToLocationId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_Sites_DeliverToSiteId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropTable(
                name: "DepreciationRunDetails");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "DepreciationRuns");

            migrationBuilder.DropTable(
                name: "FiscalPeriods");

            migrationBuilder.DropTable(
                name: "FiscalYears");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_DeliverToLocationId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_DeliverToSiteId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_BillToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_DefaultShipToLocationId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ShipToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Locations_SiteId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Assets_SiteId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DeliverToLocationId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DeliverToSiteId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "BillToAddress",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BillToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DefaultShipToLocationId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ShipToAddress",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ShipToSiteId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Aisle",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "AllowsAssetInstallation",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CurrentAssetCount",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "HeightFeet",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "HierarchyLevel",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "HierarchyPath",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsOperational",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "MaxAssetCapacity",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Rack",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SafetyRequirements",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SafetyZone",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Shelf",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SquareFootage",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "Assets");

            migrationBuilder.AddColumn<string>(
                name: "BillToLocation",
                table: "PurchaseOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToLocation",
                table: "PurchaseOrders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
