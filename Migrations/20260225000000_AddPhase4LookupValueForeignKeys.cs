using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abs.FixedAssets.Migrations
{
    public partial class AddPhase4LookupValueForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === Assets ===
            migrationBuilder.AddColumn<int>(name: "AssetTypeLookupValueId", table: "Assets", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "AssetPriorityLookupValueId", table: "Assets", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ConditionLookupValueId", table: "Assets", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DepreciationMethodLookupValueId", table: "Assets", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "Assets", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_Assets_AssetTypeLookupValueId", table: "Assets", column: "AssetTypeLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Assets_AssetPriorityLookupValueId", table: "Assets", column: "AssetPriorityLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Assets_ConditionLookupValueId", table: "Assets", column: "ConditionLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Assets_DepreciationMethodLookupValueId", table: "Assets", column: "DepreciationMethodLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Assets_StatusLookupValueId", table: "Assets", column: "StatusLookupValueId");

            migrationBuilder.AddForeignKey(name: "FK_Assets_LookupValues_AssetTypeLookupValueId", table: "Assets", column: "AssetTypeLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Assets_LookupValues_AssetPriorityLookupValueId", table: "Assets", column: "AssetPriorityLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Assets_LookupValues_ConditionLookupValueId", table: "Assets", column: "ConditionLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Assets_LookupValues_DepreciationMethodLookupValueId", table: "Assets", column: "DepreciationMethodLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Assets_LookupValues_StatusLookupValueId", table: "Assets", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === Books ===
            migrationBuilder.AddColumn<int>(name: "MethodLookupValueId", table: "Books", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ConventionLookupValueId", table: "Books", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TaxJurisdictionLookupValueId", table: "Books", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FrequencyLookupValueId", table: "Books", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(name: "IX_Books_MethodLookupValueId", table: "Books", column: "MethodLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Books_ConventionLookupValueId", table: "Books", column: "ConventionLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Books_TaxJurisdictionLookupValueId", table: "Books", column: "TaxJurisdictionLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Books_FrequencyLookupValueId", table: "Books", column: "FrequencyLookupValueId");

            migrationBuilder.AddForeignKey(name: "FK_Books_LookupValues_MethodLookupValueId", table: "Books", column: "MethodLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Books_LookupValues_ConventionLookupValueId", table: "Books", column: "ConventionLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Books_LookupValues_TaxJurisdictionLookupValueId", table: "Books", column: "TaxJurisdictionLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Books_LookupValues_FrequencyLookupValueId", table: "Books", column: "FrequencyLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === AssetTransfers ===
            migrationBuilder.AddColumn<int>(name: "ReasonLookupValueId", table: "AssetTransfers", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_AssetTransfers_ReasonLookupValueId", table: "AssetTransfers", column: "ReasonLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_AssetTransfers_LookupValues_ReasonLookupValueId", table: "AssetTransfers", column: "ReasonLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === PartialDisposals ===
            migrationBuilder.AddColumn<int>(name: "ReasonLookupValueId", table: "PartialDisposals", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_PartialDisposals_ReasonLookupValueId", table: "PartialDisposals", column: "ReasonLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_PartialDisposals_LookupValues_ReasonLookupValueId", table: "PartialDisposals", column: "ReasonLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === Attachments ===
            migrationBuilder.AddColumn<int>(name: "CategoryLookupValueId", table: "Attachments", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Attachments_CategoryLookupValueId", table: "Attachments", column: "CategoryLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_Attachments_LookupValues_CategoryLookupValueId", table: "Attachments", column: "CategoryLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === CipProjects ===
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "CipProjects", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_CipProjects_StatusLookupValueId", table: "CipProjects", column: "StatusLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_CipProjects_LookupValues_StatusLookupValueId", table: "CipProjects", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === PurchaseOrders (Status) ===
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "PurchaseOrders", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_PurchaseOrders_StatusLookupValueId", table: "PurchaseOrders", column: "StatusLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_PurchaseOrders_LookupValues_StatusLookupValueId", table: "PurchaseOrders", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === PurchaseRequisitions ===
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "PurchaseRequisitions", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PriorityLookupValueId", table: "PurchaseRequisitions", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_PurchaseRequisitions_StatusLookupValueId", table: "PurchaseRequisitions", column: "StatusLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_PurchaseRequisitions_PriorityLookupValueId", table: "PurchaseRequisitions", column: "PriorityLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_PurchaseRequisitions_LookupValues_StatusLookupValueId", table: "PurchaseRequisitions", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_PurchaseRequisitions_LookupValues_PriorityLookupValueId", table: "PurchaseRequisitions", column: "PriorityLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === VendorInvoices ===
            migrationBuilder.Sql(@"DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='VendorInvoices' AND column_name='StatusLookupValueId') THEN
                    ALTER TABLE ""VendorInvoices"" ADD COLUMN ""StatusLookupValueId"" integer;
                END IF;
            END $$;");
            migrationBuilder.Sql(@"DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname='IX_VendorInvoices_StatusLookupValueId') THEN
                    CREATE INDEX ""IX_VendorInvoices_StatusLookupValueId"" ON ""VendorInvoices"" (""StatusLookupValueId"");
                END IF;
            END $$;");
            migrationBuilder.Sql(@"DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name='FK_VendorInvoices_LookupValues_StatusLookupValueId') THEN
                    ALTER TABLE ""VendorInvoices"" ADD CONSTRAINT ""FK_VendorInvoices_LookupValues_StatusLookupValueId"" FOREIGN KEY (""StatusLookupValueId"") REFERENCES ""LookupValues"" (""Id"") ON DELETE SET NULL;
                END IF;
            END $$;");

            // === GoodsReceipts ===
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "GoodsReceipts", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_GoodsReceipts_StatusLookupValueId", table: "GoodsReceipts", column: "StatusLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_GoodsReceipts_LookupValues_StatusLookupValueId", table: "GoodsReceipts", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === Sites ===
            migrationBuilder.AddColumn<int>(name: "TypeLookupValueId", table: "Sites", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "Sites", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Sites_TypeLookupValueId", table: "Sites", column: "TypeLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Sites_StatusLookupValueId", table: "Sites", column: "StatusLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_Sites_LookupValues_TypeLookupValueId", table: "Sites", column: "TypeLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Sites_LookupValues_StatusLookupValueId", table: "Sites", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === Items ===
            migrationBuilder.AddColumn<int>(name: "TypeLookupValueId", table: "Items", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "Items", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "CostMethodLookupValueId", table: "Items", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TrackingTypeLookupValueId", table: "Items", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Items_TypeLookupValueId", table: "Items", column: "TypeLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Items_StatusLookupValueId", table: "Items", column: "StatusLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Items_CostMethodLookupValueId", table: "Items", column: "CostMethodLookupValueId");
            migrationBuilder.CreateIndex(name: "IX_Items_TrackingTypeLookupValueId", table: "Items", column: "TrackingTypeLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_Items_LookupValues_TypeLookupValueId", table: "Items", column: "TypeLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Items_LookupValues_StatusLookupValueId", table: "Items", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Items_LookupValues_CostMethodLookupValueId", table: "Items", column: "CostMethodLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddForeignKey(name: "FK_Items_LookupValues_TrackingTypeLookupValueId", table: "Items", column: "TrackingTypeLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === MaintenanceEvents (Status) ===
            migrationBuilder.AddColumn<int>(name: "StatusLookupValueId", table: "MaintenanceEvents", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_MaintenanceEvents_StatusLookupValueId", table: "MaintenanceEvents", column: "StatusLookupValueId");
            migrationBuilder.AddForeignKey(name: "FK_MaintenanceEvents_LookupValues_StatusLookupValueId", table: "MaintenanceEvents", column: "StatusLookupValueId", principalTable: "LookupValues", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            // === BACKFILL existing rows ===
            migrationBuilder.Sql(@"
                UPDATE ""Assets"" a
                SET ""ConditionLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AssetCondition'
                  AND lv.""Code"" = CAST(a.""Condition"" AS TEXT);

                UPDATE ""Assets"" a
                SET ""DepreciationMethodLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'DepreciationMethod'
                  AND lv.""Code"" = CAST(a.""DepreciationMethod"" AS TEXT);

                UPDATE ""Assets"" a
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AssetStatus'
                  AND lv.""Code"" = CAST(a.""Status"" AS TEXT);

                UPDATE ""Books"" b
                SET ""MethodLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'DepreciationMethod'
                  AND lv.""Code"" = CAST(b.""Method"" AS TEXT);

                UPDATE ""Books"" b
                SET ""ConventionLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'DepreciationConvention'
                  AND lv.""Code"" = CAST(b.""Convention"" AS TEXT);

                UPDATE ""Books"" b
                SET ""TaxJurisdictionLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'TaxJurisdiction'
                  AND lv.""Code"" = CAST(b.""TaxJurisdiction"" AS TEXT);

                UPDATE ""Books"" b
                SET ""FrequencyLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'DepreciationFrequency'
                  AND lv.""Code"" = CAST(b.""CalculationFrequency"" AS TEXT);

                UPDATE ""PartialDisposals"" pd
                SET ""ReasonLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'DisposalReason'
                  AND lv.""Code"" = CAST(pd.""Reason"" AS TEXT);

                UPDATE ""Attachments"" att
                SET ""CategoryLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'AttachmentCategory'
                  AND lv.""Code"" = CAST(att.""Category"" AS TEXT);

                UPDATE ""CipProjects"" cp
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'CipProjectStatus'
                  AND lv.""Code"" = CAST(cp.""Status"" AS TEXT);

                UPDATE ""PurchaseOrders"" po
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'POStatus'
                  AND lv.""Code"" = CAST(po.""Status"" AS TEXT);

                UPDATE ""PurchaseRequisitions"" pr
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'RequisitionStatus'
                  AND lv.""Code"" = CAST(pr.""Status"" AS TEXT);

                UPDATE ""PurchaseRequisitions"" pr
                SET ""PriorityLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'RequisitionPriority'
                  AND lv.""Code"" = CAST(pr.""Priority"" AS TEXT);

                UPDATE ""VendorInvoices"" vi
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'InvoiceStatus'
                  AND lv.""Code"" = CAST(vi.""Status"" AS TEXT);

                UPDATE ""GoodsReceipts"" gr
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'ReceiptStatus'
                  AND lv.""Code"" = CAST(gr.""Status"" AS TEXT);

                UPDATE ""Sites"" s
                SET ""TypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'SiteType'
                  AND lv.""Code"" = CAST(s.""Type"" AS TEXT);

                UPDATE ""Sites"" s
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'SiteStatus'
                  AND lv.""Code"" = CAST(s.""Status"" AS TEXT);

                UPDATE ""Items"" i
                SET ""TypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'ItemType'
                  AND lv.""Code"" = CAST(i.""Type"" AS TEXT);

                UPDATE ""Items"" i
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'ItemStatus'
                  AND lv.""Code"" = CAST(i.""Status"" AS TEXT);

                UPDATE ""Items"" i
                SET ""CostMethodLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'CostMethod'
                  AND lv.""Code"" = CAST(i.""CostMethod"" AS TEXT);

                UPDATE ""Items"" i
                SET ""TrackingTypeLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'TrackingType'
                  AND lv.""Code"" = CAST(i.""TrackingType"" AS TEXT);

                UPDATE ""MaintenanceEvents"" me
                SET ""StatusLookupValueId"" = lv.""Id""
                FROM ""LookupValues"" lv
                JOIN ""LookupTypes"" lt ON lt.""Id"" = lv.""LookupTypeId""
                WHERE lt.""Key"" = 'MaintenanceStatus'
                  AND lv.""Code"" = CAST(me.""Status"" AS TEXT);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Assets
            migrationBuilder.DropForeignKey(name: "FK_Assets_LookupValues_AssetTypeLookupValueId", table: "Assets");
            migrationBuilder.DropForeignKey(name: "FK_Assets_LookupValues_AssetPriorityLookupValueId", table: "Assets");
            migrationBuilder.DropForeignKey(name: "FK_Assets_LookupValues_ConditionLookupValueId", table: "Assets");
            migrationBuilder.DropForeignKey(name: "FK_Assets_LookupValues_DepreciationMethodLookupValueId", table: "Assets");
            migrationBuilder.DropForeignKey(name: "FK_Assets_LookupValues_StatusLookupValueId", table: "Assets");
            migrationBuilder.DropIndex(name: "IX_Assets_AssetTypeLookupValueId", table: "Assets");
            migrationBuilder.DropIndex(name: "IX_Assets_AssetPriorityLookupValueId", table: "Assets");
            migrationBuilder.DropIndex(name: "IX_Assets_ConditionLookupValueId", table: "Assets");
            migrationBuilder.DropIndex(name: "IX_Assets_DepreciationMethodLookupValueId", table: "Assets");
            migrationBuilder.DropIndex(name: "IX_Assets_StatusLookupValueId", table: "Assets");
            migrationBuilder.DropColumn(name: "AssetTypeLookupValueId", table: "Assets");
            migrationBuilder.DropColumn(name: "AssetPriorityLookupValueId", table: "Assets");
            migrationBuilder.DropColumn(name: "ConditionLookupValueId", table: "Assets");
            migrationBuilder.DropColumn(name: "DepreciationMethodLookupValueId", table: "Assets");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "Assets");

            // Books
            migrationBuilder.DropForeignKey(name: "FK_Books_LookupValues_MethodLookupValueId", table: "Books");
            migrationBuilder.DropForeignKey(name: "FK_Books_LookupValues_ConventionLookupValueId", table: "Books");
            migrationBuilder.DropForeignKey(name: "FK_Books_LookupValues_TaxJurisdictionLookupValueId", table: "Books");
            migrationBuilder.DropForeignKey(name: "FK_Books_LookupValues_FrequencyLookupValueId", table: "Books");
            migrationBuilder.DropIndex(name: "IX_Books_MethodLookupValueId", table: "Books");
            migrationBuilder.DropIndex(name: "IX_Books_ConventionLookupValueId", table: "Books");
            migrationBuilder.DropIndex(name: "IX_Books_TaxJurisdictionLookupValueId", table: "Books");
            migrationBuilder.DropIndex(name: "IX_Books_FrequencyLookupValueId", table: "Books");
            migrationBuilder.DropColumn(name: "MethodLookupValueId", table: "Books");
            migrationBuilder.DropColumn(name: "ConventionLookupValueId", table: "Books");
            migrationBuilder.DropColumn(name: "TaxJurisdictionLookupValueId", table: "Books");
            migrationBuilder.DropColumn(name: "FrequencyLookupValueId", table: "Books");

            // AssetTransfers
            migrationBuilder.DropForeignKey(name: "FK_AssetTransfers_LookupValues_ReasonLookupValueId", table: "AssetTransfers");
            migrationBuilder.DropIndex(name: "IX_AssetTransfers_ReasonLookupValueId", table: "AssetTransfers");
            migrationBuilder.DropColumn(name: "ReasonLookupValueId", table: "AssetTransfers");

            // PartialDisposals
            migrationBuilder.DropForeignKey(name: "FK_PartialDisposals_LookupValues_ReasonLookupValueId", table: "PartialDisposals");
            migrationBuilder.DropIndex(name: "IX_PartialDisposals_ReasonLookupValueId", table: "PartialDisposals");
            migrationBuilder.DropColumn(name: "ReasonLookupValueId", table: "PartialDisposals");

            // Attachments
            migrationBuilder.DropForeignKey(name: "FK_Attachments_LookupValues_CategoryLookupValueId", table: "Attachments");
            migrationBuilder.DropIndex(name: "IX_Attachments_CategoryLookupValueId", table: "Attachments");
            migrationBuilder.DropColumn(name: "CategoryLookupValueId", table: "Attachments");

            // CipProjects
            migrationBuilder.DropForeignKey(name: "FK_CipProjects_LookupValues_StatusLookupValueId", table: "CipProjects");
            migrationBuilder.DropIndex(name: "IX_CipProjects_StatusLookupValueId", table: "CipProjects");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "CipProjects");

            // PurchaseOrders
            migrationBuilder.DropForeignKey(name: "FK_PurchaseOrders_LookupValues_StatusLookupValueId", table: "PurchaseOrders");
            migrationBuilder.DropIndex(name: "IX_PurchaseOrders_StatusLookupValueId", table: "PurchaseOrders");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "PurchaseOrders");

            // PurchaseRequisitions
            migrationBuilder.DropForeignKey(name: "FK_PurchaseRequisitions_LookupValues_StatusLookupValueId", table: "PurchaseRequisitions");
            migrationBuilder.DropForeignKey(name: "FK_PurchaseRequisitions_LookupValues_PriorityLookupValueId", table: "PurchaseRequisitions");
            migrationBuilder.DropIndex(name: "IX_PurchaseRequisitions_StatusLookupValueId", table: "PurchaseRequisitions");
            migrationBuilder.DropIndex(name: "IX_PurchaseRequisitions_PriorityLookupValueId", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "PriorityLookupValueId", table: "PurchaseRequisitions");

            // VendorInvoices
            migrationBuilder.DropForeignKey(name: "FK_VendorInvoices_LookupValues_StatusLookupValueId", table: "VendorInvoices");
            migrationBuilder.DropIndex(name: "IX_VendorInvoices_StatusLookupValueId", table: "VendorInvoices");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "VendorInvoices");

            // GoodsReceipts
            migrationBuilder.DropForeignKey(name: "FK_GoodsReceipts_LookupValues_StatusLookupValueId", table: "GoodsReceipts");
            migrationBuilder.DropIndex(name: "IX_GoodsReceipts_StatusLookupValueId", table: "GoodsReceipts");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "GoodsReceipts");

            // Sites
            migrationBuilder.DropForeignKey(name: "FK_Sites_LookupValues_TypeLookupValueId", table: "Sites");
            migrationBuilder.DropForeignKey(name: "FK_Sites_LookupValues_StatusLookupValueId", table: "Sites");
            migrationBuilder.DropIndex(name: "IX_Sites_TypeLookupValueId", table: "Sites");
            migrationBuilder.DropIndex(name: "IX_Sites_StatusLookupValueId", table: "Sites");
            migrationBuilder.DropColumn(name: "TypeLookupValueId", table: "Sites");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "Sites");

            // Items
            migrationBuilder.DropForeignKey(name: "FK_Items_LookupValues_TypeLookupValueId", table: "Items");
            migrationBuilder.DropForeignKey(name: "FK_Items_LookupValues_StatusLookupValueId", table: "Items");
            migrationBuilder.DropForeignKey(name: "FK_Items_LookupValues_CostMethodLookupValueId", table: "Items");
            migrationBuilder.DropForeignKey(name: "FK_Items_LookupValues_TrackingTypeLookupValueId", table: "Items");
            migrationBuilder.DropIndex(name: "IX_Items_TypeLookupValueId", table: "Items");
            migrationBuilder.DropIndex(name: "IX_Items_StatusLookupValueId", table: "Items");
            migrationBuilder.DropIndex(name: "IX_Items_CostMethodLookupValueId", table: "Items");
            migrationBuilder.DropIndex(name: "IX_Items_TrackingTypeLookupValueId", table: "Items");
            migrationBuilder.DropColumn(name: "TypeLookupValueId", table: "Items");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "Items");
            migrationBuilder.DropColumn(name: "CostMethodLookupValueId", table: "Items");
            migrationBuilder.DropColumn(name: "TrackingTypeLookupValueId", table: "Items");

            // MaintenanceEvents (Status)
            migrationBuilder.DropForeignKey(name: "FK_MaintenanceEvents_LookupValues_StatusLookupValueId", table: "MaintenanceEvents");
            migrationBuilder.DropIndex(name: "IX_MaintenanceEvents_StatusLookupValueId", table: "MaintenanceEvents");
            migrationBuilder.DropColumn(name: "StatusLookupValueId", table: "MaintenanceEvents");
        }
    }
}
