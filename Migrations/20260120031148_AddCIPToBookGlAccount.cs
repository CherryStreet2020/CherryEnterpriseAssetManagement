using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddCIPToBookGlAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowManualDepreciation",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPostOnPeriodClose",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CalculateOnlyNoPosting",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CalculationFrequency",
                table: "Books",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Books",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Books",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPolicyId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Books",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlAccountAssetClearing",
                table: "Books",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlAccountCIP",
                table: "Books",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlAccountGainOnDisposal",
                table: "Books",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlAccountLossOnDisposal",
                table: "Books",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryBook",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalToPost",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrackBudgetVsActual",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CIP",
                table: "BookGlAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationPolicyId",
                table: "AssetCategories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookId1",
                table: "AssetBookSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DepreciationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Convention = table.Column<int>(type: "integer", nullable: false),
                    DefaultUsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    DefaultSalvagePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    DefaultSalvageAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    SalvageType = table.Column<int>(type: "integer", nullable: false),
                    SwitchToStraightLine = table.Column<bool>(type: "boolean", nullable: false),
                    SwitchToSLInYear = table.Column<int>(type: "integer", nullable: true),
                    AveragingMethod = table.Column<int>(type: "integer", nullable: false),
                    DecliningBalanceRate = table.Column<decimal>(type: "numeric", nullable: true),
                    ApplySection179 = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultSection179Percent = table.Column<decimal>(type: "numeric", nullable: true),
                    ApplyBonusDepreciation = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultBonusPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    MinimumBookValue = table.Column<decimal>(type: "numeric", nullable: false),
                    AllowNegativeDepreciation = table.Column<bool>(type: "boolean", nullable: false),
                    Rounding = table.Column<int>(type: "integer", nullable: false),
                    FirstYearProrate = table.Column<int>(type: "integer", nullable: false),
                    LastYearProrate = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    DepreciateInServiceMonth = table.Column<bool>(type: "boolean", nullable: false),
                    DepreciateInDisposalMonth = table.Column<bool>(type: "boolean", nullable: false),
                    CalculateToEndOfLife = table.Column<bool>(type: "boolean", nullable: false),
                    TrackUnitsOfProduction = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedTotalUnits = table.Column<int>(type: "integer", nullable: true),
                    CcaClassId = table.Column<int>(type: "integer", nullable: true),
                    MacrsRecoveryPeriodYears = table.Column<int>(type: "integer", nullable: true),
                    MacrsPropertyType = table.Column<int>(type: "integer", nullable: true),
                    MacrsUseADS = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicableBookType = table.Column<int>(type: "integer", nullable: false),
                    TaxJurisdiction = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    IsSystemPolicy = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationPolicies_CcaClasses_CcaClassId",
                        column: x => x.CcaClassId,
                        principalTable: "CcaClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DepreciationPolicies_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsefulLifeTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Jurisdiction = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsefulLifeTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyCategoryDefaults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepreciationPolicyId = table.Column<int>(type: "integer", nullable: false),
                    AssetCategoryId = table.Column<int>(type: "integer", nullable: false),
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyCategoryDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyCategoryDefaults_AssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyCategoryDefaults_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PolicyCategoryDefaults_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PolicyCategoryDefaults_DepreciationPolicies_DepreciationPol~",
                        column: x => x.DepreciationPolicyId,
                        principalTable: "DepreciationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsefulLifeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsefulLifeTableId = table.Column<int>(type: "integer", nullable: false),
                    AssetClassCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetClassName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GaapLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    TaxLifeMonths = table.Column<int>(type: "integer", nullable: true),
                    MacrsRecoveryYears = table.Column<int>(type: "integer", nullable: true),
                    CcaClassNumber = table.Column<int>(type: "integer", nullable: true),
                    CcaRate = table.Column<decimal>(type: "numeric", nullable: true),
                    RecommendedMethod = table.Column<int>(type: "integer", nullable: false),
                    RecommendedConvention = table.Column<int>(type: "integer", nullable: false),
                    IrsAssetClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CraAssetClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsefulLifeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsefulLifeEntries_UsefulLifeTables_UsefulLifeTableId",
                        column: x => x.UsefulLifeTableId,
                        principalTable: "UsefulLifeTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_DefaultPolicyId",
                table: "Books",
                column: "DefaultPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_DepreciationPolicyId",
                table: "AssetCategories",
                column: "DepreciationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetBookSettings_BookId1",
                table: "AssetBookSettings",
                column: "BookId1");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationPolicies_CcaClassId",
                table: "DepreciationPolicies",
                column: "CcaClassId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationPolicies_CompanyId",
                table: "DepreciationPolicies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyCategoryDefaults_AssetCategoryId",
                table: "PolicyCategoryDefaults",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyCategoryDefaults_BookId",
                table: "PolicyCategoryDefaults",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyCategoryDefaults_CompanyId",
                table: "PolicyCategoryDefaults",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyCategoryDefaults_DepreciationPolicyId",
                table: "PolicyCategoryDefaults",
                column: "DepreciationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_UsefulLifeEntries_UsefulLifeTableId",
                table: "UsefulLifeEntries",
                column: "UsefulLifeTableId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetBookSettings_Books_BookId1",
                table: "AssetBookSettings",
                column: "BookId1",
                principalTable: "Books",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetCategories_DepreciationPolicies_DepreciationPolicyId",
                table: "AssetCategories",
                column: "DepreciationPolicyId",
                principalTable: "DepreciationPolicies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_DepreciationPolicies_DefaultPolicyId",
                table: "Books",
                column: "DefaultPolicyId",
                principalTable: "DepreciationPolicies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetBookSettings_Books_BookId1",
                table: "AssetBookSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetCategories_DepreciationPolicies_DepreciationPolicyId",
                table: "AssetCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_DepreciationPolicies_DefaultPolicyId",
                table: "Books");

            migrationBuilder.DropTable(
                name: "PolicyCategoryDefaults");

            migrationBuilder.DropTable(
                name: "UsefulLifeEntries");

            migrationBuilder.DropTable(
                name: "DepreciationPolicies");

            migrationBuilder.DropTable(
                name: "UsefulLifeTables");

            migrationBuilder.DropIndex(
                name: "IX_Books_DefaultPolicyId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_AssetCategories_DepreciationPolicyId",
                table: "AssetCategories");

            migrationBuilder.DropIndex(
                name: "IX_AssetBookSettings_BookId1",
                table: "AssetBookSettings");

            migrationBuilder.DropColumn(
                name: "AllowManualDepreciation",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "AutoPostOnPeriodClose",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CalculateOnlyNoPosting",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CalculationFrequency",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "DefaultPolicyId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "GlAccountAssetClearing",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "GlAccountCIP",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "GlAccountGainOnDisposal",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "GlAccountLossOnDisposal",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsPrimaryBook",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "RequireApprovalToPost",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "TrackBudgetVsActual",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CIP",
                table: "BookGlAccounts");

            migrationBuilder.DropColumn(
                name: "DepreciationPolicyId",
                table: "AssetCategories");

            migrationBuilder.DropColumn(
                name: "BookId1",
                table: "AssetBookSettings");
        }
    }
}
