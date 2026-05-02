using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLocationPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonusDepreciationRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusDepreciationRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    OperationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssetsAffected = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NewDepartment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: true),
                    ProcessedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssetIds = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CcaClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    IsDecliningBalance = table.Column<bool>(type: "boolean", nullable: false),
                    HalfYearRuleApplies = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcceleratedInvestmentIncentive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcaClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyType = table.Column<int>(type: "integer", nullable: false),
                    CompanyStructure = table.Column<int>(type: "integer", nullable: false),
                    ParentCompanyId = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearStartDay = table.Column<int>(type: "integer", nullable: false),
                    IsShortYear = table.Column<bool>(type: "boolean", nullable: false),
                    ShortYearStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShortYearEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateProvince = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultDepMethod = table.Column<int>(type: "integer", nullable: false),
                    DefaultConvention = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LogoPath = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GstHstNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PstNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BusinessNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultLanguage = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApprovalThreshold = table.Column<decimal>(type: "numeric", nullable: true),
                    RequireApprovalForDisposals = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApprovalForTransfers = table.Column<bool>(type: "boolean", nullable: false),
                    FinancialMode = table.Column<int>(type: "integer", nullable: false),
                    IntegrationType = table.Column<int>(type: "integer", nullable: false),
                    EnableWorkOrders = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePurchasing = table.Column<bool>(type: "boolean", nullable: false),
                    EnableInventory = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAccountsPayable = table.Column<bool>(type: "boolean", nullable: false),
                    EnableVendors = table.Column<bool>(type: "boolean", nullable: false),
                    ERPConnectionString = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ERPCompanyCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Companies_ParentCompanyId",
                        column: x => x.ParentCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalAssets = table.Column<int>(type: "integer", nullable: false),
                    ScannedAssets = table.Column<int>(type: "integer", nullable: false),
                    MissingAssets = table.Column<int>(type: "integer", nullable: false),
                    FoundAssets = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Manufacturers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Manufacturers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeriodLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Section179Limits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    MaxDeduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PhaseoutThreshold = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SuvLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AutoDepreciationCap = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TruckDepreciationCap = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Section179Limits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CcaClassBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CcaClassId = table.Column<int>(type: "integer", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    OpeningUcc = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Additions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Dispositions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HalfYearAdjustment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BaseForCca = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CcaClaimed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosingUcc = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Recapture = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TerminalLoss = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsPosted = table.Column<bool>(type: "boolean", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DaysInFiscalPeriod = table.Column<int>(type: "integer", nullable: true),
                    IsShortFiscalPeriod = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcaClassBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CcaClassBalances_CcaClasses_CcaClassId",
                        column: x => x.CcaClassId,
                        principalTable: "CcaClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Convention = table.Column<int>(type: "integer", nullable: false),
                    UsefulLifeOverrideMonths = table.Column<int>(type: "integer", nullable: true),
                    GlAccountDepExp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GlAccountAccumDep = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BookType = table.Column<int>(type: "integer", nullable: false),
                    TaxJurisdiction = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Books_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateProvince = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ParentCostCenterId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostCenters_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostCenters_CostCenters_ParentCostCenterId",
                        column: x => x.ParentCostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SubCategory = table.Column<int>(type: "integer", nullable: false),
                    NormalBalance = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemAccount = table.Column<bool>(type: "boolean", nullable: false),
                    AllowManualEntry = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCostCenter = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAssetCategory = table.Column<bool>(type: "boolean", nullable: false),
                    ParentAccountId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GlAccounts_GlAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookGlAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    Asset = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AccumulatedDepreciation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepreciationExpense = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GainOnDisposal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LossOnDisposal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Clearing = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookGlAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookGlAccounts_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookId = table.Column<int>(type: "integer", nullable: true),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Batch = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Departments_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Floor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Bay = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Station = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ParentLocationId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Locations_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultMacrsClass = table.Column<int>(type: "integer", nullable: false),
                    DefaultCcaClassId = table.Column<int>(type: "integer", nullable: true),
                    DefaultUsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    DefaultSalvagePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AssetGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    AccumDepGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    DepExpGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetCategories_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetCategories_GlAccounts_AccumDepGlAccountId",
                        column: x => x.AccumDepGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetCategories_GlAccounts_AssetGlAccountId",
                        column: x => x.AssetGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssetCategories_GlAccounts_DepExpGlAccountId",
                        column: x => x.DepExpGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItemCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentCategoryId = table.Column<int>(type: "integer", nullable: true),
                    DefaultGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    ExpenseGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemCategories_GlAccounts_DefaultGlAccountId",
                        column: x => x.DefaultGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCategories_GlAccounts_ExpenseGlAccountId",
                        column: x => x.ExpenseGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCategories_ItemCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VendorType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Fax = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PaymentTerms = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultGlAccountId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    Is1099Vendor = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vendors_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vendors_GlAccounts_DefaultGlAccountId",
                        column: x => x.DefaultGlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JournalLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    Account = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectManagers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectManagers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectManagers_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectManagers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Technicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Technicians_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Technicians_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PMTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    CalendarInterval = table.Column<int>(type: "integer", nullable: false),
                    CalendarIntervalValue = table.Column<int>(type: "integer", nullable: false),
                    MeterType = table.Column<int>(type: "integer", nullable: true),
                    MeterInterval = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    EstimatedLaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedPartsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EstimatedTotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RequiresShutdown = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresLOTO = table.Column<bool>(type: "boolean", nullable: false),
                    SkillLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Craft = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Procedure = table.Column<string>(type: "text", nullable: true),
                    SafetyInstructions = table.Column<string>(type: "text", nullable: true),
                    ToolsRequired = table.Column<string>(type: "text", nullable: true),
                    ReferenceDocuments = table.Column<string>(type: "text", nullable: true),
                    AssetCategoryId = table.Column<int>(type: "integer", nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    ModelPattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsOEMRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    OEMReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRegulatoryRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RegulatoryReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PMTemplates_AssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PMTemplates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PMTemplates_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Kits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KitNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kits_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Kits_ItemCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FiscalPurchaseYear = table.Column<int>(type: "integer", nullable: true),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalvageValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BookValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FairMarketValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DepreciationMethod = table.Column<int>(type: "integer", nullable: false),
                    DepreciationRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "CAD"),
                    LastDepreciationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDepreciationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Bay = table.Column<string>(type: "text", nullable: true),
                    Department = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    AssetCategoryId = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisposalProceeds = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    GainLossOnDisposal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_AssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assets_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Assets_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExtendedDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequireRevisionControl = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    UOM = table.Column<int>(type: "integer", nullable: false),
                    StockUOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchaseUOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchaseConversion = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostMethod = table.Column<int>(type: "integer", nullable: false),
                    StandardCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AverageCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LastPurchaseCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ListPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TrackingType = table.Column<int>(type: "integer", nullable: false),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaxQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultLocation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Warehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Aisle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Rack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Shelf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Bin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PrimaryVendorId = table.Column<int>(type: "integer", nullable: true),
                    VendorPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManufacturerId = table.Column<int>(type: "integer", nullable: true),
                    IsStocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchasable = table.Column<bool>(type: "boolean", nullable: false),
                    IsCriticalSpare = table.Column<bool>(type: "boolean", nullable: false),
                    IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                    IsHazmat = table.Column<bool>(type: "boolean", nullable: false),
                    HazmatClass = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ShelfLifeDays = table.Column<int>(type: "integer", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Dimensions = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SpecUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BarcodeType = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AlternateBarcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ABCClass = table.Column<int>(type: "integer", nullable: false),
                    ReorderMethod = table.Column<int>(type: "integer", nullable: false),
                    AutoReorderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EOQ = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AnnualUsage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AverageDailyUsage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CarryingCostPercent = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    OrderingCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AlternatePartNumbers = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupersedesPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupersededByPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WarrantyMonths = table.Column<int>(type: "integer", nullable: true),
                    WarrantyTerms = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommodityCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UNSPSCCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultBuyerId = table.Column<int>(type: "integer", nullable: true),
                    DefaultBuyerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Length = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Width = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Height = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    DimensionUOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StorageRequirements = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MinStorageTemp = table.Column<int>(type: "integer", nullable: true),
                    MaxStorageTemp = table.Column<int>(type: "integer", nullable: true),
                    Certifications = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsFDARegulated = table.Column<bool>(type: "boolean", nullable: false),
                    IsOSHACompliance = table.Column<bool>(type: "boolean", nullable: false),
                    CountryOfOrigin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HTSCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_ItemCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_Vendors_PrimaryVendorId",
                        column: x => x.PrimaryVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchStatus = table.Column<int>(type: "integer", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentTerms = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceDue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InternalNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoices_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetBookSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    MethodOverride = table.Column<int>(type: "integer", nullable: true),
                    ConventionOverride = table.Column<int>(type: "integer", nullable: true),
                    UsefulLifeMonthsOverride = table.Column<int>(type: "integer", nullable: true),
                    SalvageValueOverride = table.Column<decimal>(type: "numeric", nullable: true),
                    InServiceDateOverride = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CostBasisOverride = table.Column<decimal>(type: "numeric", nullable: true),
                    Section179Deduction = table.Column<decimal>(type: "numeric", nullable: true),
                    BonusDepreciationPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    IsExcludedFromBook = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric", nullable: false),
                    BookValue = table.Column<decimal>(type: "numeric", nullable: false),
                    LastDepreciationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetBookSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetBookSettings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetBookSettings_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetInventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    BarcodeNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BarcodeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastScanDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScanLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastScannedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    ConditionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    LastReconciledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastInventoryListId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetInventories_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetInventories_InventoryLists_LastInventoryListId",
                        column: x => x.LastInventoryListId,
                        principalTable: "InventoryLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssetTaxSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    CcaClassId = table.Column<int>(type: "integer", nullable: false),
                    AvailableForUseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailableForUseOverride = table.Column<bool>(type: "boolean", nullable: false),
                    EligibleForAcceleratedIncentive = table.Column<bool>(type: "boolean", nullable: false),
                    CapitalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Proceeds = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisposalType = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTaxSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTaxSettings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTaxSettings_CcaClasses_CcaClassId",
                        column: x => x.CcaClassId,
                        principalTable: "CcaClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromBay = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FromDepartment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ToBay = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToDepartment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapitalImprovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    ImprovementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    Vendor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsefulLifeExtensionMonths = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Capitalized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitalImprovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapitalImprovements_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CcaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CcaClassId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableForUseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CapitalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Proceeds = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AdjustedCostBase = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NetAddition = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SubjectToHalfYearRule = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcceleratedIncentiveEligible = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcaTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CcaTransactions_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CcaTransactions_CcaClasses_CcaClassId",
                        column: x => x.CcaClassId,
                        principalTable: "CcaClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CipProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCosts = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommittedCosts = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProjectManagerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProjectManagerId = table.Column<int>(type: "integer", nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    GlAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true),
                    ConvertedAssetId = table.Column<int>(type: "integer", nullable: true),
                    PlacedInServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "CAD"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CipProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CipProjects_Assets_ConvertedAssetId",
                        column: x => x.ConvertedAssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CipProjects_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CipProjects_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CipProjects_GlAccounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CipProjects_ProjectManagers_ProjectManagerId",
                        column: x => x.ProjectManagerId,
                        principalTable: "ProjectManagers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InventoryScans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryListId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    ScannedBarcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScanDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScannedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryScans_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryScans_InventoryLists_InventoryListId",
                        column: x => x.InventoryListId,
                        principalTable: "InventoryLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LaborCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PartsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MaterialsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OutsideVendorCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TechnicianName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TechnicianId = table.Column<int>(type: "integer", nullable: true),
                    WorkOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DowntimeHours = table.Column<decimal>(type: "numeric", nullable: true),
                    LaborHours = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OvertimeHours = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedById = table.Column<int>(type: "integer", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RootCause = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecurrenceIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    NextScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomField1 = table.Column<string>(type: "text", nullable: true),
                    CustomField2 = table.Column<string>(type: "text", nullable: true),
                    CustomField3 = table.Column<string>(type: "text", nullable: true),
                    CustomField4 = table.Column<string>(type: "text", nullable: true),
                    CustomField5 = table.Column<string>(type: "text", nullable: true),
                    CustomField6 = table.Column<string>(type: "text", nullable: true),
                    CustomField7 = table.Column<string>(type: "text", nullable: true),
                    CustomField8 = table.Column<string>(type: "text", nullable: true),
                    CustomField9 = table.Column<string>(type: "text", nullable: true),
                    CustomField10 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceEvents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceEvents_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaintenanceEvents_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceEvents_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Recurrence = table.Column<int>(type: "integer", nullable: false),
                    IntervalValue = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastGeneratedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AssignedVendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    MeterType = table.Column<int>(type: "integer", nullable: false),
                    MeterName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reading = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ReadingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    IsRollover = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartialDisposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PercentageDisposed = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    OriginalCostDisposed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccumulatedDepreciationDisposed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BookValueDisposed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SaleProceeds = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GainLoss = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Buyer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                    ProcessedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartialDisposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartialDisposals_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PMTemplateAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PMTemplateId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    OverrideCalendarInterval = table.Column<int>(type: "integer", nullable: true),
                    OverrideCalendarValue = table.Column<int>(type: "integer", nullable: true),
                    OverrideMeterInterval = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    LastCompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMeterReading = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDueMeter = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMTemplateAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PMTemplateAssets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PMTemplateAssets_PMTemplates_PMTemplateId",
                        column: x => x.PMTemplateId,
                        principalTable: "PMTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsTaxSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    PropertyClass = table.Column<int>(type: "integer", nullable: false),
                    Convention = table.Column<int>(type: "integer", nullable: false),
                    UseADS = table.Column<bool>(type: "boolean", nullable: false),
                    Section179Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Section179Elected = table.Column<bool>(type: "boolean", nullable: false),
                    BonusDepreciationPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    BonusDepreciationAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    QualifiedImprovementProperty = table.Column<bool>(type: "boolean", nullable: false),
                    ListedProperty = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessUsePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    PlacedInServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    DepreciableBasis = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccumulatedTaxDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsTaxSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsTaxSettings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemCompanyStockings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    IsStocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsPurchasable = table.Column<bool>(type: "boolean", nullable: false),
                    IsCriticalSpare = table.Column<bool>(type: "boolean", nullable: false),
                    MinQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaxQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    PreferredVendorId = table.Column<int>(type: "integer", nullable: true),
                    ReorderMethod = table.Column<int>(type: "integer", nullable: false),
                    AutoReorderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ABCClass = table.Column<int>(type: "integer", nullable: false),
                    DefaultWarehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultAisle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultRack = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultShelf = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultBin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCompanyStockings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemCompanyStockings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCompanyStockings_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCompanyStockings_Vendors_PreferredVendorId",
                        column: x => x.PreferredVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItemImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    AltText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemImages_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemInventories2",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Warehouse = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Bin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityReserved = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityOnOrder = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCountDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastIssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemInventories2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemInventories2_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemInventories2_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemInventories2_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItemRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemRevisions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemVendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    VendorPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinOrderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    LastOrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductPageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CatalogPageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PriceBreakQty1 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PriceBreak1 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PriceBreakQty2 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PriceBreak2 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PriceBreakQty3 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PriceBreak3 = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContractPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ContractStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VendorStockAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    LastStockCheckDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemVendors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemVendors_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemVendors_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KitItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KitId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitItems_Kits_KitId",
                        column: x => x.KitId,
                        principalTable: "Kits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PMTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PMTemplateId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PMTemplateItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PMTemplateItems_PMTemplates_PMTemplateId",
                        column: x => x.PMTemplateId,
                        principalTable: "PMTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_VendorInvoices_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CipCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CipProjectId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CostType = table.Column<int>(type: "integer", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GlAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsCapitalizable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnteredBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CipCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CipCosts_CipProjects_CipProjectId",
                        column: x => x.CipProjectId,
                        principalTable: "CipProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PONumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    POType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromiseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShipToLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillToLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InternalNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WorkOrderId = table.Column<int>(type: "integer", nullable: true),
                    CipProjectId = table.Column<int>(type: "integer", nullable: true),
                    RequestedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedById = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_CipProjects_CipProjectId",
                        column: x => x.CipProjectId,
                        principalTable: "CipProjects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_MaintenanceEvents_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "MaintenanceEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaintenanceEventId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    QuantityPlanned = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityIssued = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IssuedFromLocationId = table.Column<int>(type: "integer", nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssuedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_Locations_IssuedFromLocationId",
                        column: x => x.IssuedFromLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_MaintenanceEvents_MaintenanceEventId",
                        column: x => x.MaintenanceEventId,
                        principalTable: "MaintenanceEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceEventId = table.Column<int>(type: "integer", nullable: true),
                    CipProjectId = table.Column<int>(type: "integer", nullable: true),
                    CipCostId = table.Column<int>(type: "integer", nullable: true),
                    AssetTransferId = table.Column<int>(type: "integer", nullable: true),
                    CapitalImprovementId = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_AssetTransfers_AssetTransferId",
                        column: x => x.AssetTransferId,
                        principalTable: "AssetTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attachments_CapitalImprovements_CapitalImprovementId",
                        column: x => x.CapitalImprovementId,
                        principalTable: "CapitalImprovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_CipCosts_CipCostId",
                        column: x => x.CipCostId,
                        principalTable: "CipCosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_CipProjects_CipProjectId",
                        column: x => x.CipProjectId,
                        principalTable: "CipProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attachments_MaintenanceEvents_MaintenanceEventId",
                        column: x => x.MaintenanceEventId,
                        principalTable: "MaintenanceEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShippingCarrier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PackingSlipNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceivingLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FromLocationId = table.Column<int>(type: "integer", nullable: true),
                    ToLocationId = table.Column<int>(type: "integer", nullable: true),
                    FromBin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ToBin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WorkOrderId = table.Column<int>(type: "integer", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransactedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_Locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_Locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseOrderId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    IsNonItemMaster = table.Column<bool>(type: "boolean", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExpenseCategoryId = table.Column<int>(type: "integer", nullable: true),
                    UOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    AssetId = table.Column<int>(type: "integer", nullable: true),
                    ShipToLocationId = table.Column<int>(type: "integer", nullable: true),
                    IsReceived = table.Column<bool>(type: "boolean", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_GlAccounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_ItemCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Locations_ShipToLocationId",
                        column: x => x.ShipToLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequisitionNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RequisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Requestor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestorId = table.Column<int>(type: "integer", nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Buyer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BuyerId = table.Column<int>(type: "integer", nullable: true),
                    SuggestedVendorId = table.Column<int>(type: "integer", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Justification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliverTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkOrderReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WorkOrderId = table.Column<int>(type: "integer", nullable: true),
                    PMScheduleReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConvertedToPOId = table.Column<int>(type: "integer", nullable: true),
                    ConvertedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExportedToERP = table.Column<bool>(type: "boolean", nullable: false),
                    ERPExportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ERPReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_PurchaseOrders_ConvertedToPOId",
                        column: x => x.ConvertedToPOId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitions_Vendors_SuggestedVendorId",
                        column: x => x.SuggestedVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoodsReceiptId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityAccepted = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StorageLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceivingLocationId = table.Column<int>(type: "integer", nullable: true),
                    LotNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsInvoiced = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_Locations_ReceivingLocationId",
                        column: x => x.ReceivingLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitionLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequisitionId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    IsNonItemMaster = table.Column<bool>(type: "boolean", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    PartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorPartNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExpenseCategoryId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SuggestedVendorId = table.Column<int>(type: "integer", nullable: true),
                    CurrentStock = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliverTo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_GlAccounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_ItemCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_PurchaseRequisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionLines_Vendors_SuggestedVendorId",
                        column: x => x.SuggestedVendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReorderAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    AlertType = table.Column<int>(type: "integer", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SuggestedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcknowledgedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequisitionId = table.Column<int>(type: "integer", nullable: true),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReorderAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReorderAlerts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReorderAlerts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReorderAlerts_PurchaseRequisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchaseOrderLineId = table.Column<int>(type: "integer", nullable: true),
                    GoodsReceiptLineId = table.Column<int>(type: "integer", nullable: true),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLines_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLines_GlAccounts_GlAccountId",
                        column: x => x.GlAccountId,
                        principalTable: "GlAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLines_GoodsReceiptLines_GoodsReceiptLineId",
                        column: x => x.GoodsReceiptLineId,
                        principalTable: "GoodsReceiptLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorInvoiceLines_VendorInvoices_VendorInvoiceId",
                        column: x => x.VendorInvoiceId,
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetBookSettings_AssetId_BookId",
                table: "AssetBookSettings",
                columns: new[] { "AssetId", "BookId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetBookSettings_BookId",
                table: "AssetBookSettings",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_AccumDepGlAccountId",
                table: "AssetCategories",
                column: "AccumDepGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_AssetGlAccountId",
                table: "AssetCategories",
                column: "AssetGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_Code",
                table: "AssetCategories",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_CompanyId",
                table: "AssetCategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_DepExpGlAccountId",
                table: "AssetCategories",
                column: "DepExpGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInventories_AssetId",
                table: "AssetInventories",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetInventories_BarcodeNumber",
                table: "AssetInventories",
                column: "BarcodeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInventories_LastInventoryListId",
                table: "AssetInventories",
                column: "LastInventoryListId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetCategoryId",
                table: "Assets",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CompanyId",
                table: "Assets",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CostCenterId",
                table: "Assets",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DepartmentId",
                table: "Assets",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_LocationId",
                table: "Assets",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ManufacturerId",
                table: "Assets",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_VendorId",
                table: "Assets",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTaxSettings_AssetId",
                table: "AssetTaxSettings",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTaxSettings_CcaClassId",
                table: "AssetTaxSettings",
                column: "CcaClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_AssetId",
                table: "AssetTransfers",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_TransferDate",
                table: "AssetTransfers",
                column: "TransferDate");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_AssetId",
                table: "Attachments",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_AssetTransferId",
                table: "Attachments",
                column: "AssetTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CapitalImprovementId",
                table: "Attachments",
                column: "CapitalImprovementId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CipCostId",
                table: "Attachments",
                column: "CipCostId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CipProjectId",
                table: "Attachments",
                column: "CipProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_MaintenanceEventId",
                table: "Attachments",
                column: "MaintenanceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BonusDepreciationRates_TaxYear",
                table: "BonusDepreciationRates",
                column: "TaxYear",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookGlAccounts_BookId",
                table: "BookGlAccounts",
                column: "BookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_CompanyId",
                table: "Books",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalImprovements_AssetId",
                table: "CapitalImprovements",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitalImprovements_ImprovementDate",
                table: "CapitalImprovements",
                column: "ImprovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_CcaClassBalances_CcaClassId_FiscalYear",
                table: "CcaClassBalances",
                columns: new[] { "CcaClassId", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CcaClasses_ClassNumber",
                table: "CcaClasses",
                column: "ClassNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CcaTransactions_AssetId",
                table: "CcaTransactions",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CcaTransactions_CcaClassId_FiscalYear",
                table: "CcaTransactions",
                columns: new[] { "CcaClassId", "FiscalYear" });

            migrationBuilder.CreateIndex(
                name: "IX_CipCosts_CipProjectId",
                table: "CipCosts",
                column: "CipProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CipCosts_TransactionDate",
                table: "CipCosts",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_ConvertedAssetId",
                table: "CipProjects",
                column: "ConvertedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_CostCenterId",
                table: "CipProjects",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_DepartmentId",
                table: "CipProjects",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_GlAccountId",
                table: "CipProjects",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_ProjectManagerId",
                table: "CipProjects",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_ProjectNumber",
                table: "CipProjects",
                column: "ProjectNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_Status",
                table: "CipProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CompanyCode",
                table: "Companies",
                column: "CompanyCode");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_ParentCompanyId",
                table: "Companies",
                column: "ParentCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_Code",
                table: "CostCenters",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_CompanyId",
                table: "CostCenters",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_ParentCostCenterId",
                table: "CostCenters",
                column: "ParentCostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId",
                table: "Departments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CostCenterId",
                table: "Departments",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_GlAccounts_AccountNumber",
                table: "GlAccounts",
                column: "AccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_GlAccounts_Category",
                table: "GlAccounts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GlAccounts_CompanyId",
                table: "GlAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GlAccounts_ParentAccountId",
                table: "GlAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_GoodsReceiptId",
                table: "GoodsReceiptLines",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_PurchaseOrderLineId",
                table: "GoodsReceiptLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_ReceivingLocationId",
                table: "GoodsReceiptLines",
                column: "ReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_CompanyId",
                table: "GoodsReceipts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseOrderId",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLists_CreatedDate",
                table: "InventoryLists",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLists_Status",
                table: "InventoryLists",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScans_AssetId",
                table: "InventoryScans",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScans_InventoryListId",
                table: "InventoryScans",
                column: "InventoryListId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryScans_ScanDate",
                table: "InventoryScans",
                column: "ScanDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_VendorInvoiceId",
                table: "InvoicePayments",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_Code",
                table: "ItemCategories",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_DefaultGlAccountId",
                table: "ItemCategories",
                column: "DefaultGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ExpenseGlAccountId",
                table: "ItemCategories",
                column: "ExpenseGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ParentCategoryId",
                table: "ItemCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCompanyStockings_CompanyId",
                table: "ItemCompanyStockings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCompanyStockings_ItemId",
                table: "ItemCompanyStockings",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCompanyStockings_PreferredVendorId",
                table: "ItemCompanyStockings",
                column: "PreferredVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemImages_ItemId",
                table: "ItemImages",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInventories2_CompanyId",
                table: "ItemInventories2",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInventories2_ItemId_LocationId_Bin",
                table: "ItemInventories2",
                columns: new[] { "ItemId", "LocationId", "Bin" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemInventories2_LocationId",
                table: "ItemInventories2",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemRevisions_ItemId_Revision",
                table: "ItemRevisions",
                columns: new[] { "ItemId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_CategoryId",
                table: "Items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyId",
                table: "Items",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ManufacturerId",
                table: "Items",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PartNumber",
                table: "Items",
                column: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PrimaryVendorId",
                table: "Items",
                column: "PrimaryVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_CompanyId",
                table: "ItemTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_FromLocationId",
                table: "ItemTransactions",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_ItemId",
                table: "ItemTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_PurchaseOrderId",
                table: "ItemTransactions",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_ToLocationId",
                table: "ItemTransactions",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_TransactionDate",
                table: "ItemTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_TransactionNumber",
                table: "ItemTransactions",
                column: "TransactionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVendors_ItemId_VendorId",
                table: "ItemVendors",
                columns: new[] { "ItemId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemVendors_VendorId",
                table: "ItemVendors",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Batch",
                table: "JournalEntries",
                column: "Batch");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_BookId",
                table: "JournalEntries",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Period",
                table: "JournalEntries",
                column: "Period");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_JournalEntryId_LineNo",
                table: "JournalLines",
                columns: new[] { "JournalEntryId", "LineNo" });

            migrationBuilder.CreateIndex(
                name: "IX_KitItems_ItemId",
                table: "KitItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_KitItems_KitId_ItemId",
                table: "KitItems",
                columns: new[] { "KitId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kits_CategoryId",
                table: "Kits",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Kits_CompanyId",
                table: "Kits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Kits_KitNumber",
                table: "Kits",
                column: "KitNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Code",
                table: "Locations",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CompanyId",
                table: "Locations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CostCenterId",
                table: "Locations",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_ApprovedById",
                table: "MaintenanceEvents",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_AssetId",
                table: "MaintenanceEvents",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_RequestedById",
                table: "MaintenanceEvents",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_ScheduledDate",
                table: "MaintenanceEvents",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_Status",
                table: "MaintenanceEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_TechnicianId",
                table: "MaintenanceEvents",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_NextDueDate",
                table: "MaintenanceSchedules",
                column: "NextDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_Active",
                table: "Manufacturers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_Name",
                table: "Manufacturers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_AssetId_MeterType_ReadingDate",
                table: "MeterReadings",
                columns: new[] { "AssetId", "MeterType", "ReadingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_CompanyId",
                table: "MeterReadings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartialDisposals_AssetId",
                table: "PartialDisposals",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_Period",
                table: "PeriodLocks",
                column: "Period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateAssets_AssetId",
                table: "PMTemplateAssets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateAssets_PMTemplateId_AssetId",
                table: "PMTemplateAssets",
                columns: new[] { "PMTemplateId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateItems_ItemId",
                table: "PMTemplateItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplateItems_PMTemplateId_ItemId",
                table: "PMTemplateItems",
                columns: new[] { "PMTemplateId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplates_AssetCategoryId",
                table: "PMTemplates",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplates_Code",
                table: "PMTemplates",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplates_CompanyId",
                table: "PMTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PMTemplates_ManufacturerId",
                table: "PMTemplates",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_Active",
                table: "ProjectManagers",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_CostCenterId",
                table: "ProjectManagers",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_DepartmentId",
                table: "ProjectManagers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_Name",
                table: "ProjectManagers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_AssetId",
                table: "PurchaseOrderLines",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_CostCenterId",
                table: "PurchaseOrderLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ExpenseCategoryId",
                table: "PurchaseOrderLines",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_GlAccountId",
                table: "PurchaseOrderLines",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ItemId",
                table: "PurchaseOrderLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId",
                table: "PurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_ShipToLocationId",
                table: "PurchaseOrderLines",
                column: "ShipToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedById",
                table: "PurchaseOrders",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CipProjectId",
                table: "PurchaseOrders",
                column: "CipProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyId",
                table: "PurchaseOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_RequestedById",
                table: "PurchaseOrders",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_VendorId",
                table: "PurchaseOrders",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_WorkOrderId",
                table: "PurchaseOrders",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_CostCenterId",
                table: "PurchaseRequisitionLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_ExpenseCategoryId",
                table: "PurchaseRequisitionLines",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_GlAccountId",
                table: "PurchaseRequisitionLines",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_ItemId",
                table: "PurchaseRequisitionLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_RequisitionId",
                table: "PurchaseRequisitionLines",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionLines_SuggestedVendorId",
                table: "PurchaseRequisitionLines",
                column: "SuggestedVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_CompanyId",
                table: "PurchaseRequisitions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_ConvertedToPOId",
                table: "PurchaseRequisitions",
                column: "ConvertedToPOId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_SuggestedVendorId",
                table: "PurchaseRequisitions",
                column: "SuggestedVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReorderAlerts_CompanyId",
                table: "ReorderAlerts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ReorderAlerts_ItemId",
                table: "ReorderAlerts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReorderAlerts_RequisitionId",
                table: "ReorderAlerts",
                column: "RequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Section179Limits_TaxYear",
                table: "Section179Limits",
                column: "TaxYear",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Active",
                table: "Technicians",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_CostCenterId",
                table: "Technicians",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_DepartmentId",
                table: "Technicians",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Name",
                table: "Technicians",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsTaxSettings_AssetId",
                table: "UsTaxSettings",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLines_CostCenterId",
                table: "VendorInvoiceLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLines_GlAccountId",
                table: "VendorInvoiceLines",
                column: "GlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLines_GoodsReceiptLineId",
                table: "VendorInvoiceLines",
                column: "GoodsReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLines_PurchaseOrderLineId",
                table: "VendorInvoiceLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLines_VendorInvoiceId",
                table: "VendorInvoiceLines",
                column: "VendorInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_ApprovedById",
                table: "VendorInvoices",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_CompanyId",
                table: "VendorInvoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_VendorId",
                table: "VendorInvoices",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyId",
                table: "Vendors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_DefaultGlAccountId",
                table: "Vendors",
                column: "DefaultGlAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_IssuedFromLocationId",
                table: "WorkOrderParts",
                column: "IssuedFromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_ItemId",
                table: "WorkOrderParts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_MaintenanceEventId_ItemId",
                table: "WorkOrderParts",
                columns: new[] { "MaintenanceEventId", "ItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AssetBookSettings");

            migrationBuilder.DropTable(
                name: "AssetInventories");

            migrationBuilder.DropTable(
                name: "AssetTaxSettings");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BonusDepreciationRates");

            migrationBuilder.DropTable(
                name: "BookGlAccounts");

            migrationBuilder.DropTable(
                name: "BulkOperations");

            migrationBuilder.DropTable(
                name: "CcaClassBalances");

            migrationBuilder.DropTable(
                name: "CcaTransactions");

            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "InventoryScans");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "ItemCompanyStockings");

            migrationBuilder.DropTable(
                name: "ItemImages");

            migrationBuilder.DropTable(
                name: "ItemInventories2");

            migrationBuilder.DropTable(
                name: "ItemRevisions");

            migrationBuilder.DropTable(
                name: "ItemTransactions");

            migrationBuilder.DropTable(
                name: "ItemVendors");

            migrationBuilder.DropTable(
                name: "JournalLines");

            migrationBuilder.DropTable(
                name: "KitItems");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules");

            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "PartialDisposals");

            migrationBuilder.DropTable(
                name: "PeriodLocks");

            migrationBuilder.DropTable(
                name: "PMTemplateAssets");

            migrationBuilder.DropTable(
                name: "PMTemplateItems");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitionLines");

            migrationBuilder.DropTable(
                name: "ReorderAlerts");

            migrationBuilder.DropTable(
                name: "Section179Limits");

            migrationBuilder.DropTable(
                name: "UsTaxSettings");

            migrationBuilder.DropTable(
                name: "VendorInvoiceLines");

            migrationBuilder.DropTable(
                name: "WorkOrderParts");

            migrationBuilder.DropTable(
                name: "AssetTransfers");

            migrationBuilder.DropTable(
                name: "CapitalImprovements");

            migrationBuilder.DropTable(
                name: "CipCosts");

            migrationBuilder.DropTable(
                name: "CcaClasses");

            migrationBuilder.DropTable(
                name: "InventoryLists");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "Kits");

            migrationBuilder.DropTable(
                name: "PMTemplates");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitions");

            migrationBuilder.DropTable(
                name: "GoodsReceiptLines");

            migrationBuilder.DropTable(
                name: "VendorInvoices");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "GoodsReceipts");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "ItemCategories");

            migrationBuilder.DropTable(
                name: "CipProjects");

            migrationBuilder.DropTable(
                name: "MaintenanceEvents");

            migrationBuilder.DropTable(
                name: "ProjectManagers");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Technicians");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AssetCategories");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Manufacturers");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "GlAccounts");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
