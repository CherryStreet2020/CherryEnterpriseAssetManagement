using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    /// <inheritdoc />
    public partial class AddRemainingLookupValueForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "WorkOrderOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "WorkOrderOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "VendorInvoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityLookupValueId",
                table: "PurchaseRequisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "PurchaseRequisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "POTypeLookupValueId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasonLookupValueId",
                table: "PartialDisposals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriorityLookupValueId",
                table: "MaintenanceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "MaintenanceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "MaintenanceEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "ItemTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostMethodLookupValueId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackingTypeLookupValueId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "ItemRevisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "GoodsReceipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountTypeLookupValueId",
                table: "GlAccounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "Departments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeLookupValueId",
                table: "CostCenters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "CipProjects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostTypeLookupValueId",
                table: "CipCosts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookTypeLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConventionLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrequencyLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MethodLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxJurisdictionLookupValueId",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryLookupValueId",
                table: "Attachments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasonLookupValueId",
                table: "AssetTransfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetPriorityLookupValueId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetTypeLookupValueId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConditionLookupValueId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationMethodLookupValueId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusLookupValueId",
                table: "Assets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateProvince = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentTermId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_node",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    node_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    site_id = table.Column<int>(type: "integer", nullable: true),
                    location_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_node", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_node_org_node_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "platform",
                        principalTable: "org_node",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceDue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusLookupValueId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PurchaseOrderRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SiteId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UOM = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GlAccountId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInvoiceLines_CustomerInvoices_CustomerInvoiceId",
                        column: x => x.CustomerInvoiceId,
                        principalTable: "CustomerInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_StatusLookupValueId",
                table: "VendorInvoices",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_StatusLookupValueId",
                table: "Sites",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_TypeLookupValueId",
                table: "Sites",
                column: "TypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_PriorityLookupValueId",
                table: "PurchaseRequisitions",
                column: "PriorityLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_StatusLookupValueId",
                table: "PurchaseRequisitions",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_POTypeLookupValueId",
                table: "PurchaseOrders",
                column: "POTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_StatusLookupValueId",
                table: "PurchaseOrders",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_PartialDisposals_ReasonLookupValueId",
                table: "PartialDisposals",
                column: "ReasonLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_PriorityLookupValueId",
                table: "MaintenanceEvents",
                column: "PriorityLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_StatusLookupValueId",
                table: "MaintenanceEvents",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_TypeLookupValueId",
                table: "MaintenanceEvents",
                column: "TypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CostMethodLookupValueId",
                table: "Items",
                column: "CostMethodLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_StatusLookupValueId",
                table: "Items",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TrackingTypeLookupValueId",
                table: "Items",
                column: "TrackingTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TypeLookupValueId",
                table: "Items",
                column: "TypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_StatusLookupValueId",
                table: "GoodsReceipts",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CipProjects_StatusLookupValueId",
                table: "CipProjects",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CipCosts_CostTypeLookupValueId",
                table: "CipCosts",
                column: "CostTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_BookTypeLookupValueId",
                table: "Books",
                column: "BookTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_ConventionLookupValueId",
                table: "Books",
                column: "ConventionLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_FrequencyLookupValueId",
                table: "Books",
                column: "FrequencyLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_MethodLookupValueId",
                table: "Books",
                column: "MethodLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_TaxJurisdictionLookupValueId",
                table: "Books",
                column: "TaxJurisdictionLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CategoryLookupValueId",
                table: "Attachments",
                column: "CategoryLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_ReasonLookupValueId",
                table: "AssetTransfers",
                column: "ReasonLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetPriorityLookupValueId",
                table: "Assets",
                column: "AssetPriorityLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTypeLookupValueId",
                table: "Assets",
                column: "AssetTypeLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ConditionLookupValueId",
                table: "Assets",
                column: "ConditionLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DepreciationMethodLookupValueId",
                table: "Assets",
                column: "DepreciationMethodLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_StatusLookupValueId",
                table: "Assets",
                column: "StatusLookupValueId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoiceLines_CustomerInvoiceId",
                table: "CustomerInvoiceLines",
                column: "CustomerInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_CompanyId_InvoiceNumber",
                table: "CustomerInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInvoices_CustomerId",
                table: "CustomerInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_CustomerCode",
                table: "Customers",
                columns: new[] { "CompanyId", "CustomerCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_node_company_id",
                schema: "platform",
                table: "org_node",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_node_location_id",
                schema: "platform",
                table: "org_node",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_node_parent_id",
                schema: "platform",
                table: "org_node",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_node_site_id",
                schema: "platform",
                table: "org_node",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_node_tenant_code_node_type",
                schema: "platform",
                table: "org_node",
                columns: new[] { "tenant_code", "node_type" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupValues_AssetPriorityLookupValueId",
                table: "Assets",
                column: "AssetPriorityLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupValues_AssetTypeLookupValueId",
                table: "Assets",
                column: "AssetTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupValues_ConditionLookupValueId",
                table: "Assets",
                column: "ConditionLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupValues_DepreciationMethodLookupValueId",
                table: "Assets",
                column: "DepreciationMethodLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_LookupValues_StatusLookupValueId",
                table: "Assets",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetTransfers_LookupValues_ReasonLookupValueId",
                table: "AssetTransfers",
                column: "ReasonLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_LookupValues_CategoryLookupValueId",
                table: "Attachments",
                column: "CategoryLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_BookTypeLookupValueId",
                table: "Books",
                column: "BookTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_ConventionLookupValueId",
                table: "Books",
                column: "ConventionLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_FrequencyLookupValueId",
                table: "Books",
                column: "FrequencyLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_MethodLookupValueId",
                table: "Books",
                column: "MethodLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Books_LookupValues_TaxJurisdictionLookupValueId",
                table: "Books",
                column: "TaxJurisdictionLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CipCosts_LookupValues_CostTypeLookupValueId",
                table: "CipCosts",
                column: "CostTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CipProjects_LookupValues_StatusLookupValueId",
                table: "CipProjects",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_LookupValues_StatusLookupValueId",
                table: "GoodsReceipts",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_LookupValues_CostMethodLookupValueId",
                table: "Items",
                column: "CostMethodLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_LookupValues_StatusLookupValueId",
                table: "Items",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_LookupValues_TrackingTypeLookupValueId",
                table: "Items",
                column: "TrackingTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_LookupValues_TypeLookupValueId",
                table: "Items",
                column: "TypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_PriorityLookupValueId",
                table: "MaintenanceEvents",
                column: "PriorityLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_StatusLookupValueId",
                table: "MaintenanceEvents",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_TypeLookupValueId",
                table: "MaintenanceEvents",
                column: "TypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PartialDisposals_LookupValues_ReasonLookupValueId",
                table: "PartialDisposals",
                column: "ReasonLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_LookupValues_POTypeLookupValueId",
                table: "PurchaseOrders",
                column: "POTypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_LookupValues_StatusLookupValueId",
                table: "PurchaseOrders",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_LookupValues_PriorityLookupValueId",
                table: "PurchaseRequisitions",
                column: "PriorityLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitions_LookupValues_StatusLookupValueId",
                table: "PurchaseRequisitions",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_LookupValues_StatusLookupValueId",
                table: "Sites",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_LookupValues_TypeLookupValueId",
                table: "Sites",
                column: "TypeLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoices_LookupValues_StatusLookupValueId",
                table: "VendorInvoices",
                column: "StatusLookupValueId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupValues_AssetPriorityLookupValueId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupValues_AssetTypeLookupValueId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupValues_ConditionLookupValueId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupValues_DepreciationMethodLookupValueId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_LookupValues_StatusLookupValueId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetTransfers_LookupValues_ReasonLookupValueId",
                table: "AssetTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_LookupValues_CategoryLookupValueId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_LookupValues_BookTypeLookupValueId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_LookupValues_ConventionLookupValueId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_LookupValues_FrequencyLookupValueId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_LookupValues_MethodLookupValueId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_Books_LookupValues_TaxJurisdictionLookupValueId",
                table: "Books");

            migrationBuilder.DropForeignKey(
                name: "FK_CipCosts_LookupValues_CostTypeLookupValueId",
                table: "CipCosts");

            migrationBuilder.DropForeignKey(
                name: "FK_CipProjects_LookupValues_StatusLookupValueId",
                table: "CipProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_LookupValues_StatusLookupValueId",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_LookupValues_CostMethodLookupValueId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_LookupValues_StatusLookupValueId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_LookupValues_TrackingTypeLookupValueId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_LookupValues_TypeLookupValueId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_PriorityLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_StatusLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceEvents_LookupValues_TypeLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_PartialDisposals_LookupValues_ReasonLookupValueId",
                table: "PartialDisposals");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_LookupValues_POTypeLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_LookupValues_StatusLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_LookupValues_PriorityLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitions_LookupValues_StatusLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Sites_LookupValues_StatusLookupValueId",
                table: "Sites");

            migrationBuilder.DropForeignKey(
                name: "FK_Sites_LookupValues_TypeLookupValueId",
                table: "Sites");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoices_LookupValues_StatusLookupValueId",
                table: "VendorInvoices");

            migrationBuilder.DropTable(
                name: "CustomerInvoiceLines");

            migrationBuilder.DropTable(
                name: "org_node",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CustomerInvoices");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoices_StatusLookupValueId",
                table: "VendorInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Sites_StatusLookupValueId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_TypeLookupValueId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_PriorityLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_StatusLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_POTypeLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_StatusLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PartialDisposals_ReasonLookupValueId",
                table: "PartialDisposals");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceEvents_PriorityLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceEvents_StatusLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceEvents_TypeLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropIndex(
                name: "IX_Items_CostMethodLookupValueId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_StatusLookupValueId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_TrackingTypeLookupValueId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_TypeLookupValueId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_StatusLookupValueId",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_CipProjects_StatusLookupValueId",
                table: "CipProjects");

            migrationBuilder.DropIndex(
                name: "IX_CipCosts_CostTypeLookupValueId",
                table: "CipCosts");

            migrationBuilder.DropIndex(
                name: "IX_Books_BookTypeLookupValueId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_ConventionLookupValueId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_FrequencyLookupValueId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_MethodLookupValueId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_TaxJurisdictionLookupValueId",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_CategoryLookupValueId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_AssetTransfers_ReasonLookupValueId",
                table: "AssetTransfers");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetPriorityLookupValueId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetTypeLookupValueId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ConditionLookupValueId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_DepreciationMethodLookupValueId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_StatusLookupValueId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "WorkOrderOperations");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "WorkOrderOperations");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "VendorInvoices");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriorityLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "POTypeLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReasonLookupValueId",
                table: "PartialDisposals");

            migrationBuilder.DropColumn(
                name: "PriorityLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "ItemTransactions");

            migrationBuilder.DropColumn(
                name: "CostMethodLookupValueId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TrackingTypeLookupValueId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "ItemRevisions");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "AccountTypeLookupValueId",
                table: "GlAccounts");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TypeLookupValueId",
                table: "CostCenters");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "CipProjects");

            migrationBuilder.DropColumn(
                name: "CostTypeLookupValueId",
                table: "CipCosts");

            migrationBuilder.DropColumn(
                name: "BookTypeLookupValueId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ConventionLookupValueId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "FrequencyLookupValueId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "MethodLookupValueId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "TaxJurisdictionLookupValueId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "CategoryLookupValueId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ReasonLookupValueId",
                table: "AssetTransfers");

            migrationBuilder.DropColumn(
                name: "AssetPriorityLookupValueId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AssetTypeLookupValueId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ConditionLookupValueId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DepreciationMethodLookupValueId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StatusLookupValueId",
                table: "Assets");
        }
    }
}
