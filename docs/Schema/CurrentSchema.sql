-- CherryAI Enterprise Asset Management - Database Schema Snapshot
-- Generated: 2026-01-22 21:38:36 UTC
-- Environment: Development (Replit)
-- Method: pg_dump --schema-only
-- PostgreSQL 16.10

-- NOTE: This is schema-only. NO DATA is included.
-- This file is safe to commit to version control.

--
-- PostgreSQL database dump
--


-- Dumped from database version 16.10
-- Dumped by pg_dump version 16.10

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

ALTER TABLE IF EXISTS ONLY public."OutboxEvents" DROP CONSTRAINT IF EXISTS "OutboxEvents_TenantId_fkey";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "FK_WorkRequests_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "FK_WorkRequests_MaintenanceEvents_GeneratedWorkOrderId";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "FK_WorkRequests_Locations_LocationId";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "FK_WorkRequests_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "FK_WorkRequests_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."WorkOrderParts" DROP CONSTRAINT IF EXISTS "FK_WorkOrderParts_MaintenanceEvents_MaintenanceEventId";
ALTER TABLE IF EXISTS ONLY public."WorkOrderParts" DROP CONSTRAINT IF EXISTS "FK_WorkOrderParts_Locations_IssuedFromLocationId";
ALTER TABLE IF EXISTS ONLY public."WorkOrderParts" DROP CONSTRAINT IF EXISTS "FK_WorkOrderParts_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperations" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperations_Technicians";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperations" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperations_MaintenanceEvents";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperations" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperations_Crafts";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationTools" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperationTools_WorkOrderOperations";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationParts" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperationParts_WorkOrderOperations";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationParts" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperationParts_Items";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationLabors" DROP CONSTRAINT IF EXISTS "FK_WorkOrderOperationLabor_WorkOrderOperations";
ALTER TABLE IF EXISTS ONLY public."WebhookSubscriptions" DROP CONSTRAINT IF EXISTS "FK_WebhookSubscriptions_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."WebhookDeliveryLogs" DROP CONSTRAINT IF EXISTS "FK_WebhookDeliveryLogs_WebhookSubscriptions_WebhookSubscriptio~";
ALTER TABLE IF EXISTS ONLY public."WebhookDeliveryLogs" DROP CONSTRAINT IF EXISTS "FK_WebhookDeliveryLogs_OutboxEvents_OutboxEventId";
ALTER TABLE IF EXISTS ONLY public."Vendors" DROP CONSTRAINT IF EXISTS "FK_Vendors_GlAccounts_DefaultGlAccountId";
ALTER TABLE IF EXISTS ONLY public."Vendors" DROP CONSTRAINT IF EXISTS "FK_Vendors_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."VendorItemParts" DROP CONSTRAINT IF EXISTS "FK_VendorItemParts_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."VendorItemParts" DROP CONSTRAINT IF EXISTS "FK_VendorItemParts_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."VendorItemParts" DROP CONSTRAINT IF EXISTS "FK_VendorItemParts_ItemManufacturerParts_ItemManufacturerPartId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoices" DROP CONSTRAINT IF EXISTS "FK_VendorInvoices_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoices" DROP CONSTRAINT IF EXISTS "FK_VendorInvoices_Users_ApprovedById";
ALTER TABLE IF EXISTS ONLY public."VendorInvoices" DROP CONSTRAINT IF EXISTS "FK_VendorInvoices_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "FK_VendorInvoiceLines_VendorInvoices_VendorInvoiceId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "FK_VendorInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "FK_VendorInvoiceLines_GoodsReceiptLines_GoodsReceiptLineId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "FK_VendorInvoiceLines_GlAccounts_GlAccountId";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "FK_VendorInvoiceLines_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."UsefulLifeEntries" DROP CONSTRAINT IF EXISTS "FK_UsefulLifeEntries_UsefulLifeTables_UsefulLifeTableId";
ALTER TABLE IF EXISTS ONLY public."UsTaxSettings" DROP CONSTRAINT IF EXISTS "FK_UsTaxSettings_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Technicians" DROP CONSTRAINT IF EXISTS "FK_Technicians_Departments_DepartmentId";
ALTER TABLE IF EXISTS ONLY public."Technicians" DROP CONSTRAINT IF EXISTS "FK_Technicians_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."Skills" DROP CONSTRAINT IF EXISTS "FK_Skills_Crafts_CraftId";
ALTER TABLE IF EXISTS ONLY public."Sites" DROP CONSTRAINT IF EXISTS "FK_Sites_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ReorderAlerts" DROP CONSTRAINT IF EXISTS "FK_ReorderAlerts_PurchaseRequisitions_RequisitionId";
ALTER TABLE IF EXISTS ONLY public."ReorderAlerts" DROP CONSTRAINT IF EXISTS "FK_ReorderAlerts_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ReorderAlerts" DROP CONSTRAINT IF EXISTS "FK_ReorderAlerts_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitions_Vendors_SuggestedVendorId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitions_Sites_DeliverToSiteId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitions_PurchaseOrders_ConvertedToPOId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitions_Locations_DeliverToLocationId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitions_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_Vendors_SuggestedVendorId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_PurchaseRequisitions_RequisitionId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_ItemCategories_ExpenseCategoryId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_GlAccounts_GlAccountId";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseRequisitionLines_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Users_RequestedById";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Users_ApprovedById";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Sites_ShipToSiteId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Sites_BillToSiteId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_MaintenanceEvents_WorkOrderId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Locations_DefaultShipToLocationId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_CipProjects_CipProjectId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderReleases" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderReleases_PurchaseOrderLines_PurchaseOrderLineId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderReleases" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderReleases_Locations_ShipToLocationId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_Locations_ShipToLocationId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_ItemCategories_ExpenseCategoryId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_GlAccounts_GlAccountId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "FK_PurchaseOrderLines_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."ProjectManagers" DROP CONSTRAINT IF EXISTS "FK_ProjectManagers_Departments_DepartmentId";
ALTER TABLE IF EXISTS ONLY public."ProjectManagers" DROP CONSTRAINT IF EXISTS "FK_ProjectManagers_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."PolicyCategoryDefaults" DROP CONSTRAINT IF EXISTS "FK_PolicyCategoryDefaults_DepreciationPolicies_DepreciationPol~";
ALTER TABLE IF EXISTS ONLY public."PolicyCategoryDefaults" DROP CONSTRAINT IF EXISTS "FK_PolicyCategoryDefaults_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."PolicyCategoryDefaults" DROP CONSTRAINT IF EXISTS "FK_PolicyCategoryDefaults_Books_BookId";
ALTER TABLE IF EXISTS ONLY public."PolicyCategoryDefaults" DROP CONSTRAINT IF EXISTS "FK_PolicyCategoryDefaults_AssetCategories_AssetCategoryId";
ALTER TABLE IF EXISTS ONLY public."PartialDisposals" DROP CONSTRAINT IF EXISTS "FK_PartialDisposals_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."PMTemplates" DROP CONSTRAINT IF EXISTS "FK_PMTemplates_PMTemplateRevisions_CurrentReleasedRevisionId";
ALTER TABLE IF EXISTS ONLY public."PMTemplates" DROP CONSTRAINT IF EXISTS "FK_PMTemplates_Manufacturers_ManufacturerId";
ALTER TABLE IF EXISTS ONLY public."PMTemplates" DROP CONSTRAINT IF EXISTS "FK_PMTemplates_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."PMTemplates" DROP CONSTRAINT IF EXISTS "FK_PMTemplates_AssetCategories_AssetCategoryId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateRevisions" DROP CONSTRAINT IF EXISTS "FK_PMTemplateRevisions_PMTemplates_PMTemplateId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateRevisions" DROP CONSTRAINT IF EXISTS "FK_PMTemplateRevisions_PMTemplateRevisions_SupersedesRevisionId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateRevisionOperations" DROP CONSTRAINT IF EXISTS "FK_PMTemplateRevisionOperations_PMTemplateRevisions_PMTemplateR";
ALTER TABLE IF EXISTS ONLY public."PMTemplateItems" DROP CONSTRAINT IF EXISTS "FK_PMTemplateItems_PMTemplates_PMTemplateId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateItems" DROP CONSTRAINT IF EXISTS "FK_PMTemplateItems_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateAssets" DROP CONSTRAINT IF EXISTS "FK_PMTemplateAssets_PMTemplates_PMTemplateId";
ALTER TABLE IF EXISTS ONLY public."PMTemplateAssets" DROP CONSTRAINT IF EXISTS "FK_PMTemplateAssets_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."PMSchedules" DROP CONSTRAINT IF EXISTS "FK_PMSchedules_Sites";
ALTER TABLE IF EXISTS ONLY public."PMSchedules" DROP CONSTRAINT IF EXISTS "FK_PMSchedules_PMTemplates";
ALTER TABLE IF EXISTS ONLY public."PMSchedules" DROP CONSTRAINT IF EXISTS "FK_PMSchedules_Companies";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "FK_PMOccurrences_Sites";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "FK_PMOccurrences_PMTemplates";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "FK_PMOccurrences_PMSchedules";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "FK_PMOccurrences_MaintenanceEvents";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "FK_PMOccurrences_Companies";
ALTER TABLE IF EXISTS ONLY public."OutboxEvents" DROP CONSTRAINT IF EXISTS "FK_OutboxEvents_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."OutboxEvents" DROP CONSTRAINT IF EXISTS "FK_OutboxEvents_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."MeterReadings" DROP CONSTRAINT IF EXISTS "FK_MeterReadings_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."MeterReadings" DROP CONSTRAINT IF EXISTS "FK_MeterReadings_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Manufacturers" DROP CONSTRAINT IF EXISTS "FK_Manufacturers_Tenants_TenantId";
ALTER TABLE IF EXISTS ONLY public."MaintenanceSchedules" DROP CONSTRAINT IF EXISTS "FK_MaintenanceSchedules_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."MaintenanceEvents" DROP CONSTRAINT IF EXISTS "FK_MaintenanceEvents_Users_RequestedById";
ALTER TABLE IF EXISTS ONLY public."MaintenanceEvents" DROP CONSTRAINT IF EXISTS "FK_MaintenanceEvents_Users_ApprovedById";
ALTER TABLE IF EXISTS ONLY public."MaintenanceEvents" DROP CONSTRAINT IF EXISTS "FK_MaintenanceEvents_Technicians_TechnicianId";
ALTER TABLE IF EXISTS ONLY public."MaintenanceEvents" DROP CONSTRAINT IF EXISTS "FK_MaintenanceEvents_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Locations" DROP CONSTRAINT IF EXISTS "FK_Locations_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."Locations" DROP CONSTRAINT IF EXISTS "FK_Locations_Locations_ParentLocationId";
ALTER TABLE IF EXISTS ONLY public."Locations" DROP CONSTRAINT IF EXISTS "FK_Locations_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."Locations" DROP CONSTRAINT IF EXISTS "FK_Locations_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."LessonsLearned" DROP CONSTRAINT IF EXISTS "FK_LessonsLearned_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."LessonsLearned" DROP CONSTRAINT IF EXISTS "FK_LessonsLearned_MaintenanceEvents_SourceWorkOrderId";
ALTER TABLE IF EXISTS ONLY public."LessonsLearned" DROP CONSTRAINT IF EXISTS "FK_LessonsLearned_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."LessonsLearned" DROP CONSTRAINT IF EXISTS "FK_LessonsLearned_AssetCategories_AssetCategoryId";
ALTER TABLE IF EXISTS ONLY public."LaborRates" DROP CONSTRAINT IF EXISTS "FK_LaborRates_Skills_SkillId";
ALTER TABLE IF EXISTS ONLY public."LaborRates" DROP CONSTRAINT IF EXISTS "FK_LaborRates_Crafts_CraftId";
ALTER TABLE IF EXISTS ONLY public."Kits" DROP CONSTRAINT IF EXISTS "FK_Kits_ItemCategories_CategoryId";
ALTER TABLE IF EXISTS ONLY public."Kits" DROP CONSTRAINT IF EXISTS "FK_Kits_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."KitItems" DROP CONSTRAINT IF EXISTS "FK_KitItems_Kits_KitId";
ALTER TABLE IF EXISTS ONLY public."KitItems" DROP CONSTRAINT IF EXISTS "FK_KitItems_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."JournalLines" DROP CONSTRAINT IF EXISTS "FK_JournalLines_JournalEntries_JournalEntryId";
ALTER TABLE IF EXISTS ONLY public."JournalEntries" DROP CONSTRAINT IF EXISTS "FK_JournalEntries_Books_BookId";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "FK_Items_Vendors_PrimaryVendorId";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "FK_Items_Manufacturers_ManufacturerId";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "FK_Items_ItemRevisions_CurrentReleasedRevisionId";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "FK_Items_ItemCategories_CategoryId";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "FK_Items_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ItemVendors" DROP CONSTRAINT IF EXISTS "FK_ItemVendors_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."ItemVendors" DROP CONSTRAINT IF EXISTS "FK_ItemVendors_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "FK_ItemTransactions_PurchaseOrders_PurchaseOrderId";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "FK_ItemTransactions_Locations_ToLocationId";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "FK_ItemTransactions_Locations_FromLocationId";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "FK_ItemTransactions_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "FK_ItemTransactions_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ItemSupersessions" DROP CONSTRAINT IF EXISTS "FK_ItemSupersessions_Users_CreatedByUserId";
ALTER TABLE IF EXISTS ONLY public."ItemSupersessions" DROP CONSTRAINT IF EXISTS "FK_ItemSupersessions_Tenants_TenantId";
ALTER TABLE IF EXISTS ONLY public."ItemSupersessions" DROP CONSTRAINT IF EXISTS "FK_ItemSupersessions_Items_OldItemId";
ALTER TABLE IF EXISTS ONLY public."ItemSupersessions" DROP CONSTRAINT IF EXISTS "FK_ItemSupersessions_Items_NewItemId";
ALTER TABLE IF EXISTS ONLY public."ItemRevisions" DROP CONSTRAINT IF EXISTS "FK_ItemRevisions_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemRevisions" DROP CONSTRAINT IF EXISTS "FK_ItemRevisions_ItemRevisions_SupersedesItemRevisionId";
ALTER TABLE IF EXISTS ONLY public."ItemManufacturerParts" DROP CONSTRAINT IF EXISTS "FK_ItemManufacturerParts_Manufacturers_ManufacturerId";
ALTER TABLE IF EXISTS ONLY public."ItemManufacturerParts" DROP CONSTRAINT IF EXISTS "FK_ItemManufacturerParts_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemInventories2" DROP CONSTRAINT IF EXISTS "FK_ItemInventories2_Locations_LocationId";
ALTER TABLE IF EXISTS ONLY public."ItemInventories2" DROP CONSTRAINT IF EXISTS "FK_ItemInventories2_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemInventories2" DROP CONSTRAINT IF EXISTS "FK_ItemInventories2_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ItemImages" DROP CONSTRAINT IF EXISTS "FK_ItemImages_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemCompanyStockings" DROP CONSTRAINT IF EXISTS "FK_ItemCompanyStockings_Vendors_PreferredVendorId";
ALTER TABLE IF EXISTS ONLY public."ItemCompanyStockings" DROP CONSTRAINT IF EXISTS "FK_ItemCompanyStockings_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemCompanyStockings" DROP CONSTRAINT IF EXISTS "FK_ItemCompanyStockings_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ItemCategories" DROP CONSTRAINT IF EXISTS "FK_ItemCategories_ItemCategories_ParentCategoryId";
ALTER TABLE IF EXISTS ONLY public."ItemCategories" DROP CONSTRAINT IF EXISTS "FK_ItemCategories_GlAccounts_ExpenseGlAccountId";
ALTER TABLE IF EXISTS ONLY public."ItemCategories" DROP CONSTRAINT IF EXISTS "FK_ItemCategories_GlAccounts_DefaultGlAccountId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Users_CreatedByUserId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Tenants_TenantId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "FK_ItemApprovedVendors_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."ItemAlternates" DROP CONSTRAINT IF EXISTS "FK_ItemAlternates_Users_CreatedByUserId";
ALTER TABLE IF EXISTS ONLY public."ItemAlternates" DROP CONSTRAINT IF EXISTS "FK_ItemAlternates_Tenants_TenantId";
ALTER TABLE IF EXISTS ONLY public."ItemAlternates" DROP CONSTRAINT IF EXISTS "FK_ItemAlternates_Items_ItemId";
ALTER TABLE IF EXISTS ONLY public."ItemAlternates" DROP CONSTRAINT IF EXISTS "FK_ItemAlternates_Items_AlternateItemId";
ALTER TABLE IF EXISTS ONLY public."InvoicePayments" DROP CONSTRAINT IF EXISTS "FK_InvoicePayments_VendorInvoices_VendorInvoiceId";
ALTER TABLE IF EXISTS ONLY public."InventoryScans" DROP CONSTRAINT IF EXISTS "FK_InventoryScans_InventoryLists_InventoryListId";
ALTER TABLE IF EXISTS ONLY public."InventoryScans" DROP CONSTRAINT IF EXISTS "FK_InventoryScans_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."IntegrationMappings" DROP CONSTRAINT IF EXISTS "FK_IntegrationMappings_IntegrationEndpoints_IntegrationEndpoin~";
ALTER TABLE IF EXISTS ONLY public."IntegrationEndpoints" DROP CONSTRAINT IF EXISTS "FK_IntegrationEndpoints_Tenants_TenantId";
ALTER TABLE IF EXISTS ONLY public."InboundEvents" DROP CONSTRAINT IF EXISTS "FK_InboundEvents_IntegrationEndpoints_IntegrationEndpointId";
ALTER TABLE IF EXISTS ONLY public."InboundEvents" DROP CONSTRAINT IF EXISTS "FK_InboundEvents_Companies_TenantId";
ALTER TABLE IF EXISTS ONLY public."GoodsReceipts" DROP CONSTRAINT IF EXISTS "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId";
ALTER TABLE IF EXISTS ONLY public."GoodsReceipts" DROP CONSTRAINT IF EXISTS "FK_GoodsReceipts_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."GoodsReceiptLines" DROP CONSTRAINT IF EXISTS "FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId";
ALTER TABLE IF EXISTS ONLY public."GoodsReceiptLines" DROP CONSTRAINT IF EXISTS "FK_GoodsReceiptLines_Locations_ReceivingLocationId";
ALTER TABLE IF EXISTS ONLY public."GoodsReceiptLines" DROP CONSTRAINT IF EXISTS "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId";
ALTER TABLE IF EXISTS ONLY public."GlAccounts" DROP CONSTRAINT IF EXISTS "FK_GlAccounts_GlAccounts_ParentAccountId";
ALTER TABLE IF EXISTS ONLY public."GlAccounts" DROP CONSTRAINT IF EXISTS "FK_GlAccounts_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."FiscalYears" DROP CONSTRAINT IF EXISTS "FK_FiscalYears_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."FiscalPeriods" DROP CONSTRAINT IF EXISTS "FK_FiscalPeriods_FiscalYears_FiscalYearId";
ALTER TABLE IF EXISTS ONLY public."FiscalPeriods" DROP CONSTRAINT IF EXISTS "FK_FiscalPeriods_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."FailureCodes" DROP CONSTRAINT IF EXISTS "FK_FailureCodes_FailureCodes_ParentId";
ALTER TABLE IF EXISTS ONLY public."DepreciationRuns" DROP CONSTRAINT IF EXISTS "FK_DepreciationRuns_FiscalPeriods_FiscalPeriodId";
ALTER TABLE IF EXISTS ONLY public."DepreciationRuns" DROP CONSTRAINT IF EXISTS "FK_DepreciationRuns_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."DepreciationRuns" DROP CONSTRAINT IF EXISTS "FK_DepreciationRuns_Books_BookId";
ALTER TABLE IF EXISTS ONLY public."DepreciationRunDetails" DROP CONSTRAINT IF EXISTS "FK_DepreciationRunDetails_DepreciationRuns_DepreciationRunId";
ALTER TABLE IF EXISTS ONLY public."DepreciationRunDetails" DROP CONSTRAINT IF EXISTS "FK_DepreciationRunDetails_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."DepreciationPolicies" DROP CONSTRAINT IF EXISTS "FK_DepreciationPolicies_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."DepreciationPolicies" DROP CONSTRAINT IF EXISTS "FK_DepreciationPolicies_CcaClasses_CcaClassId";
ALTER TABLE IF EXISTS ONLY public."Departments" DROP CONSTRAINT IF EXISTS "FK_Departments_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."Departments" DROP CONSTRAINT IF EXISTS "FK_Departments_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."CostCenters" DROP CONSTRAINT IF EXISTS "FK_CostCenters_CostCenters_ParentCostCenterId";
ALTER TABLE IF EXISTS ONLY public."CostCenters" DROP CONSTRAINT IF EXISTS "FK_CostCenters_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."Companies" DROP CONSTRAINT IF EXISTS "FK_Companies_Companies_ParentCompanyId";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "FK_CipProjects_ProjectManagers_ProjectManagerId";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "FK_CipProjects_GlAccounts_GlAccountId";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "FK_CipProjects_Departments_DepartmentId";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "FK_CipProjects_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "FK_CipProjects_Assets_ConvertedAssetId";
ALTER TABLE IF EXISTS ONLY public."CipCosts" DROP CONSTRAINT IF EXISTS "FK_CipCosts_CipProjects_CipProjectId";
ALTER TABLE IF EXISTS ONLY public."CcaTransactions" DROP CONSTRAINT IF EXISTS "FK_CcaTransactions_CcaClasses_CcaClassId";
ALTER TABLE IF EXISTS ONLY public."CcaTransactions" DROP CONSTRAINT IF EXISTS "FK_CcaTransactions_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."CcaClassBalances" DROP CONSTRAINT IF EXISTS "FK_CcaClassBalances_CcaClasses_CcaClassId";
ALTER TABLE IF EXISTS ONLY public."CauseCodes" DROP CONSTRAINT IF EXISTS "FK_CauseCodes_CauseCodes_ParentId";
ALTER TABLE IF EXISTS ONLY public."CapitalImprovements" DROP CONSTRAINT IF EXISTS "FK_CapitalImprovements_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Books" DROP CONSTRAINT IF EXISTS "FK_Books_DepreciationPolicies_DefaultPolicyId";
ALTER TABLE IF EXISTS ONLY public."Books" DROP CONSTRAINT IF EXISTS "FK_Books_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."BookGlAccounts" DROP CONSTRAINT IF EXISTS "FK_BookGlAccounts_Books_BookId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_MaintenanceEvents_MaintenanceEventId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_CipProjects_CipProjectId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_CipCosts_CipCostId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_CapitalImprovements_CapitalImprovementId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "FK_Attachments_AssetTransfers_AssetTransferId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Vendors_VendorId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Sites_SiteId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Manufacturers_ManufacturerId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Locations_LocationId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Departments_DepartmentId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_CostCenters_CostCenterId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_Assets_ParentAssetId";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "FK_Assets_AssetCategories_AssetCategoryId";
ALTER TABLE IF EXISTS ONLY public."AssetTransfers" DROP CONSTRAINT IF EXISTS "FK_AssetTransfers_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."AssetTaxSettings" DROP CONSTRAINT IF EXISTS "FK_AssetTaxSettings_CcaClasses_CcaClassId";
ALTER TABLE IF EXISTS ONLY public."AssetTaxSettings" DROP CONSTRAINT IF EXISTS "FK_AssetTaxSettings_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."AssetInventories" DROP CONSTRAINT IF EXISTS "FK_AssetInventories_InventoryLists_LastInventoryListId";
ALTER TABLE IF EXISTS ONLY public."AssetInventories" DROP CONSTRAINT IF EXISTS "FK_AssetInventories_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "FK_AssetCategories_GlAccounts_DepExpGlAccountId";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "FK_AssetCategories_GlAccounts_AssetGlAccountId";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "FK_AssetCategories_GlAccounts_AccumDepGlAccountId";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "FK_AssetCategories_DepreciationPolicies_DepreciationPolicyId";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "FK_AssetCategories_Companies_CompanyId";
ALTER TABLE IF EXISTS ONLY public."AssetBookSettings" DROP CONSTRAINT IF EXISTS "FK_AssetBookSettings_Books_BookId1";
ALTER TABLE IF EXISTS ONLY public."AssetBookSettings" DROP CONSTRAINT IF EXISTS "FK_AssetBookSettings_Books_BookId";
ALTER TABLE IF EXISTS ONLY public."AssetBookSettings" DROP CONSTRAINT IF EXISTS "FK_AssetBookSettings_Assets_AssetId";
ALTER TABLE IF EXISTS ONLY public."Companies" DROP CONSTRAINT IF EXISTS "Companies_TenantId_fkey";
DROP INDEX IF EXISTS public."IX_WorkRequests_SiteId";
DROP INDEX IF EXISTS public."IX_WorkRequests_LocationId";
DROP INDEX IF EXISTS public."IX_WorkRequests_GeneratedWorkOrderId";
DROP INDEX IF EXISTS public."IX_WorkRequests_CompanyId";
DROP INDEX IF EXISTS public."IX_WorkRequests_AssetId";
DROP INDEX IF EXISTS public."IX_WorkOrderParts_MaintenanceEventId_ItemId";
DROP INDEX IF EXISTS public."IX_WorkOrderParts_ItemId";
DROP INDEX IF EXISTS public."IX_WorkOrderParts_IssuedFromLocationId";
DROP INDEX IF EXISTS public."IX_WebhookSubscriptions_IsActive";
DROP INDEX IF EXISTS public."IX_WebhookSubscriptions_CompanyId";
DROP INDEX IF EXISTS public."IX_WebhookDeliveryLogs_WebhookSubscriptionId";
DROP INDEX IF EXISTS public."IX_WebhookDeliveryLogs_OutboxEventId";
DROP INDEX IF EXISTS public."IX_Vendors_DefaultGlAccountId";
DROP INDEX IF EXISTS public."IX_Vendors_CompanyId";
DROP INDEX IF EXISTS public."IX_VendorItemParts_VendorPartNumber";
DROP INDEX IF EXISTS public."IX_VendorItemParts_VendorId_VendorPartNumber";
DROP INDEX IF EXISTS public."IX_VendorItemParts_ItemManufacturerPartId";
DROP INDEX IF EXISTS public."IX_VendorItemParts_ItemId";
DROP INDEX IF EXISTS public."IX_VendorItemParts_CatalogUrl";
DROP INDEX IF EXISTS public."IX_VendorInvoices_VendorId";
DROP INDEX IF EXISTS public."IX_VendorInvoices_CompanyId";
DROP INDEX IF EXISTS public."IX_VendorInvoices_ApprovedById";
DROP INDEX IF EXISTS public."IX_VendorInvoiceLines_VendorInvoiceId";
DROP INDEX IF EXISTS public."IX_VendorInvoiceLines_PurchaseOrderLineId";
DROP INDEX IF EXISTS public."IX_VendorInvoiceLines_GoodsReceiptLineId";
DROP INDEX IF EXISTS public."IX_VendorInvoiceLines_GlAccountId";
DROP INDEX IF EXISTS public."IX_VendorInvoiceLines_CostCenterId";
DROP INDEX IF EXISTS public."IX_Users_Username";
DROP INDEX IF EXISTS public."IX_Users_Email";
DROP INDEX IF EXISTS public."IX_UsefulLifeEntries_UsefulLifeTableId";
DROP INDEX IF EXISTS public."IX_UsTaxSettings_AssetId";
DROP INDEX IF EXISTS public."IX_Technicians_Name";
DROP INDEX IF EXISTS public."IX_Technicians_DepartmentId";
DROP INDEX IF EXISTS public."IX_Technicians_CostCenterId";
DROP INDEX IF EXISTS public."IX_Technicians_Active";
DROP INDEX IF EXISTS public."IX_Skills_CraftId";
DROP INDEX IF EXISTS public."IX_Sites_CompanyId";
DROP INDEX IF EXISTS public."IX_Section179Limits_TaxYear";
DROP INDEX IF EXISTS public."IX_ReorderAlerts_RequisitionId";
DROP INDEX IF EXISTS public."IX_ReorderAlerts_ItemId";
DROP INDEX IF EXISTS public."IX_ReorderAlerts_CompanyId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitions_SuggestedVendorId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitions_DeliverToSiteId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitions_DeliverToLocationId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitions_ConvertedToPOId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitions_CompanyId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_SuggestedVendorId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_RequisitionId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_ItemId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_GlAccountId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_ExpenseCategoryId";
DROP INDEX IF EXISTS public."IX_PurchaseRequisitionLines_CostCenterId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_WorkOrderId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_VendorId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_ShipToSiteId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_RequestedById";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_DefaultShipToLocationId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_CompanyId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_CipProjectId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_BillToSiteId";
DROP INDEX IF EXISTS public."IX_PurchaseOrders_ApprovedById";
DROP INDEX IF EXISTS public."IX_PurchaseOrderReleases_ShipToLocationId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderReleases_PurchaseOrderLineId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_ShipToLocationId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_PurchaseOrderId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_ItemId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_GlAccountId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_ExpenseCategoryId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_CostCenterId";
DROP INDEX IF EXISTS public."IX_PurchaseOrderLines_AssetId";
DROP INDEX IF EXISTS public."IX_ProjectManagers_Name";
DROP INDEX IF EXISTS public."IX_ProjectManagers_DepartmentId";
DROP INDEX IF EXISTS public."IX_ProjectManagers_CostCenterId";
DROP INDEX IF EXISTS public."IX_ProjectManagers_Active";
DROP INDEX IF EXISTS public."IX_PolicyCategoryDefaults_DepreciationPolicyId";
DROP INDEX IF EXISTS public."IX_PolicyCategoryDefaults_CompanyId";
DROP INDEX IF EXISTS public."IX_PolicyCategoryDefaults_BookId";
DROP INDEX IF EXISTS public."IX_PolicyCategoryDefaults_AssetCategoryId";
DROP INDEX IF EXISTS public."IX_PeriodLocks_Period";
DROP INDEX IF EXISTS public."IX_PartialDisposals_AssetId";
DROP INDEX IF EXISTS public."IX_PMTemplates_ManufacturerId";
DROP INDEX IF EXISTS public."IX_PMTemplates_CurrentReleasedRevisionId";
DROP INDEX IF EXISTS public."IX_PMTemplates_CompanyId";
DROP INDEX IF EXISTS public."IX_PMTemplates_Code";
DROP INDEX IF EXISTS public."IX_PMTemplates_AssetCategoryId";
DROP INDEX IF EXISTS public."IX_PMTemplateRevisions_SupersedesRevisionId";
DROP INDEX IF EXISTS public."IX_PMTemplateRevisions_PMTemplateId_RevisionCode";
DROP INDEX IF EXISTS public."IX_PMTemplateRevisionOperations_PMTemplateRevisionId_Sequence";
DROP INDEX IF EXISTS public."IX_PMTemplateItems_PMTemplateId_ItemId";
DROP INDEX IF EXISTS public."IX_PMTemplateItems_ItemId";
DROP INDEX IF EXISTS public."IX_PMTemplateAssets_PMTemplateId_AssetId";
DROP INDEX IF EXISTS public."IX_PMTemplateAssets_AssetId";
DROP INDEX IF EXISTS public."IX_PMSchedules_Active";
DROP INDEX IF EXISTS public."IX_PMOccurrences_Unique";
DROP INDEX IF EXISTS public."IX_OutboxEvents_Status_NextAttemptAt";
DROP INDEX IF EXISTS public."IX_OutboxEvents_Status";
DROP INDEX IF EXISTS public."IX_OutboxEvents_SiteId";
DROP INDEX IF EXISTS public."IX_OutboxEvents_CompanyId";
DROP INDEX IF EXISTS public."IX_MeterReadings_CompanyId";
DROP INDEX IF EXISTS public."IX_MeterReadings_AssetId_MeterType_ReadingDate";
DROP INDEX IF EXISTS public."IX_Manufacturers_TenantId_Code";
DROP INDEX IF EXISTS public."IX_Manufacturers_Name";
DROP INDEX IF EXISTS public."IX_Manufacturers_Active";
DROP INDEX IF EXISTS public."IX_MaintenanceSchedules_NextDueDate";
DROP INDEX IF EXISTS public."IX_MaintenanceSchedules_AssetId";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_TechnicianId";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_Status";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_ScheduledDate";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_RequestedById";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_AssetId";
DROP INDEX IF EXISTS public."IX_MaintenanceEvents_ApprovedById";
DROP INDEX IF EXISTS public."IX_Locations_SiteId";
DROP INDEX IF EXISTS public."IX_Locations_ParentLocationId";
DROP INDEX IF EXISTS public."IX_Locations_CostCenterId";
DROP INDEX IF EXISTS public."IX_Locations_CompanyId";
DROP INDEX IF EXISTS public."IX_Locations_Code";
DROP INDEX IF EXISTS public."IX_LessonsLearned_SourceWorkOrderId";
DROP INDEX IF EXISTS public."IX_LessonsLearned_SiteId";
DROP INDEX IF EXISTS public."IX_LessonsLearned_FailureCode";
DROP INDEX IF EXISTS public."IX_LessonsLearned_CompanyId";
DROP INDEX IF EXISTS public."IX_LessonsLearned_AssetCategoryId";
DROP INDEX IF EXISTS public."IX_LaborRates_SkillId";
DROP INDEX IF EXISTS public."IX_LaborRates_CraftId";
DROP INDEX IF EXISTS public."IX_Kits_KitNumber";
DROP INDEX IF EXISTS public."IX_Kits_CompanyId";
DROP INDEX IF EXISTS public."IX_Kits_CategoryId";
DROP INDEX IF EXISTS public."IX_KitItems_KitId_ItemId";
DROP INDEX IF EXISTS public."IX_KitItems_ItemId";
DROP INDEX IF EXISTS public."IX_JournalLines_JournalEntryId_LineNo";
DROP INDEX IF EXISTS public."IX_JournalEntries_Period";
DROP INDEX IF EXISTS public."IX_JournalEntries_BookId";
DROP INDEX IF EXISTS public."IX_JournalEntries_Batch";
DROP INDEX IF EXISTS public."IX_Items_PrimaryVendorId";
DROP INDEX IF EXISTS public."IX_Items_PartNumber";
DROP INDEX IF EXISTS public."IX_Items_ManufacturerId";
DROP INDEX IF EXISTS public."IX_Items_ImagePath";
DROP INDEX IF EXISTS public."IX_Items_CurrentReleasedRevisionId";
DROP INDEX IF EXISTS public."IX_Items_CompanyId";
DROP INDEX IF EXISTS public."IX_Items_CategoryId";
DROP INDEX IF EXISTS public."IX_ItemVendors_VendorId";
DROP INDEX IF EXISTS public."IX_ItemVendors_ItemId_VendorId";
DROP INDEX IF EXISTS public."IX_ItemTransactions_TransactionNumber";
DROP INDEX IF EXISTS public."IX_ItemTransactions_TransactionDate";
DROP INDEX IF EXISTS public."IX_ItemTransactions_ToLocationId";
DROP INDEX IF EXISTS public."IX_ItemTransactions_PurchaseOrderId";
DROP INDEX IF EXISTS public."IX_ItemTransactions_ItemId";
DROP INDEX IF EXISTS public."IX_ItemTransactions_FromLocationId";
DROP INDEX IF EXISTS public."IX_ItemTransactions_CompanyId";
DROP INDEX IF EXISTS public."IX_ItemSupersessions_TenantId_OldItemId";
DROP INDEX IF EXISTS public."IX_ItemSupersessions_OldItemId";
DROP INDEX IF EXISTS public."IX_ItemSupersessions_NewItemId";
DROP INDEX IF EXISTS public."IX_ItemSupersessions_CreatedByUserId";
DROP INDEX IF EXISTS public."IX_ItemRevisions_SupersedesItemRevisionId";
DROP INDEX IF EXISTS public."IX_ItemRevisions_ItemId_RevisionCode";
DROP INDEX IF EXISTS public."IX_ItemManufacturerParts_MfrPartNumber";
DROP INDEX IF EXISTS public."IX_ItemManufacturerParts_ManufacturerId";
DROP INDEX IF EXISTS public."IX_ItemManufacturerParts_ItemId_ManufacturerId_MfrPartNumber";
DROP INDEX IF EXISTS public."IX_ItemInventories2_LocationId";
DROP INDEX IF EXISTS public."IX_ItemInventories2_ItemId_LocationId_Bin";
DROP INDEX IF EXISTS public."IX_ItemInventories2_CompanyId";
DROP INDEX IF EXISTS public."IX_ItemImages_ItemId";
DROP INDEX IF EXISTS public."IX_ItemCompanyStockings_PreferredVendorId";
DROP INDEX IF EXISTS public."IX_ItemCompanyStockings_ItemId";
DROP INDEX IF EXISTS public."IX_ItemCompanyStockings_CompanyId";
DROP INDEX IF EXISTS public."IX_ItemCategories_ParentCategoryId";
DROP INDEX IF EXISTS public."IX_ItemCategories_ExpenseGlAccountId";
DROP INDEX IF EXISTS public."IX_ItemCategories_DefaultGlAccountId";
DROP INDEX IF EXISTS public."IX_ItemCategories_Code";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_VendorId";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_TenantId_ItemId_VendorId";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_SiteId";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_ItemId";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_CreatedByUserId";
DROP INDEX IF EXISTS public."IX_ItemApprovedVendors_CompanyId";
DROP INDEX IF EXISTS public."IX_ItemAlternates_TenantId_ItemId_AlternateItemId";
DROP INDEX IF EXISTS public."IX_ItemAlternates_ItemId";
DROP INDEX IF EXISTS public."IX_ItemAlternates_CreatedByUserId";
DROP INDEX IF EXISTS public."IX_ItemAlternates_AlternateItemId";
DROP INDEX IF EXISTS public."IX_InvoicePayments_VendorInvoiceId";
DROP INDEX IF EXISTS public."IX_InventoryScans_ScanDate";
DROP INDEX IF EXISTS public."IX_InventoryScans_InventoryListId";
DROP INDEX IF EXISTS public."IX_InventoryScans_AssetId";
DROP INDEX IF EXISTS public."IX_InventoryLists_Status";
DROP INDEX IF EXISTS public."IX_InventoryLists_CreatedDate";
DROP INDEX IF EXISTS public."IX_IntegrationMappings_IntegrationEndpointId";
DROP INDEX IF EXISTS public."IX_IntegrationEndpoints_TenantId";
DROP INDEX IF EXISTS public."IX_InboundEvents_TenantId";
DROP INDEX IF EXISTS public."IX_InboundEvents_IntegrationEndpointId";
DROP INDEX IF EXISTS public."IX_GoodsReceipts_PurchaseOrderId";
DROP INDEX IF EXISTS public."IX_GoodsReceipts_CompanyId";
DROP INDEX IF EXISTS public."IX_GoodsReceiptLines_ReceivingLocationId";
DROP INDEX IF EXISTS public."IX_GoodsReceiptLines_PurchaseOrderLineId";
DROP INDEX IF EXISTS public."IX_GoodsReceiptLines_GoodsReceiptId";
DROP INDEX IF EXISTS public."IX_GlAccounts_ParentAccountId";
DROP INDEX IF EXISTS public."IX_GlAccounts_CompanyId";
DROP INDEX IF EXISTS public."IX_GlAccounts_Category";
DROP INDEX IF EXISTS public."IX_GlAccounts_AccountNumber";
DROP INDEX IF EXISTS public."IX_FiscalYears_CompanyId";
DROP INDEX IF EXISTS public."IX_FiscalPeriods_FiscalYearId";
DROP INDEX IF EXISTS public."IX_FiscalPeriods_CompanyId";
DROP INDEX IF EXISTS public."IX_FailureCodes_ParentId";
DROP INDEX IF EXISTS public."IX_DepreciationRuns_FiscalPeriodId";
DROP INDEX IF EXISTS public."IX_DepreciationRuns_CompanyId";
DROP INDEX IF EXISTS public."IX_DepreciationRuns_BookId";
DROP INDEX IF EXISTS public."IX_DepreciationRunDetails_DepreciationRunId";
DROP INDEX IF EXISTS public."IX_DepreciationRunDetails_AssetId";
DROP INDEX IF EXISTS public."IX_DepreciationPolicies_CompanyId";
DROP INDEX IF EXISTS public."IX_DepreciationPolicies_CcaClassId";
DROP INDEX IF EXISTS public."IX_Departments_CostCenterId";
DROP INDEX IF EXISTS public."IX_Departments_CompanyId";
DROP INDEX IF EXISTS public."IX_Departments_Code";
DROP INDEX IF EXISTS public."IX_CostCenters_ParentCostCenterId";
DROP INDEX IF EXISTS public."IX_CostCenters_CompanyId";
DROP INDEX IF EXISTS public."IX_CostCenters_Code";
DROP INDEX IF EXISTS public."IX_Companies_ParentCompanyId";
DROP INDEX IF EXISTS public."IX_Companies_Name";
DROP INDEX IF EXISTS public."IX_Companies_CompanyCode";
DROP INDEX IF EXISTS public."IX_CipProjects_Status";
DROP INDEX IF EXISTS public."IX_CipProjects_ProjectNumber";
DROP INDEX IF EXISTS public."IX_CipProjects_ProjectManagerId";
DROP INDEX IF EXISTS public."IX_CipProjects_GlAccountId";
DROP INDEX IF EXISTS public."IX_CipProjects_DepartmentId";
DROP INDEX IF EXISTS public."IX_CipProjects_CostCenterId";
DROP INDEX IF EXISTS public."IX_CipProjects_ConvertedAssetId";
DROP INDEX IF EXISTS public."IX_CipCosts_TransactionDate";
DROP INDEX IF EXISTS public."IX_CipCosts_CipProjectId";
DROP INDEX IF EXISTS public."IX_CcaTransactions_CcaClassId_FiscalYear";
DROP INDEX IF EXISTS public."IX_CcaTransactions_AssetId";
DROP INDEX IF EXISTS public."IX_CcaClasses_ClassNumber";
DROP INDEX IF EXISTS public."IX_CcaClassBalances_CcaClassId_FiscalYear";
DROP INDEX IF EXISTS public."IX_CauseCodes_ParentId";
DROP INDEX IF EXISTS public."IX_CapitalImprovements_ImprovementDate";
DROP INDEX IF EXISTS public."IX_CapitalImprovements_AssetId";
DROP INDEX IF EXISTS public."IX_Books_DefaultPolicyId";
DROP INDEX IF EXISTS public."IX_Books_CompanyId";
DROP INDEX IF EXISTS public."IX_BookGlAccounts_BookId";
DROP INDEX IF EXISTS public."IX_BonusDepreciationRates_TaxYear";
DROP INDEX IF EXISTS public."IX_AuditLogs_Timestamp";
DROP INDEX IF EXISTS public."IX_AuditLogs_EntityType_EntityId";
DROP INDEX IF EXISTS public."IX_AuditLogs_EntityType";
DROP INDEX IF EXISTS public."IX_Attachments_MaintenanceEventId";
DROP INDEX IF EXISTS public."IX_Attachments_CipProjectId";
DROP INDEX IF EXISTS public."IX_Attachments_CipCostId";
DROP INDEX IF EXISTS public."IX_Attachments_CapitalImprovementId";
DROP INDEX IF EXISTS public."IX_Attachments_AssetTransferId";
DROP INDEX IF EXISTS public."IX_Attachments_AssetId";
DROP INDEX IF EXISTS public."IX_Assets_VendorId";
DROP INDEX IF EXISTS public."IX_Assets_SiteId";
DROP INDEX IF EXISTS public."IX_Assets_ParentAssetId";
DROP INDEX IF EXISTS public."IX_Assets_ManufacturerId";
DROP INDEX IF EXISTS public."IX_Assets_LocationId";
DROP INDEX IF EXISTS public."IX_Assets_DepartmentId";
DROP INDEX IF EXISTS public."IX_Assets_CostCenterId";
DROP INDEX IF EXISTS public."IX_Assets_CompanyId";
DROP INDEX IF EXISTS public."IX_Assets_AssetCategoryId";
DROP INDEX IF EXISTS public."IX_AssetTransfers_TransferDate";
DROP INDEX IF EXISTS public."IX_AssetTransfers_AssetId";
DROP INDEX IF EXISTS public."IX_AssetTaxSettings_CcaClassId";
DROP INDEX IF EXISTS public."IX_AssetTaxSettings_AssetId";
DROP INDEX IF EXISTS public."IX_AssetInventories_LastInventoryListId";
DROP INDEX IF EXISTS public."IX_AssetInventories_BarcodeNumber";
DROP INDEX IF EXISTS public."IX_AssetInventories_AssetId";
DROP INDEX IF EXISTS public."IX_AssetCategories_DepreciationPolicyId";
DROP INDEX IF EXISTS public."IX_AssetCategories_DepExpGlAccountId";
DROP INDEX IF EXISTS public."IX_AssetCategories_CompanyId";
DROP INDEX IF EXISTS public."IX_AssetCategories_Code";
DROP INDEX IF EXISTS public."IX_AssetCategories_AssetGlAccountId";
DROP INDEX IF EXISTS public."IX_AssetCategories_AccumDepGlAccountId";
DROP INDEX IF EXISTS public."IX_AssetBookSettings_BookId1";
DROP INDEX IF EXISTS public."IX_AssetBookSettings_BookId";
DROP INDEX IF EXISTS public."IX_AssetBookSettings_AssetId_BookId";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperations" DROP CONSTRAINT IF EXISTS "WorkOrderOperations_pkey";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationTools" DROP CONSTRAINT IF EXISTS "WorkOrderOperationTools_pkey";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationParts" DROP CONSTRAINT IF EXISTS "WorkOrderOperationParts_pkey";
ALTER TABLE IF EXISTS ONLY public."WorkOrderOperationLabors" DROP CONSTRAINT IF EXISTS "WorkOrderOperationLabor_pkey";
ALTER TABLE IF EXISTS ONLY public."Tenants" DROP CONSTRAINT IF EXISTS "Tenants_pkey";
ALTER TABLE IF EXISTS ONLY public."Tenants" DROP CONSTRAINT IF EXISTS "Tenants_Code_key";
ALTER TABLE IF EXISTS ONLY public."PMSchedules" DROP CONSTRAINT IF EXISTS "PMSchedules_pkey";
ALTER TABLE IF EXISTS ONLY public."PMOccurrences" DROP CONSTRAINT IF EXISTS "PMOccurrences_pkey";
ALTER TABLE IF EXISTS ONLY public."__EFMigrationsHistory" DROP CONSTRAINT IF EXISTS "PK___EFMigrationsHistory";
ALTER TABLE IF EXISTS ONLY public."WorkRequests" DROP CONSTRAINT IF EXISTS "PK_WorkRequests";
ALTER TABLE IF EXISTS ONLY public."WorkOrderTypes" DROP CONSTRAINT IF EXISTS "PK_WorkOrderTypes";
ALTER TABLE IF EXISTS ONLY public."WorkOrderParts" DROP CONSTRAINT IF EXISTS "PK_WorkOrderParts";
ALTER TABLE IF EXISTS ONLY public."WebhookSubscriptions" DROP CONSTRAINT IF EXISTS "PK_WebhookSubscriptions";
ALTER TABLE IF EXISTS ONLY public."WebhookDeliveryLogs" DROP CONSTRAINT IF EXISTS "PK_WebhookDeliveryLogs";
ALTER TABLE IF EXISTS ONLY public."Vendors" DROP CONSTRAINT IF EXISTS "PK_Vendors";
ALTER TABLE IF EXISTS ONLY public."VendorItemParts" DROP CONSTRAINT IF EXISTS "PK_VendorItemParts";
ALTER TABLE IF EXISTS ONLY public."VendorInvoices" DROP CONSTRAINT IF EXISTS "PK_VendorInvoices";
ALTER TABLE IF EXISTS ONLY public."VendorInvoiceLines" DROP CONSTRAINT IF EXISTS "PK_VendorInvoiceLines";
ALTER TABLE IF EXISTS ONLY public."Users" DROP CONSTRAINT IF EXISTS "PK_Users";
ALTER TABLE IF EXISTS ONLY public."UsefulLifeTables" DROP CONSTRAINT IF EXISTS "PK_UsefulLifeTables";
ALTER TABLE IF EXISTS ONLY public."UsefulLifeEntries" DROP CONSTRAINT IF EXISTS "PK_UsefulLifeEntries";
ALTER TABLE IF EXISTS ONLY public."UsTaxSettings" DROP CONSTRAINT IF EXISTS "PK_UsTaxSettings";
ALTER TABLE IF EXISTS ONLY public."UOMDefinitions" DROP CONSTRAINT IF EXISTS "PK_UOMDefinitions";
ALTER TABLE IF EXISTS ONLY public."Technicians" DROP CONSTRAINT IF EXISTS "PK_Technicians";
ALTER TABLE IF EXISTS ONLY public."TaxCodes" DROP CONSTRAINT IF EXISTS "PK_TaxCodes";
ALTER TABLE IF EXISTS ONLY public."Skills" DROP CONSTRAINT IF EXISTS "PK_Skills";
ALTER TABLE IF EXISTS ONLY public."Sites" DROP CONSTRAINT IF EXISTS "PK_Sites";
ALTER TABLE IF EXISTS ONLY public."ShippingMethods" DROP CONSTRAINT IF EXISTS "PK_ShippingMethods";
ALTER TABLE IF EXISTS ONLY public."Section179Limits" DROP CONSTRAINT IF EXISTS "PK_Section179Limits";
ALTER TABLE IF EXISTS ONLY public."ReorderAlerts" DROP CONSTRAINT IF EXISTS "PK_ReorderAlerts";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitions" DROP CONSTRAINT IF EXISTS "PK_PurchaseRequisitions";
ALTER TABLE IF EXISTS ONLY public."PurchaseRequisitionLines" DROP CONSTRAINT IF EXISTS "PK_PurchaseRequisitionLines";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrders" DROP CONSTRAINT IF EXISTS "PK_PurchaseOrders";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderReleases" DROP CONSTRAINT IF EXISTS "PK_PurchaseOrderReleases";
ALTER TABLE IF EXISTS ONLY public."PurchaseOrderLines" DROP CONSTRAINT IF EXISTS "PK_PurchaseOrderLines";
ALTER TABLE IF EXISTS ONLY public."ProjectManagers" DROP CONSTRAINT IF EXISTS "PK_ProjectManagers";
ALTER TABLE IF EXISTS ONLY public."ProblemCodes" DROP CONSTRAINT IF EXISTS "PK_ProblemCodes";
ALTER TABLE IF EXISTS ONLY public."PriorityLevels" DROP CONSTRAINT IF EXISTS "PK_PriorityLevels";
ALTER TABLE IF EXISTS ONLY public."PolicyCategoryDefaults" DROP CONSTRAINT IF EXISTS "PK_PolicyCategoryDefaults";
ALTER TABLE IF EXISTS ONLY public."PeriodLocks" DROP CONSTRAINT IF EXISTS "PK_PeriodLocks";
ALTER TABLE IF EXISTS ONLY public."PaymentTerms" DROP CONSTRAINT IF EXISTS "PK_PaymentTerms";
ALTER TABLE IF EXISTS ONLY public."PartialDisposals" DROP CONSTRAINT IF EXISTS "PK_PartialDisposals";
ALTER TABLE IF EXISTS ONLY public."PMTemplates" DROP CONSTRAINT IF EXISTS "PK_PMTemplates";
ALTER TABLE IF EXISTS ONLY public."PMTemplateRevisions" DROP CONSTRAINT IF EXISTS "PK_PMTemplateRevisions";
ALTER TABLE IF EXISTS ONLY public."PMTemplateRevisionOperations" DROP CONSTRAINT IF EXISTS "PK_PMTemplateRevisionOperations";
ALTER TABLE IF EXISTS ONLY public."PMTemplateItems" DROP CONSTRAINT IF EXISTS "PK_PMTemplateItems";
ALTER TABLE IF EXISTS ONLY public."PMTemplateAssets" DROP CONSTRAINT IF EXISTS "PK_PMTemplateAssets";
ALTER TABLE IF EXISTS ONLY public."OutboxEvents" DROP CONSTRAINT IF EXISTS "PK_OutboxEvents";
ALTER TABLE IF EXISTS ONLY public."NumberingSequences" DROP CONSTRAINT IF EXISTS "PK_NumberingSequences";
ALTER TABLE IF EXISTS ONLY public."MeterReadings" DROP CONSTRAINT IF EXISTS "PK_MeterReadings";
ALTER TABLE IF EXISTS ONLY public."Manufacturers" DROP CONSTRAINT IF EXISTS "PK_Manufacturers";
ALTER TABLE IF EXISTS ONLY public."MaintenanceTypeCodes" DROP CONSTRAINT IF EXISTS "PK_MaintenanceTypeCodes";
ALTER TABLE IF EXISTS ONLY public."MaintenanceSchedules" DROP CONSTRAINT IF EXISTS "PK_MaintenanceSchedules";
ALTER TABLE IF EXISTS ONLY public."MaintenanceEvents" DROP CONSTRAINT IF EXISTS "PK_MaintenanceEvents";
ALTER TABLE IF EXISTS ONLY public."Locations" DROP CONSTRAINT IF EXISTS "PK_Locations";
ALTER TABLE IF EXISTS ONLY public."LessonsLearned" DROP CONSTRAINT IF EXISTS "PK_LessonsLearned";
ALTER TABLE IF EXISTS ONLY public."LaborTypes" DROP CONSTRAINT IF EXISTS "PK_LaborTypes";
ALTER TABLE IF EXISTS ONLY public."LaborRates" DROP CONSTRAINT IF EXISTS "PK_LaborRates";
ALTER TABLE IF EXISTS ONLY public."Kits" DROP CONSTRAINT IF EXISTS "PK_Kits";
ALTER TABLE IF EXISTS ONLY public."KitItems" DROP CONSTRAINT IF EXISTS "PK_KitItems";
ALTER TABLE IF EXISTS ONLY public."JournalLines" DROP CONSTRAINT IF EXISTS "PK_JournalLines";
ALTER TABLE IF EXISTS ONLY public."JournalEntries" DROP CONSTRAINT IF EXISTS "PK_JournalEntries";
ALTER TABLE IF EXISTS ONLY public."Items" DROP CONSTRAINT IF EXISTS "PK_Items";
ALTER TABLE IF EXISTS ONLY public."ItemVendors" DROP CONSTRAINT IF EXISTS "PK_ItemVendors";
ALTER TABLE IF EXISTS ONLY public."ItemTransactions" DROP CONSTRAINT IF EXISTS "PK_ItemTransactions";
ALTER TABLE IF EXISTS ONLY public."ItemSupersessions" DROP CONSTRAINT IF EXISTS "PK_ItemSupersessions";
ALTER TABLE IF EXISTS ONLY public."ItemRevisions" DROP CONSTRAINT IF EXISTS "PK_ItemRevisions";
ALTER TABLE IF EXISTS ONLY public."ItemManufacturerParts" DROP CONSTRAINT IF EXISTS "PK_ItemManufacturerParts";
ALTER TABLE IF EXISTS ONLY public."ItemInventories2" DROP CONSTRAINT IF EXISTS "PK_ItemInventories2";
ALTER TABLE IF EXISTS ONLY public."ItemImages" DROP CONSTRAINT IF EXISTS "PK_ItemImages";
ALTER TABLE IF EXISTS ONLY public."ItemCompanyStockings" DROP CONSTRAINT IF EXISTS "PK_ItemCompanyStockings";
ALTER TABLE IF EXISTS ONLY public."ItemCategories" DROP CONSTRAINT IF EXISTS "PK_ItemCategories";
ALTER TABLE IF EXISTS ONLY public."ItemApprovedVendors" DROP CONSTRAINT IF EXISTS "PK_ItemApprovedVendors";
ALTER TABLE IF EXISTS ONLY public."ItemAlternates" DROP CONSTRAINT IF EXISTS "PK_ItemAlternates";
ALTER TABLE IF EXISTS ONLY public."InvoicePayments" DROP CONSTRAINT IF EXISTS "PK_InvoicePayments";
ALTER TABLE IF EXISTS ONLY public."InventoryScans" DROP CONSTRAINT IF EXISTS "PK_InventoryScans";
ALTER TABLE IF EXISTS ONLY public."InventoryLists" DROP CONSTRAINT IF EXISTS "PK_InventoryLists";
ALTER TABLE IF EXISTS ONLY public."IntegrationMappings" DROP CONSTRAINT IF EXISTS "PK_IntegrationMappings";
ALTER TABLE IF EXISTS ONLY public."IntegrationEndpoints" DROP CONSTRAINT IF EXISTS "PK_IntegrationEndpoints";
ALTER TABLE IF EXISTS ONLY public."InboundEvents" DROP CONSTRAINT IF EXISTS "PK_InboundEvents";
ALTER TABLE IF EXISTS ONLY public."GoodsReceipts" DROP CONSTRAINT IF EXISTS "PK_GoodsReceipts";
ALTER TABLE IF EXISTS ONLY public."GoodsReceiptLines" DROP CONSTRAINT IF EXISTS "PK_GoodsReceiptLines";
ALTER TABLE IF EXISTS ONLY public."GlAccounts" DROP CONSTRAINT IF EXISTS "PK_GlAccounts";
ALTER TABLE IF EXISTS ONLY public."FiscalYears" DROP CONSTRAINT IF EXISTS "PK_FiscalYears";
ALTER TABLE IF EXISTS ONLY public."FiscalPeriods" DROP CONSTRAINT IF EXISTS "PK_FiscalPeriods";
ALTER TABLE IF EXISTS ONLY public."FailureCodes" DROP CONSTRAINT IF EXISTS "PK_FailureCodes";
ALTER TABLE IF EXISTS ONLY public."ExchangeRates" DROP CONSTRAINT IF EXISTS "PK_ExchangeRates";
ALTER TABLE IF EXISTS ONLY public."DepreciationRuns" DROP CONSTRAINT IF EXISTS "PK_DepreciationRuns";
ALTER TABLE IF EXISTS ONLY public."DepreciationRunDetails" DROP CONSTRAINT IF EXISTS "PK_DepreciationRunDetails";
ALTER TABLE IF EXISTS ONLY public."DepreciationPolicies" DROP CONSTRAINT IF EXISTS "PK_DepreciationPolicies";
ALTER TABLE IF EXISTS ONLY public."Departments" DROP CONSTRAINT IF EXISTS "PK_Departments";
ALTER TABLE IF EXISTS ONLY public."Currencies" DROP CONSTRAINT IF EXISTS "PK_Currencies";
ALTER TABLE IF EXISTS ONLY public."Crafts" DROP CONSTRAINT IF EXISTS "PK_Crafts";
ALTER TABLE IF EXISTS ONLY public."CostCenters" DROP CONSTRAINT IF EXISTS "PK_CostCenters";
ALTER TABLE IF EXISTS ONLY public."Companies" DROP CONSTRAINT IF EXISTS "PK_Companies";
ALTER TABLE IF EXISTS ONLY public."CipProjects" DROP CONSTRAINT IF EXISTS "PK_CipProjects";
ALTER TABLE IF EXISTS ONLY public."CipCosts" DROP CONSTRAINT IF EXISTS "PK_CipCosts";
ALTER TABLE IF EXISTS ONLY public."CcaTransactions" DROP CONSTRAINT IF EXISTS "PK_CcaTransactions";
ALTER TABLE IF EXISTS ONLY public."CcaClasses" DROP CONSTRAINT IF EXISTS "PK_CcaClasses";
ALTER TABLE IF EXISTS ONLY public."CcaClassBalances" DROP CONSTRAINT IF EXISTS "PK_CcaClassBalances";
ALTER TABLE IF EXISTS ONLY public."CauseCodes" DROP CONSTRAINT IF EXISTS "PK_CauseCodes";
ALTER TABLE IF EXISTS ONLY public."CapitalImprovements" DROP CONSTRAINT IF EXISTS "PK_CapitalImprovements";
ALTER TABLE IF EXISTS ONLY public."BulkOperations" DROP CONSTRAINT IF EXISTS "PK_BulkOperations";
ALTER TABLE IF EXISTS ONLY public."Books" DROP CONSTRAINT IF EXISTS "PK_Books";
ALTER TABLE IF EXISTS ONLY public."BookGlAccounts" DROP CONSTRAINT IF EXISTS "PK_BookGlAccounts";
ALTER TABLE IF EXISTS ONLY public."BonusDepreciationRates" DROP CONSTRAINT IF EXISTS "PK_BonusDepreciationRates";
ALTER TABLE IF EXISTS ONLY public."AuditLogs" DROP CONSTRAINT IF EXISTS "PK_AuditLogs";
ALTER TABLE IF EXISTS ONLY public."Attachments" DROP CONSTRAINT IF EXISTS "PK_Attachments";
ALTER TABLE IF EXISTS ONLY public."Assets" DROP CONSTRAINT IF EXISTS "PK_Assets";
ALTER TABLE IF EXISTS ONLY public."AssetTransfers" DROP CONSTRAINT IF EXISTS "PK_AssetTransfers";
ALTER TABLE IF EXISTS ONLY public."AssetTaxSettings" DROP CONSTRAINT IF EXISTS "PK_AssetTaxSettings";
ALTER TABLE IF EXISTS ONLY public."AssetInventories" DROP CONSTRAINT IF EXISTS "PK_AssetInventories";
ALTER TABLE IF EXISTS ONLY public."AssetCategories" DROP CONSTRAINT IF EXISTS "PK_AssetCategories";
ALTER TABLE IF EXISTS ONLY public."AssetBookSettings" DROP CONSTRAINT IF EXISTS "PK_AssetBookSettings";
ALTER TABLE IF EXISTS ONLY public."ApprovalWorkflows" DROP CONSTRAINT IF EXISTS "PK_ApprovalWorkflows";
ALTER TABLE IF EXISTS ONLY public."ApiKeys" DROP CONSTRAINT IF EXISTS "PK_ApiKeys";
ALTER TABLE IF EXISTS ONLY public."ActionCodes" DROP CONSTRAINT IF EXISTS "PK_ActionCodes";
ALTER TABLE IF EXISTS public."WorkOrderOperations" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."WorkOrderOperationTools" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."WorkOrderOperationParts" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."WorkOrderOperationLabors" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."Tenants" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."PMSchedules" ALTER COLUMN "Id" DROP DEFAULT;
ALTER TABLE IF EXISTS public."PMOccurrences" ALTER COLUMN "Id" DROP DEFAULT;
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
DROP TABLE IF EXISTS public."WorkRequests";
DROP TABLE IF EXISTS public."WorkOrderTypes";
DROP TABLE IF EXISTS public."WorkOrderParts";
DROP SEQUENCE IF EXISTS public."WorkOrderOperations_Id_seq";
DROP TABLE IF EXISTS public."WorkOrderOperations";
DROP SEQUENCE IF EXISTS public."WorkOrderOperationTools_Id_seq";
DROP TABLE IF EXISTS public."WorkOrderOperationTools";
DROP SEQUENCE IF EXISTS public."WorkOrderOperationParts_Id_seq";
DROP TABLE IF EXISTS public."WorkOrderOperationParts";
DROP SEQUENCE IF EXISTS public."WorkOrderOperationLabor_Id_seq";
DROP TABLE IF EXISTS public."WorkOrderOperationLabors";
DROP TABLE IF EXISTS public."WebhookSubscriptions";
DROP TABLE IF EXISTS public."WebhookDeliveryLogs";
DROP TABLE IF EXISTS public."Vendors";
DROP TABLE IF EXISTS public."VendorItemParts";
DROP TABLE IF EXISTS public."VendorInvoices";
DROP TABLE IF EXISTS public."VendorInvoiceLines";
DROP TABLE IF EXISTS public."Users";
DROP TABLE IF EXISTS public."UsefulLifeTables";
DROP TABLE IF EXISTS public."UsefulLifeEntries";
DROP TABLE IF EXISTS public."UsTaxSettings";
DROP TABLE IF EXISTS public."UOMDefinitions";
DROP SEQUENCE IF EXISTS public."Tenants_Id_seq";
DROP TABLE IF EXISTS public."Tenants";
DROP TABLE IF EXISTS public."Technicians";
DROP TABLE IF EXISTS public."TaxCodes";
DROP TABLE IF EXISTS public."Skills";
DROP TABLE IF EXISTS public."Sites";
DROP TABLE IF EXISTS public."ShippingMethods";
DROP TABLE IF EXISTS public."Section179Limits";
DROP TABLE IF EXISTS public."ReorderAlerts";
DROP TABLE IF EXISTS public."PurchaseRequisitions";
DROP TABLE IF EXISTS public."PurchaseRequisitionLines";
DROP TABLE IF EXISTS public."PurchaseOrders";
DROP TABLE IF EXISTS public."PurchaseOrderReleases";
DROP TABLE IF EXISTS public."PurchaseOrderLines";
DROP TABLE IF EXISTS public."ProjectManagers";
DROP TABLE IF EXISTS public."ProblemCodes";
DROP TABLE IF EXISTS public."PriorityLevels";
DROP TABLE IF EXISTS public."PolicyCategoryDefaults";
DROP TABLE IF EXISTS public."PeriodLocks";
DROP TABLE IF EXISTS public."PaymentTerms";
DROP TABLE IF EXISTS public."PartialDisposals";
DROP TABLE IF EXISTS public."PMTemplates";
DROP TABLE IF EXISTS public."PMTemplateRevisions";
DROP TABLE IF EXISTS public."PMTemplateRevisionOperations";
DROP TABLE IF EXISTS public."PMTemplateItems";
DROP TABLE IF EXISTS public."PMTemplateAssets";
DROP SEQUENCE IF EXISTS public."PMSchedules_Id_seq";
DROP TABLE IF EXISTS public."PMSchedules";
DROP SEQUENCE IF EXISTS public."PMOccurrences_Id_seq";
DROP TABLE IF EXISTS public."PMOccurrences";
DROP TABLE IF EXISTS public."OutboxEvents";
DROP TABLE IF EXISTS public."NumberingSequences";
DROP TABLE IF EXISTS public."MeterReadings";
DROP TABLE IF EXISTS public."Manufacturers";
DROP TABLE IF EXISTS public."MaintenanceTypeCodes";
DROP TABLE IF EXISTS public."MaintenanceSchedules";
DROP TABLE IF EXISTS public."MaintenanceEvents";
DROP TABLE IF EXISTS public."Locations";
DROP TABLE IF EXISTS public."LessonsLearned";
DROP TABLE IF EXISTS public."LaborTypes";
DROP TABLE IF EXISTS public."LaborRates";
DROP TABLE IF EXISTS public."Kits";
DROP TABLE IF EXISTS public."KitItems";
DROP TABLE IF EXISTS public."JournalLines";
DROP TABLE IF EXISTS public."JournalEntries";
DROP TABLE IF EXISTS public."Items";
DROP TABLE IF EXISTS public."ItemVendors";
DROP TABLE IF EXISTS public."ItemTransactions";
DROP TABLE IF EXISTS public."ItemSupersessions";
DROP TABLE IF EXISTS public."ItemRevisions";
DROP TABLE IF EXISTS public."ItemManufacturerParts";
DROP TABLE IF EXISTS public."ItemInventories2";
DROP TABLE IF EXISTS public."ItemImages";
DROP TABLE IF EXISTS public."ItemCompanyStockings";
DROP TABLE IF EXISTS public."ItemCategories";
DROP TABLE IF EXISTS public."ItemApprovedVendors";
DROP TABLE IF EXISTS public."ItemAlternates";
DROP TABLE IF EXISTS public."InvoicePayments";
DROP TABLE IF EXISTS public."InventoryScans";
DROP TABLE IF EXISTS public."InventoryLists";
DROP TABLE IF EXISTS public."IntegrationMappings";
DROP TABLE IF EXISTS public."IntegrationEndpoints";
DROP TABLE IF EXISTS public."InboundEvents";
DROP TABLE IF EXISTS public."GoodsReceipts";
DROP TABLE IF EXISTS public."GoodsReceiptLines";
DROP TABLE IF EXISTS public."GlAccounts";
DROP TABLE IF EXISTS public."FiscalYears";
DROP TABLE IF EXISTS public."FiscalPeriods";
DROP TABLE IF EXISTS public."FailureCodes";
DROP TABLE IF EXISTS public."ExchangeRates";
DROP TABLE IF EXISTS public."DepreciationRuns";
DROP TABLE IF EXISTS public."DepreciationRunDetails";
DROP TABLE IF EXISTS public."DepreciationPolicies";
DROP TABLE IF EXISTS public."Departments";
DROP TABLE IF EXISTS public."Currencies";
DROP TABLE IF EXISTS public."Crafts";
DROP TABLE IF EXISTS public."CostCenters";
DROP TABLE IF EXISTS public."Companies";
DROP TABLE IF EXISTS public."CipProjects";
DROP TABLE IF EXISTS public."CipCosts";
DROP TABLE IF EXISTS public."CcaTransactions";
DROP TABLE IF EXISTS public."CcaClasses";
DROP TABLE IF EXISTS public."CcaClassBalances";
DROP TABLE IF EXISTS public."CauseCodes";
DROP TABLE IF EXISTS public."CapitalImprovements";
DROP TABLE IF EXISTS public."BulkOperations";
DROP TABLE IF EXISTS public."Books";
DROP TABLE IF EXISTS public."BookGlAccounts";
DROP TABLE IF EXISTS public."BonusDepreciationRates";
DROP TABLE IF EXISTS public."AuditLogs";
DROP TABLE IF EXISTS public."Attachments";
DROP TABLE IF EXISTS public."Assets";
DROP TABLE IF EXISTS public."AssetTransfers";
DROP TABLE IF EXISTS public."AssetTaxSettings";
DROP TABLE IF EXISTS public."AssetInventories";
DROP TABLE IF EXISTS public."AssetCategories";
DROP TABLE IF EXISTS public."AssetBookSettings";
DROP TABLE IF EXISTS public."ApprovalWorkflows";
DROP TABLE IF EXISTS public."ApiKeys";
DROP TABLE IF EXISTS public."ActionCodes";
-- *not* dropping schema, since initdb creates it
--
-- Name: public; Type: SCHEMA; Schema: -; Owner: -
--

-- *not* creating schema, since initdb creates it


--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON SCHEMA public IS '';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: ActionCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ActionCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Category" integer NOT NULL,
    "RequiresParts" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: ActionCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ActionCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ActionCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ApiKeys; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ApiKeys" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "KeyHash" character varying(64) NOT NULL,
    "KeyPrefix" character varying(10) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastUsedAt" timestamp with time zone,
    "ExpiresAt" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "Scopes" character varying(500),
    "CreatedBy" character varying(100)
);


--
-- Name: ApiKeys_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ApiKeys" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ApiKeys_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ApprovalWorkflows; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ApprovalWorkflows" (
    "Id" integer NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Type" integer NOT NULL,
    "ThresholdAmount" numeric NOT NULL,
    "RequiredApprovals" integer NOT NULL,
    "ApproverRoles" character varying(500),
    "ApproverUserIds" character varying(500),
    "RequireSequentialApproval" boolean NOT NULL,
    "AutoApproveIfBelowThreshold" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: ApprovalWorkflows_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ApprovalWorkflows" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ApprovalWorkflows_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AssetBookSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AssetBookSettings" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "BookId" integer NOT NULL,
    "MethodOverride" integer,
    "ConventionOverride" integer,
    "UsefulLifeMonthsOverride" integer,
    "SalvageValueOverride" numeric,
    "InServiceDateOverride" timestamp with time zone,
    "CostBasisOverride" numeric,
    "Section179Deduction" numeric,
    "BonusDepreciationPercent" numeric,
    "IsExcludedFromBook" boolean NOT NULL,
    "Notes" character varying(500),
    "AccumulatedDepreciation" numeric NOT NULL,
    "BookValue" numeric NOT NULL,
    "LastDepreciationDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "BookId1" integer
);


--
-- Name: AssetBookSettings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AssetBookSettings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AssetBookSettings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AssetCategories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AssetCategories" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(200),
    "DefaultMacrsClass" integer NOT NULL,
    "DefaultCcaClassId" integer,
    "DefaultUsefulLifeMonths" integer NOT NULL,
    "DefaultSalvagePercent" numeric(5,2) NOT NULL,
    "AssetGlAccountId" integer,
    "AccumDepGlAccountId" integer,
    "DepExpGlAccountId" integer,
    "IsActive" boolean NOT NULL,
    "CompanyId" integer,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "DepreciationPolicyId" integer
);


--
-- Name: AssetCategories_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AssetCategories" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AssetCategories_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AssetInventories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AssetInventories" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "BarcodeNumber" character varying(100),
    "BarcodeType" character varying(50),
    "LastScanDate" timestamp with time zone,
    "LastScanLocation" character varying(100),
    "LastScannedBy" character varying(100),
    "Condition" integer NOT NULL,
    "ConditionNotes" character varying(500),
    "PhotoPath" character varying(500),
    "IsReconciled" boolean NOT NULL,
    "LastReconciledDate" timestamp with time zone,
    "LastInventoryListId" integer
);


--
-- Name: AssetInventories_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AssetInventories" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AssetInventories_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AssetTaxSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AssetTaxSettings" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "CcaClassId" integer NOT NULL,
    "AvailableForUseDate" timestamp with time zone,
    "AvailableForUseOverride" boolean NOT NULL,
    "EligibleForAcceleratedIncentive" boolean NOT NULL,
    "CapitalCost" numeric(18,2) NOT NULL,
    "Proceeds" numeric(18,2),
    "DisposalDate" timestamp with time zone,
    "DisposalType" integer,
    "Notes" character varying(500)
);


--
-- Name: AssetTaxSettings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AssetTaxSettings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AssetTaxSettings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AssetTransfers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AssetTransfers" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "TransferDate" timestamp with time zone NOT NULL,
    "FromLocation" character varying(100),
    "FromBay" character varying(50),
    "FromDepartment" character varying(100),
    "ToLocation" character varying(100),
    "ToBay" character varying(50),
    "ToDepartment" character varying(100),
    "Reason" character varying(100),
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: AssetTransfers_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AssetTransfers" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AssetTransfers_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Assets; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Assets" (
    "Id" integer NOT NULL,
    "AssetNumber" character varying(50) NOT NULL,
    "Description" character varying(200) NOT NULL,
    "Model" character varying(200),
    "SerialNumber" character varying(100),
    "InServiceDate" timestamp with time zone NOT NULL,
    "FiscalPurchaseYear" integer,
    "AcquisitionCost" numeric(18,2) NOT NULL,
    "AccumulatedDepreciation" numeric(18,2) NOT NULL,
    "SalvageValue" numeric(18,2) NOT NULL,
    "BookValue" numeric(18,2),
    "FairMarketValue" numeric(18,2),
    "DepreciationMethod" integer NOT NULL,
    "DepreciationRate" numeric(5,2),
    "Currency" character varying(3) DEFAULT 'CAD'::character varying NOT NULL,
    "LastDepreciationDate" timestamp with time zone,
    "NextDepreciationDate" timestamp with time zone,
    "Bay" text,
    "Department" text,
    "VendorId" integer,
    "ManufacturerId" integer,
    "CostCenterId" integer,
    "DepartmentId" integer,
    "LocationId" integer,
    "AssetCategoryId" integer,
    "Active" boolean NOT NULL,
    "UsefulLifeMonths" integer NOT NULL,
    "Status" integer NOT NULL,
    "DisposalDate" timestamp with time zone,
    "DisposalProceeds" numeric(18,2),
    "GainLossOnDisposal" numeric(18,2),
    "CompanyId" integer,
    "SiteId" integer,
    "Aisle" character varying(50),
    "Amperage" numeric(10,2),
    "AnnualEnergyConsumptionKWH" numeric(12,2),
    "AssetType" character varying(50),
    "CalibrationCertificateNumber" character varying(100),
    "CalibrationFrequencyDays" integer,
    "CalibrationRequired" boolean DEFAULT false NOT NULL,
    "CalibrationStatus" character varying(20),
    "CalibrationType" character varying(50),
    "CalibrationVendor" character varying(100),
    "Capacity" numeric(10,2),
    "CapacityUOM" character varying(20),
    "CellId" character varying(50),
    "ClassificationCode" character varying(50),
    "Condition" integer DEFAULT 0 NOT NULL,
    "ConfinedSpaceEntry" boolean DEFAULT false NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "CurrentAvailability" numeric(5,2),
    "CurrentMeterReading" numeric(18,2),
    "CurrentOEE" numeric(5,2),
    "CurrentPerformance" numeric(5,2),
    "CurrentPressure" numeric(8,2),
    "CurrentQuality" numeric(5,2),
    "CurrentTemperature" numeric(6,2),
    "CurrentVibration" numeric(8,3),
    "DataHistorianTag" character varying(200),
    "Dimensions" character varying(100),
    "DisposalMethod" character varying(50),
    "DisposalReason" character varying(500),
    "EPAPermitNumber" character varying(100),
    "EmissionsMonitored" boolean DEFAULT false NOT NULL,
    "EnergyClass" character varying(10),
    "EnergyMeterId" character varying(50),
    "EnvironmentalClass" character varying(50),
    "FailureClassId" integer,
    "GLAccumDepAccount" character varying(20),
    "GLAssetAccount" character varying(20),
    "GLDepExpenseAccount" character varying(20),
    "HasMeter" boolean DEFAULT false NOT NULL,
    "HasStandbyMode" boolean DEFAULT false NOT NULL,
    "HealthScoreLastCalculated" timestamp with time zone,
    "HighVoltage" boolean DEFAULT false NOT NULL,
    "Horsepower" numeric(10,2),
    "HotWorkPermitRequired" boolean DEFAULT false NOT NULL,
    "IPAddress" character varying(45),
    "IdealCycleTimeSeconds" numeric(10,4),
    "IdlePowerConsumptionKW" numeric(12,2),
    "InstallDate" timestamp with time zone,
    "InsuredValue" numeric(18,2),
    "InvoiceNumber" character varying(50),
    "IoTConnectionStatus" character varying(20),
    "IoTDeviceId" character varying(100),
    "IoTEnabled" boolean DEFAULT false NOT NULL,
    "IoTEndpointUrl" character varying(100),
    "IoTGatewayId" character varying(100),
    "IoTPollingIntervalSeconds" integer,
    "IoTProtocol" character varying(30),
    "IsCritical" boolean DEFAULT false NOT NULL,
    "IsLinear" boolean DEFAULT false NOT NULL,
    "IsRotating" boolean DEFAULT false NOT NULL,
    "KilowattRating" numeric(10,2),
    "LOTOProcedureId" character varying(50),
    "LastCalibrationDate" timestamp with time zone,
    "LastIoTCommunication" timestamp with time zone,
    "LastMeterReadingDate" timestamp with time zone,
    "LockoutTagoutRequired" boolean DEFAULT false NOT NULL,
    "LongDescription" character varying(500),
    "MACAddress" character varying(17),
    "MeterType" character varying(20),
    "ModifiedAt" timestamp with time zone,
    "ModifiedBy" character varying(100),
    "NextCalibrationDue" timestamp with time zone,
    "Notes" character varying(2000),
    "OEELastCalculated" timestamp with time zone,
    "OEETracked" boolean DEFAULT false NOT NULL,
    "OSHAClassification" character varying(100),
    "OperationId" character varying(50),
    "ParentAssetId" integer,
    "PlannedProductionHoursPerDay" integer,
    "Position" character varying(50),
    "PredictedFailureDate" timestamp with time zone,
    "PredictedFailureReason" character varying(200),
    "PredictiveHealthScore" numeric(5,2),
    "PressureAlarmThreshold" numeric(8,2),
    "PressureUOM" character varying(10),
    "PressureWarningThreshold" numeric(8,2),
    "Priority" integer DEFAULT 0 NOT NULL,
    "ProcessId" character varying(50),
    "ProductionLineId" character varying(50),
    "ProductionLineName" character varying(100),
    "PurchaseDate" timestamp with time zone,
    "PurchaseOrderNumber" character varying(50),
    "RPM" integer,
    "RatedPowerConsumptionKW" numeric(12,2),
    "ReplacementCost" numeric(18,2) DEFAULT 0.0 NOT NULL,
    "RoutingSequence" integer,
    "Row" character varying(50),
    "SCADATag" character varying(200),
    "SafetyClassification" character varying(20),
    "SafetyNotes" character varying(500),
    "SensorReadingsLastUpdated" timestamp with time zone,
    "ShiftCalendarId" character varying(50),
    "StandardRunRate" numeric(12,4),
    "StandardRunRateUOM" character varying(20),
    "StandbyPowerConsumptionKW" numeric(12,2),
    "TagNumber" character varying(50),
    "TargetAvailability" numeric(5,2),
    "TargetOEE" numeric(5,2),
    "TargetPerformance" numeric(5,2),
    "TargetQuality" numeric(5,2),
    "TemperatureAlarmThreshold" numeric(6,2),
    "TemperatureWarningThreshold" numeric(6,2),
    "VibrationAlarmThreshold" numeric(8,3),
    "VibrationWarningThreshold" numeric(8,3),
    "Voltage" integer,
    "WarrantyContractNumber" character varying(100),
    "WarrantyEndDate" timestamp with time zone,
    "WarrantyStartDate" timestamp with time zone,
    "WarrantyVendorId" integer,
    "Weight" numeric(10,2),
    "WeightUOM" character varying(10),
    "WorkCenterId" character varying(50),
    "WorkCenterName" character varying(100),
    "ImageUrl" character varying(500)
);


--
-- Name: Assets_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Assets" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Assets_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Attachments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Attachments" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "MaintenanceEventId" integer,
    "CipProjectId" integer,
    "CipCostId" integer,
    "AssetTransferId" integer,
    "CapitalImprovementId" integer,
    "Source" integer NOT NULL,
    "FileName" character varying(255) NOT NULL,
    "StoredFileName" character varying(255) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "FileSize" bigint NOT NULL,
    "Description" character varying(500),
    "Category" integer NOT NULL,
    "UploadedBy" character varying(100),
    "UploadedAt" timestamp with time zone NOT NULL
);


--
-- Name: Attachments_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Attachments" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Attachments_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AuditLogs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AuditLogs" (
    "Id" integer NOT NULL,
    "EntityType" character varying(100) NOT NULL,
    "EntityId" integer,
    "Action" character varying(50) NOT NULL,
    "BeforeJson" text,
    "AfterJson" text,
    "Username" character varying(100),
    "Timestamp" timestamp with time zone NOT NULL,
    "IpAddress" character varying(45),
    "Description" character varying(500)
);


--
-- Name: AuditLogs_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AuditLogs" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AuditLogs_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: BonusDepreciationRates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BonusDepreciationRates" (
    "Id" integer NOT NULL,
    "TaxYear" integer NOT NULL,
    "Rate" numeric(5,2) NOT NULL,
    "Notes" character varying(200)
);


--
-- Name: BonusDepreciationRates_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."BonusDepreciationRates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."BonusDepreciationRates_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: BookGlAccounts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BookGlAccounts" (
    "Id" integer NOT NULL,
    "BookId" integer NOT NULL,
    "Asset" character varying(50),
    "AccumulatedDepreciation" character varying(50),
    "DepreciationExpense" character varying(50),
    "GainOnDisposal" character varying(50),
    "LossOnDisposal" character varying(50),
    "Clearing" character varying(50),
    "CIP" character varying(50)
);


--
-- Name: BookGlAccounts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."BookGlAccounts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."BookGlAccounts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Books; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Books" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Method" integer NOT NULL,
    "Convention" integer NOT NULL,
    "UsefulLifeOverrideMonths" integer,
    "GlAccountDepExp" character varying(50),
    "GlAccountAccumDep" character varying(50),
    "BookType" integer NOT NULL,
    "TaxJurisdiction" integer NOT NULL,
    "CompanyId" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "AllowManualDepreciation" boolean DEFAULT false NOT NULL,
    "AutoPostOnPeriodClose" boolean DEFAULT false NOT NULL,
    "CalculateOnlyNoPosting" boolean DEFAULT false NOT NULL,
    "CalculationFrequency" integer DEFAULT 0 NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT '-infinity'::timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "DefaultPolicyId" integer,
    "Description" character varying(500),
    "GlAccountAssetClearing" character varying(50),
    "GlAccountCIP" character varying(50),
    "GlAccountGainOnDisposal" character varying(50),
    "GlAccountLossOnDisposal" character varying(50),
    "IsPrimaryBook" boolean DEFAULT false NOT NULL,
    "RequireApprovalToPost" boolean DEFAULT false NOT NULL,
    "TrackBudgetVsActual" boolean DEFAULT false NOT NULL
);


--
-- Name: Books_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Books" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Books_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: BulkOperations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BulkOperations" (
    "Id" integer NOT NULL,
    "OperationType" integer NOT NULL,
    "OperationDate" timestamp with time zone NOT NULL,
    "AssetsAffected" integer NOT NULL,
    "Description" character varying(500),
    "NewLocation" character varying(100),
    "NewDepartment" character varying(100),
    "NewStatus" integer,
    "ProcessedBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "AssetIds" text
);


--
-- Name: BulkOperations_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."BulkOperations" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."BulkOperations_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CapitalImprovements; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CapitalImprovements" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "ImprovementDate" timestamp with time zone NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Cost" numeric NOT NULL,
    "Vendor" character varying(200),
    "InvoiceNumber" character varying(100),
    "UsefulLifeExtensionMonths" integer,
    "Notes" text,
    "Capitalized" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: CapitalImprovements_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CapitalImprovements" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CapitalImprovements_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CauseCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CauseCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "ParentId" integer,
    "Category" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: CauseCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CauseCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CauseCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CcaClassBalances; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CcaClassBalances" (
    "Id" integer NOT NULL,
    "CcaClassId" integer NOT NULL,
    "FiscalYear" integer NOT NULL,
    "OpeningUcc" numeric(18,2) NOT NULL,
    "Additions" numeric(18,2) NOT NULL,
    "Dispositions" numeric(18,2) NOT NULL,
    "HalfYearAdjustment" numeric(18,2) NOT NULL,
    "BaseForCca" numeric(18,2) NOT NULL,
    "CcaClaimed" numeric(18,2) NOT NULL,
    "ClosingUcc" numeric(18,2) NOT NULL,
    "Recapture" numeric(18,2),
    "TerminalLoss" numeric(18,2),
    "IsPosted" boolean NOT NULL,
    "PostedDate" timestamp with time zone,
    "PostedBy" character varying(100),
    "DaysInFiscalPeriod" integer,
    "IsShortFiscalPeriod" boolean NOT NULL
);


--
-- Name: CcaClassBalances_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CcaClassBalances" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CcaClassBalances_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CcaClasses; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CcaClasses" (
    "Id" integer NOT NULL,
    "ClassNumber" integer NOT NULL,
    "Description" character varying(200) NOT NULL,
    "Rate" numeric(7,4) NOT NULL,
    "IsDecliningBalance" boolean NOT NULL,
    "HalfYearRuleApplies" boolean NOT NULL,
    "IsAcceleratedInvestmentIncentive" boolean NOT NULL,
    "Notes" character varying(500),
    "Active" boolean NOT NULL
);


--
-- Name: CcaClasses_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CcaClasses" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CcaClasses_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CcaTransactions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CcaTransactions" (
    "Id" integer NOT NULL,
    "CcaClassId" integer NOT NULL,
    "AssetId" integer,
    "FiscalYear" integer NOT NULL,
    "TransactionType" integer NOT NULL,
    "TransactionDate" timestamp with time zone NOT NULL,
    "AvailableForUseDate" timestamp with time zone,
    "CapitalCost" numeric(18,2) NOT NULL,
    "Proceeds" numeric(18,2),
    "AdjustedCostBase" numeric(18,2),
    "NetAddition" numeric(18,2) NOT NULL,
    "SubjectToHalfYearRule" boolean NOT NULL,
    "IsAcceleratedIncentiveEligible" boolean NOT NULL,
    "Description" character varying(200),
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: CcaTransactions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CcaTransactions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CcaTransactions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CipCosts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CipCosts" (
    "Id" integer NOT NULL,
    "CipProjectId" integer NOT NULL,
    "Description" character varying(200) NOT NULL,
    "CostType" integer NOT NULL,
    "TransactionDate" timestamp with time zone NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Vendor" character varying(100),
    "InvoiceNumber" character varying(50),
    "PurchaseOrderNumber" character varying(50),
    "GlAccount" character varying(50),
    "IsCapitalizable" boolean NOT NULL,
    "Notes" character varying(500),
    "EnteredBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: CipCosts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CipCosts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CipCosts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CipProjects; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CipProjects" (
    "Id" integer NOT NULL,
    "ProjectNumber" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "Status" integer NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EstimatedCompletionDate" timestamp with time zone,
    "ActualCompletionDate" timestamp with time zone,
    "BudgetAmount" numeric(18,2) NOT NULL,
    "TotalCosts" numeric(18,2) NOT NULL,
    "CommittedCosts" numeric(18,2) NOT NULL,
    "ProjectManagerName" character varying(100),
    "ProjectManagerId" integer,
    "Location" character varying(100),
    "CostCenterId" integer,
    "Department" character varying(100),
    "DepartmentId" integer,
    "GlAccount" character varying(50),
    "GlAccountId" integer,
    "ConvertedAssetId" integer,
    "PlacedInServiceDate" timestamp with time zone,
    "Currency" character varying(3) DEFAULT 'CAD'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: CipProjects_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CipProjects" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CipProjects_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Companies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Companies" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "LegalName" character varying(100),
    "CompanyCode" character varying(20),
    "CompanyType" integer NOT NULL,
    "CompanyStructure" integer NOT NULL,
    "ParentCompanyId" integer,
    "Currency" character varying(3) DEFAULT 'USD'::character varying NOT NULL,
    "TaxId" character varying(50),
    "PeriodType" integer NOT NULL,
    "FiscalYearStartMonth" integer NOT NULL,
    "FiscalYearStartDay" integer NOT NULL,
    "IsShortYear" boolean NOT NULL,
    "ShortYearStart" timestamp with time zone,
    "ShortYearEnd" timestamp with time zone,
    "Address" character varying(200),
    "City" character varying(100),
    "StateProvince" character varying(50),
    "PostalCode" character varying(20),
    "Country" character varying(50),
    "ContactName" character varying(100),
    "ContactEmail" character varying(100),
    "ContactPhone" character varying(20),
    "DefaultDepMethod" integer NOT NULL,
    "DefaultConvention" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "LogoPath" character varying(255),
    "GstHstNumber" character varying(50),
    "PstNumber" character varying(50),
    "BusinessNumber" character varying(50),
    "DefaultLanguage" character varying(5) NOT NULL,
    "TimeZone" character varying(50) NOT NULL,
    "ApprovalThreshold" numeric,
    "RequireApprovalForDisposals" boolean NOT NULL,
    "RequireApprovalForTransfers" boolean NOT NULL,
    "FinancialMode" integer NOT NULL,
    "IntegrationType" integer NOT NULL,
    "EnableWorkOrders" boolean NOT NULL,
    "EnablePurchasing" boolean NOT NULL,
    "EnableInventory" boolean NOT NULL,
    "EnableAccountsPayable" boolean NOT NULL,
    "EnableVendors" boolean NOT NULL,
    "ERPConnectionString" character varying(500),
    "ERPCompanyCode" character varying(100),
    "TenantId" integer
);


--
-- Name: Companies_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Companies" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Companies_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: CostCenters; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."CostCenters" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(200),
    "Address" character varying(200),
    "City" character varying(100),
    "StateProvince" character varying(50),
    "PostalCode" character varying(20),
    "Country" character varying(50),
    "Type" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "ParentCostCenterId" integer,
    "CompanyId" integer,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: CostCenters_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."CostCenters" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."CostCenters_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Crafts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Crafts" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "DefaultHourlyRate" numeric NOT NULL,
    "RequiresCertification" boolean NOT NULL,
    "RequiredCertifications" character varying(500),
    "IsInternal" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: Crafts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Crafts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Crafts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Currencies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Currencies" (
    "Id" integer NOT NULL,
    "Code" character varying(3) NOT NULL,
    "Name" character varying(50) NOT NULL,
    "Symbol" character varying(5) NOT NULL,
    "DecimalPlaces" integer NOT NULL,
    "IsBaseCurrency" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: Currencies_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Currencies" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Currencies_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Departments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Departments" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(200),
    "Type" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "ManagerId" integer,
    "CostCenterId" integer,
    "CompanyId" integer,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Departments_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Departments" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Departments_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: DepreciationPolicies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DepreciationPolicies" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Method" integer NOT NULL,
    "Convention" integer NOT NULL,
    "DefaultUsefulLifeMonths" integer NOT NULL,
    "DefaultSalvagePercent" numeric NOT NULL,
    "DefaultSalvageAmount" numeric NOT NULL,
    "SalvageType" integer NOT NULL,
    "SwitchToStraightLine" boolean NOT NULL,
    "SwitchToSLInYear" integer,
    "AveragingMethod" integer NOT NULL,
    "DecliningBalanceRate" numeric,
    "ApplySection179" boolean NOT NULL,
    "DefaultSection179Percent" numeric,
    "ApplyBonusDepreciation" boolean NOT NULL,
    "DefaultBonusPercent" numeric,
    "MinimumBookValue" numeric NOT NULL,
    "AllowNegativeDepreciation" boolean NOT NULL,
    "Rounding" integer NOT NULL,
    "FirstYearProrate" integer NOT NULL,
    "LastYearProrate" integer NOT NULL,
    "Frequency" integer NOT NULL,
    "DepreciateInServiceMonth" boolean NOT NULL,
    "DepreciateInDisposalMonth" boolean NOT NULL,
    "CalculateToEndOfLife" boolean NOT NULL,
    "TrackUnitsOfProduction" boolean NOT NULL,
    "EstimatedTotalUnits" integer,
    "CcaClassId" integer,
    "MacrsRecoveryPeriodYears" integer,
    "MacrsPropertyType" integer,
    "MacrsUseADS" boolean NOT NULL,
    "ApplicableBookType" integer NOT NULL,
    "TaxJurisdiction" integer NOT NULL,
    "CompanyId" integer,
    "IsSystemPolicy" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "ModifiedAt" timestamp with time zone,
    "ModifiedBy" character varying(100)
);


--
-- Name: DepreciationPolicies_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."DepreciationPolicies" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."DepreciationPolicies_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: DepreciationRunDetails; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DepreciationRunDetails" (
    "Id" integer NOT NULL,
    "DepreciationRunId" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "BeginningBookValue" numeric(18,2) NOT NULL,
    "DepreciationAmount" numeric(18,2) NOT NULL,
    "EndingBookValue" numeric(18,2) NOT NULL,
    "YtdDepreciation" numeric(18,2) NOT NULL,
    "LtdDepreciation" numeric(18,2) NOT NULL,
    "MethodUsed" integer NOT NULL,
    "RemainingLifeMonths" integer NOT NULL,
    "Notes" character varying(200)
);


--
-- Name: DepreciationRunDetails_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."DepreciationRunDetails" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."DepreciationRunDetails_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: DepreciationRuns; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DepreciationRuns" (
    "Id" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "FiscalPeriodId" integer NOT NULL,
    "BookId" integer NOT NULL,
    "RunDate" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "AssetsProcessed" integer NOT NULL,
    "TotalDepreciation" numeric(18,2) NOT NULL,
    "PostedDate" timestamp with time zone,
    "PostedBy" character varying(100),
    "ReversedDate" timestamp with time zone,
    "ReversedBy" character varying(100),
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: DepreciationRuns_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."DepreciationRuns" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."DepreciationRuns_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ExchangeRates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ExchangeRates" (
    "Id" integer NOT NULL,
    "FromCurrency" character varying(3) NOT NULL,
    "ToCurrency" character varying(3) NOT NULL,
    "Rate" numeric NOT NULL,
    "EffectiveDate" timestamp with time zone NOT NULL,
    "ExpirationDate" timestamp with time zone,
    "Source" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: ExchangeRates_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ExchangeRates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ExchangeRates_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: FailureCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FailureCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "ParentId" integer,
    "Category" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: FailureCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."FailureCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."FailureCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: FiscalPeriods; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FiscalPeriods" (
    "Id" integer NOT NULL,
    "FiscalYearId" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "PeriodNumber" integer NOT NULL,
    "Name" character varying(50) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "IsAdjustmentPeriod" boolean NOT NULL,
    "DaysInPeriod" integer NOT NULL,
    "DepreciationCalculated" boolean NOT NULL,
    "DepreciationPosted" boolean NOT NULL,
    "ClosedAt" timestamp with time zone,
    "ClosedBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: FiscalPeriods_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."FiscalPeriods" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."FiscalPeriods_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: FiscalYears; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."FiscalYears" (
    "Id" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "Year" integer NOT NULL,
    "Name" character varying(50) NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "IsShortYear" boolean NOT NULL,
    "NumberOfPeriods" integer NOT NULL,
    "PeriodType" integer NOT NULL,
    "HasAdjustmentPeriod" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ClosedAt" timestamp with time zone,
    "ClosedBy" character varying(100)
);


--
-- Name: FiscalYears_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."FiscalYears" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."FiscalYears_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: GlAccounts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GlAccounts" (
    "Id" integer NOT NULL,
    "AccountNumber" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "AccountType" integer NOT NULL,
    "Category" integer NOT NULL,
    "SubCategory" integer NOT NULL,
    "NormalBalance" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "IsSystemAccount" boolean NOT NULL,
    "AllowManualEntry" boolean NOT NULL,
    "RequiresCostCenter" boolean NOT NULL,
    "RequiresDepartment" boolean NOT NULL,
    "RequiresAssetCategory" boolean NOT NULL,
    "ParentAccountId" integer,
    "SortOrder" integer NOT NULL,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: GlAccounts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."GlAccounts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."GlAccounts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: GoodsReceiptLines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GoodsReceiptLines" (
    "Id" integer NOT NULL,
    "GoodsReceiptId" integer NOT NULL,
    "PurchaseOrderLineId" integer NOT NULL,
    "LineNumber" integer NOT NULL,
    "QuantityReceived" numeric(18,4) NOT NULL,
    "QuantityAccepted" numeric(18,4) NOT NULL,
    "QuantityRejected" numeric(18,4) NOT NULL,
    "RejectionReason" character varying(500),
    "StorageLocation" character varying(100),
    "ReceivingLocationId" integer,
    "LotNumber" character varying(50),
    "SerialNumber" character varying(50),
    "Notes" character varying(500),
    "IsInvoiced" boolean NOT NULL
);


--
-- Name: GoodsReceiptLines_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."GoodsReceiptLines" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."GoodsReceiptLines_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: GoodsReceipts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."GoodsReceipts" (
    "Id" integer NOT NULL,
    "ReceiptNumber" character varying(20) NOT NULL,
    "PurchaseOrderId" integer NOT NULL,
    "Status" integer NOT NULL,
    "ReceiptDate" timestamp with time zone NOT NULL,
    "ReceivedBy" character varying(100),
    "ShippingCarrier" character varying(100),
    "TrackingNumber" character varying(100),
    "PackingSlipNumber" character varying(100),
    "ReceivingLocation" character varying(100),
    "Notes" character varying(500),
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: GoodsReceipts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."GoodsReceipts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."GoodsReceipts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: InboundEvents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."InboundEvents" (
    "Id" integer NOT NULL,
    "TenantId" integer,
    "IntegrationEndpointId" integer NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "EventType" character varying(100) NOT NULL,
    "ExternalEntityId" character varying(100),
    "CorrelationId" character varying(100),
    "IdempotencyKey" character varying(100),
    "RawBodyJson" text NOT NULL,
    "HeadersJson" text,
    "Status" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone,
    "LastError" character varying(1000),
    "ProcessedAt" timestamp with time zone
);


--
-- Name: InboundEvents_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."InboundEvents" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."InboundEvents_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: IntegrationEndpoints; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."IntegrationEndpoints" (
    "Id" integer NOT NULL,
    "TenantId" integer,
    "Name" character varying(100) NOT NULL,
    "IntegrationKey" character varying(50) NOT NULL,
    "Secret" character varying(64) NOT NULL,
    "IsActive" boolean NOT NULL,
    "AllowedEventTypesCsv" character varying(500) NOT NULL,
    "Description" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "LastEventAt" timestamp with time zone,
    "EventsReceivedCount" integer NOT NULL,
    "EventsProcessedCount" integer NOT NULL,
    "EventsFailedCount" integer NOT NULL
);


--
-- Name: IntegrationEndpoints_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."IntegrationEndpoints" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."IntegrationEndpoints_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: IntegrationMappings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."IntegrationMappings" (
    "Id" integer NOT NULL,
    "IntegrationEndpointId" integer NOT NULL,
    "MappingType" character varying(50) NOT NULL,
    "ExternalId" character varying(200) NOT NULL,
    "InternalId" integer,
    "InternalCode" character varying(200),
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: IntegrationMappings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."IntegrationMappings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."IntegrationMappings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: InventoryLists; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."InventoryLists" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Status" integer NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "StartedDate" timestamp with time zone,
    "CompletedDate" timestamp with time zone,
    "AssignedTo" character varying(100),
    "Location" character varying(100),
    "TotalAssets" integer NOT NULL,
    "ScannedAssets" integer NOT NULL,
    "MissingAssets" integer NOT NULL,
    "FoundAssets" integer NOT NULL
);


--
-- Name: InventoryLists_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."InventoryLists" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."InventoryLists_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: InventoryScans; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."InventoryScans" (
    "Id" integer NOT NULL,
    "InventoryListId" integer NOT NULL,
    "AssetId" integer,
    "ScannedBarcode" character varying(100),
    "ScanDate" timestamp with time zone NOT NULL,
    "ScannedBy" character varying(100),
    "Location" character varying(100),
    "Result" integer NOT NULL,
    "Condition" integer NOT NULL,
    "Notes" character varying(500),
    "PhotoPath" character varying(500)
);


--
-- Name: InventoryScans_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."InventoryScans" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."InventoryScans_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: InvoicePayments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."InvoicePayments" (
    "Id" integer NOT NULL,
    "VendorInvoiceId" integer NOT NULL,
    "PaymentDate" timestamp with time zone NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentMethod" character varying(50),
    "ReferenceNumber" character varying(50),
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: InvoicePayments_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."InvoicePayments" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."InvoicePayments_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemAlternates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemAlternates" (
    "Id" integer NOT NULL,
    "TenantId" integer DEFAULT 0 NOT NULL,
    "ItemId" integer NOT NULL,
    "AlternateItemId" integer NOT NULL,
    "AlternateType" integer NOT NULL,
    "Rank" integer NOT NULL,
    "Reason" character varying(500),
    "IsApproved" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CreatedByUserId" integer
);


--
-- Name: ItemAlternates_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemAlternates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemAlternates_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemApprovedVendors; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemApprovedVendors" (
    "Id" integer NOT NULL,
    "TenantId" integer DEFAULT 0 NOT NULL,
    "CompanyId" integer,
    "SiteId" integer,
    "ItemId" integer NOT NULL,
    "VendorId" integer NOT NULL,
    "IsPreferred" boolean NOT NULL,
    "ApprovalStatus" integer NOT NULL,
    "Notes" character varying(500),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CreatedByUserId" integer
);


--
-- Name: ItemApprovedVendors_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemApprovedVendors" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemApprovedVendors_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemCategories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemCategories" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "ParentCategoryId" integer,
    "DefaultGlAccountId" integer,
    "ExpenseGlAccountId" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: ItemCategories_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemCategories" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemCategories_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemCompanyStockings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemCompanyStockings" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "IsStocked" boolean NOT NULL,
    "IsPurchasable" boolean NOT NULL,
    "IsCriticalSpare" boolean NOT NULL,
    "MinQuantity" numeric(18,4) NOT NULL,
    "MaxQuantity" numeric(18,4) NOT NULL,
    "ReorderPoint" numeric(18,4) NOT NULL,
    "ReorderQuantity" numeric(18,4) NOT NULL,
    "SafetyStock" numeric(18,4) NOT NULL,
    "LeadTimeDays" integer NOT NULL,
    "PreferredVendorId" integer,
    "ReorderMethod" integer NOT NULL,
    "AutoReorderEnabled" boolean NOT NULL,
    "ABCClass" integer NOT NULL,
    "DefaultWarehouse" character varying(20),
    "DefaultAisle" character varying(20),
    "DefaultRack" character varying(20),
    "DefaultShelf" character varying(20),
    "DefaultBin" character varying(20),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedBy" character varying(50),
    "UpdatedBy" character varying(50)
);


--
-- Name: ItemCompanyStockings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemCompanyStockings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemCompanyStockings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemImages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemImages" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "FileName" character varying(200) NOT NULL,
    "FilePath" character varying(500) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "FileSize" bigint NOT NULL,
    "AltText" character varying(200),
    "Caption" character varying(500),
    "IsPrimary" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "ExternalUrl" character varying(500),
    "IsExternal" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(50)
);


--
-- Name: ItemImages_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemImages" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemImages_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemInventories2; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemInventories2" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "LocationId" integer,
    "Warehouse" character varying(50),
    "Bin" character varying(20),
    "QuantityOnHand" numeric(18,4) NOT NULL,
    "QuantityReserved" numeric(18,4) NOT NULL,
    "QuantityOnOrder" numeric(18,4) NOT NULL,
    "LotNumber" character varying(50),
    "SerialNumber" character varying(50),
    "ExpirationDate" timestamp with time zone,
    "LastCountDate" timestamp with time zone,
    "LastReceiptDate" timestamp with time zone,
    "LastIssueDate" timestamp with time zone,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: ItemInventories2_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemInventories2" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemInventories2_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemManufacturerParts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemManufacturerParts" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "ManufacturerId" integer NOT NULL,
    "MfrPartNumber" character varying(100) NOT NULL,
    "Description" character varying(500),
    "LifecycleStatus" character varying(50),
    "DatasheetUrl" character varying(500),
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    "CreatedBy" character varying(100)
);


--
-- Name: ItemManufacturerParts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemManufacturerParts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemManufacturerParts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemRevisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemRevisions" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "RevisionCode" character varying(10) NOT NULL,
    "ChangeReason" character varying(500),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ReleasedAtUtc" timestamp with time zone,
    "ObsoletedAtUtc" timestamp with time zone,
    "ApprovedAtUtc" timestamp with time zone,
    "ApprovedByUserId" character varying(100),
    "CreatedByUserId" character varying(100),
    "Description" character varying(1000),
    "EffectiveFromUtc" timestamp with time zone,
    "EffectiveToUtc" timestamp with time zone,
    "Name" character varying(200) DEFAULT ''::character varying NOT NULL,
    "Status" integer DEFAULT 0 NOT NULL,
    "SupersedesItemRevisionId" integer
);


--
-- Name: ItemRevisions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemRevisions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemRevisions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemSupersessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemSupersessions" (
    "Id" integer NOT NULL,
    "TenantId" integer DEFAULT 0 NOT NULL,
    "OldItemId" integer NOT NULL,
    "NewItemId" integer NOT NULL,
    "EffectiveFromUtc" timestamp with time zone,
    "Reason" character varying(500),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CreatedByUserId" integer
);


--
-- Name: ItemSupersessions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemSupersessions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemSupersessions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemTransactions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemTransactions" (
    "Id" integer NOT NULL,
    "TransactionNumber" character varying(20) NOT NULL,
    "ItemId" integer NOT NULL,
    "Type" integer NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "UnitCost" numeric(18,4) NOT NULL,
    "FromLocationId" integer,
    "ToLocationId" integer,
    "FromBin" character varying(50),
    "ToBin" character varying(50),
    "LotNumber" character varying(50),
    "SerialNumber" character varying(50),
    "ReferenceType" character varying(50),
    "ReferenceNumber" character varying(50),
    "WorkOrderId" integer,
    "PurchaseOrderId" integer,
    "Notes" character varying(500),
    "TransactedBy" character varying(50) NOT NULL,
    "TransactionDate" timestamp with time zone NOT NULL,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: ItemTransactions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemTransactions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemTransactions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ItemVendors; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ItemVendors" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "VendorId" integer NOT NULL,
    "VendorPartNumber" character varying(50),
    "UnitPrice" numeric(18,4) NOT NULL,
    "MinOrderQty" numeric(18,4) NOT NULL,
    "LeadTimeDays" integer NOT NULL,
    "IsPreferred" boolean NOT NULL,
    "LastOrderDate" timestamp with time zone,
    "ProductPageUrl" character varying(500),
    "OrderUrl" character varying(500),
    "CatalogPageUrl" character varying(500),
    "PriceBreakQty1" numeric(18,4),
    "PriceBreak1" numeric(18,4),
    "PriceBreakQty2" numeric(18,4),
    "PriceBreak2" numeric(18,4),
    "PriceBreakQty3" numeric(18,4),
    "PriceBreak3" numeric(18,4),
    "ContractNumber" character varying(50),
    "ContractPrice" numeric(18,4),
    "ContractStartDate" timestamp with time zone,
    "ContractEndDate" timestamp with time zone,
    "VendorStockAvailable" boolean,
    "LastStockCheckDate" timestamp with time zone,
    "Notes" character varying(200),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: ItemVendors_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ItemVendors" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ItemVendors_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Items; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Items" (
    "Id" integer NOT NULL,
    "PartNumber" character varying(50) NOT NULL,
    "Description" character varying(200) NOT NULL,
    "ExtendedDescription" character varying(500),
    "Revision" character varying(10),
    "RequireRevisionControl" boolean NOT NULL,
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "CategoryId" integer,
    "UOM" integer NOT NULL,
    "StockUOM" character varying(20) NOT NULL,
    "PurchaseUOM" character varying(20) NOT NULL,
    "PurchaseConversion" numeric(18,4) NOT NULL,
    "CostMethod" integer NOT NULL,
    "StandardCost" numeric(18,4) NOT NULL,
    "AverageCost" numeric(18,4) NOT NULL,
    "LastPurchaseCost" numeric(18,4) NOT NULL,
    "ListPrice" numeric(18,4),
    "TrackingType" integer NOT NULL,
    "MinQuantity" numeric(18,4) NOT NULL,
    "MaxQuantity" numeric(18,4) NOT NULL,
    "ReorderPoint" numeric(18,4) NOT NULL,
    "ReorderQuantity" numeric(18,4) NOT NULL,
    "SafetyStock" numeric(18,4) NOT NULL,
    "LeadTimeDays" integer NOT NULL,
    "DefaultLocation" character varying(50),
    "Warehouse" character varying(20),
    "Aisle" character varying(20),
    "Rack" character varying(20),
    "Shelf" character varying(20),
    "Bin" character varying(20),
    "PrimaryVendorId" integer,
    "VendorPartNumber" character varying(50),
    "ManufacturerPartNumber" character varying(50),
    "ManufacturerId" integer,
    "IsStocked" boolean NOT NULL,
    "IsPurchasable" boolean NOT NULL,
    "IsCriticalSpare" boolean NOT NULL,
    "IsTaxable" boolean NOT NULL,
    "IsHazmat" boolean NOT NULL,
    "HazmatClass" character varying(50),
    "ShelfLifeDays" integer,
    "Weight" numeric(18,4),
    "Dimensions" character varying(100),
    "Notes" character varying(500),
    "ImageUrl" character varying(200),
    "SpecUrl" character varying(200),
    "BarcodeType" integer NOT NULL,
    "Barcode" character varying(100),
    "AlternateBarcode" character varying(100),
    "ABCClass" integer NOT NULL,
    "ReorderMethod" integer NOT NULL,
    "AutoReorderEnabled" boolean NOT NULL,
    "EOQ" numeric(18,4),
    "AnnualUsage" numeric(18,4) NOT NULL,
    "AverageDailyUsage" numeric(18,4) NOT NULL,
    "CarryingCostPercent" numeric(18,4),
    "OrderingCost" numeric(18,4),
    "AlternatePartNumbers" character varying(200),
    "SupersedesPartNumber" character varying(100),
    "SupersededByPartNumber" character varying(100),
    "WarrantyMonths" integer,
    "WarrantyTerms" character varying(200),
    "CommodityCode" character varying(20),
    "UNSPSCCode" character varying(20),
    "DefaultBuyerId" integer,
    "DefaultBuyerName" character varying(100),
    "Length" numeric(18,4),
    "Width" numeric(18,4),
    "Height" numeric(18,4),
    "DimensionUOM" character varying(20),
    "StorageRequirements" character varying(100),
    "MinStorageTemp" integer,
    "MaxStorageTemp" integer,
    "Certifications" character varying(200),
    "IsFDARegulated" boolean NOT NULL,
    "IsOSHACompliance" boolean NOT NULL,
    "CountryOfOrigin" character varying(50),
    "HTSCode" character varying(20),
    "Source" integer NOT NULL,
    "ExternalId" character varying(50),
    "CompanyId" integer,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedBy" character varying(50),
    "UpdatedBy" character varying(50),
    "CurrentReleasedRevisionId" integer,
    "ImagePath" character varying(500),
    "ExternalImageUrl" character varying(500),
    "OrderMultiple" numeric(18,4),
    "LastPrice" numeric(18,4),
    "CurrencyCode" character varying(10),
    "PriceEffectiveDate" timestamp with time zone,
    "ContractFlag" boolean DEFAULT false NOT NULL,
    "ContractRef" character varying(50),
    "StockPolicy" integer DEFAULT 0 NOT NULL,
    "MinOrderQty" integer,
    "PackQty" integer
);


--
-- Name: Items_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Items" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Items_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: JournalEntries; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."JournalEntries" (
    "Id" integer NOT NULL,
    "BookId" integer,
    "Period" integer NOT NULL,
    "Batch" character varying(30) NOT NULL,
    "Reference" character varying(50),
    "Source" character varying(30),
    "PostingDate" timestamp with time zone NOT NULL,
    "CreatedUtc" timestamp with time zone NOT NULL,
    "Description" character varying(200)
);


--
-- Name: JournalEntries_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."JournalEntries" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."JournalEntries_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: JournalLines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."JournalLines" (
    "Id" integer NOT NULL,
    "JournalEntryId" integer NOT NULL,
    "LineNo" integer NOT NULL,
    "Account" character varying(50) NOT NULL,
    "Description" character varying(200),
    "Debit" numeric(18,2) NOT NULL,
    "Credit" numeric(18,2) NOT NULL
);


--
-- Name: JournalLines_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."JournalLines" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."JournalLines_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: KitItems; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."KitItems" (
    "Id" integer NOT NULL,
    "KitId" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "Notes" character varying(200),
    "Sequence" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: KitItems_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."KitItems" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."KitItems_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Kits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Kits" (
    "Id" integer NOT NULL,
    "KitNumber" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(500),
    "CategoryId" integer,
    "TotalCost" numeric(18,2) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: Kits_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Kits" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Kits_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: LaborRates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LaborRates" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "CraftId" integer,
    "SkillId" integer,
    "StandardRate" numeric NOT NULL,
    "OvertimeRate" numeric NOT NULL,
    "DoubleTimeRate" numeric NOT NULL,
    "EffectiveDate" timestamp with time zone NOT NULL,
    "ExpirationDate" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: LaborRates_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."LaborRates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."LaborRates_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: LaborTypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LaborTypes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Category" integer NOT NULL,
    "MultiplierRate" numeric NOT NULL,
    "IsBillable" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: LaborTypes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."LaborTypes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."LaborTypes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: LessonsLearned; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."LessonsLearned" (
    "Id" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "SiteId" integer,
    "AssetCategoryId" integer,
    "Tags" character varying(500),
    "Text" character varying(4000) NOT NULL,
    "Title" character varying(500),
    "SourceWorkOrderId" integer,
    "FailureCode" character varying(100),
    "CreatedBy" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: LessonsLearned_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."LessonsLearned" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."LessonsLearned_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Locations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Locations" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(200),
    "Type" integer NOT NULL,
    "Building" character varying(100),
    "Floor" character varying(50),
    "Bay" character varying(50),
    "Station" character varying(50),
    "IsActive" boolean NOT NULL,
    "ParentLocationId" integer,
    "CostCenterId" integer,
    "CompanyId" integer,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Aisle" character varying(50),
    "AllowsAssetInstallation" boolean DEFAULT false NOT NULL,
    "Bin" character varying(50),
    "CreatedBy" text,
    "Criticality" integer DEFAULT 0 NOT NULL,
    "CurrentAssetCount" integer DEFAULT 0 NOT NULL,
    "HeightFeet" numeric,
    "HierarchyLevel" integer DEFAULT 0 NOT NULL,
    "HierarchyPath" character varying(500),
    "IsOperational" boolean DEFAULT false NOT NULL,
    "Latitude" numeric,
    "Longitude" numeric,
    "MaxAssetCapacity" integer,
    "ModifiedAt" timestamp with time zone,
    "ModifiedBy" text,
    "Rack" character varying(50),
    "SafetyRequirements" character varying(100),
    "SafetyZone" integer DEFAULT 0 NOT NULL,
    "Shelf" character varying(50),
    "SiteId" integer,
    "SquareFootage" integer
);


--
-- Name: Locations_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Locations" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Locations_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: MaintenanceEvents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MaintenanceEvents" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "Type" integer NOT NULL,
    "Description" character varying(200) NOT NULL,
    "ScheduledDate" timestamp with time zone NOT NULL,
    "CompletedDate" timestamp with time zone,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "EstimatedCost" numeric(18,2) NOT NULL,
    "ActualCost" numeric(18,2),
    "LaborCost" numeric(18,2),
    "PartsCost" numeric(18,2),
    "MaterialsCost" numeric(18,2),
    "OutsideVendorCost" numeric(18,2),
    "Vendor" character varying(100),
    "TechnicianName" character varying(100),
    "TechnicianId" integer,
    "WorkOrderNumber" character varying(50),
    "PurchaseOrderNumber" character varying(50),
    "DowntimeHours" numeric,
    "LaborHours" numeric(18,2),
    "OvertimeHours" numeric(18,2),
    "ApprovalStatus" integer NOT NULL,
    "ApprovedById" integer,
    "ApprovedAt" timestamp with time zone,
    "RequestedById" integer,
    "RequestedAt" timestamp with time zone,
    "FailureCode" character varying(500),
    "RootCause" character varying(500),
    "CorrectiveAction" character varying(500),
    "Notes" character varying(1000),
    "Resolution" character varying(1000),
    "RecurrenceIntervalDays" integer,
    "NextScheduledDate" timestamp with time zone,
    "CreatedBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "CompletedBy" character varying(100),
    "CustomField1" text,
    "CustomField2" text,
    "CustomField3" text,
    "CustomField4" text,
    "CustomField5" text,
    "CustomField6" text,
    "CustomField7" text,
    "CustomField8" text,
    "CustomField9" text,
    "CustomField10" text,
    "ClosedAt" timestamp with time zone,
    "ClosedBy" character varying(100),
    "LessonsLearned" character varying(2000),
    "ResolutionSummary" character varying(2000)
);


--
-- Name: MaintenanceEvents_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."MaintenanceEvents" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."MaintenanceEvents_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: MaintenanceSchedules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MaintenanceSchedules" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Type" integer NOT NULL,
    "Recurrence" integer NOT NULL,
    "IntervalValue" integer NOT NULL,
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone,
    "LastGeneratedDate" timestamp with time zone,
    "NextDueDate" timestamp with time zone,
    "EstimatedCost" numeric(18,2) NOT NULL,
    "AssignedVendor" character varying(100),
    "IsActive" boolean NOT NULL,
    "LeadTimeDays" integer NOT NULL
);


--
-- Name: MaintenanceSchedules_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."MaintenanceSchedules" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."MaintenanceSchedules_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: MaintenanceTypeCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MaintenanceTypeCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "IsPreventive" boolean NOT NULL,
    "IsCorrective" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: MaintenanceTypeCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."MaintenanceTypeCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."MaintenanceTypeCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Manufacturers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Manufacturers" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Website" character varying(200),
    "Country" character varying(100),
    "ContactName" character varying(100),
    "ContactEmail" character varying(100),
    "ContactPhone" character varying(30),
    "Address" character varying(500),
    "Notes" character varying(500),
    "Active" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Code" character varying(20) DEFAULT ''::character varying,
    "TenantId" integer
);


--
-- Name: Manufacturers_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Manufacturers" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Manufacturers_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: MeterReadings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."MeterReadings" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "MeterType" integer NOT NULL,
    "MeterName" character varying(50),
    "Reading" numeric(18,2) NOT NULL,
    "PreviousReading" numeric(18,2),
    "ReadingDate" timestamp with time zone NOT NULL,
    "RecordedBy" character varying(50),
    "Source" character varying(50),
    "Notes" character varying(500),
    "IsEstimated" boolean NOT NULL,
    "IsRollover" boolean NOT NULL,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: MeterReadings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."MeterReadings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."MeterReadings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: NumberingSequences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."NumberingSequences" (
    "Id" integer NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Prefix" character varying(20) NOT NULL,
    "Suffix" character varying(20) NOT NULL,
    "NextNumber" integer NOT NULL,
    "NumberLength" integer NOT NULL,
    "PadWithZeros" boolean NOT NULL,
    "IncludeYear" boolean NOT NULL,
    "IncludeMonth" boolean NOT NULL,
    "ResetYearly" boolean NOT NULL,
    "ResetMonthly" boolean NOT NULL,
    "LastResetYear" integer,
    "LastResetMonth" integer,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: NumberingSequences_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."NumberingSequences" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."NumberingSequences_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: OutboxEvents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."OutboxEvents" (
    "Id" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "SiteId" integer,
    "EventType" character varying(100) NOT NULL,
    "EntityType" character varying(100) NOT NULL,
    "EntityId" character varying(50) NOT NULL,
    "PayloadJson" text NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone,
    "LastError" character varying(1000),
    "SentAt" timestamp with time zone,
    "CorrelationId" character varying(100),
    "TenantId" integer
);


--
-- Name: OutboxEvents_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."OutboxEvents" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."OutboxEvents_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PMOccurrences; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMOccurrences" (
    "Id" integer NOT NULL,
    "TenantId" integer,
    "CompanyId" integer,
    "SiteId" integer,
    "PMScheduleId" integer NOT NULL,
    "PMTemplateId" integer NOT NULL,
    "DueDateUtc" timestamp with time zone NOT NULL,
    "WorkOrderId" integer,
    "Status" integer DEFAULT 0 NOT NULL,
    "ErrorMessage" character varying(500),
    "GeneratedBy" character varying(50),
    "GeneratedAt" timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: PMOccurrences_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."PMOccurrences_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: PMOccurrences_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."PMOccurrences_Id_seq" OWNED BY public."PMOccurrences"."Id";


--
-- Name: PMSchedules; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMSchedules" (
    "Id" integer NOT NULL,
    "TenantId" integer,
    "CompanyId" integer,
    "SiteId" integer,
    "PMTemplateId" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Active" boolean DEFAULT true NOT NULL,
    "StartDateUtc" timestamp with time zone NOT NULL,
    "TimeZoneId" character varying(100),
    "CadenceType" integer DEFAULT 0 NOT NULL,
    "IntervalDays" integer DEFAULT 30,
    "DaysOfWeekMask" integer,
    "DayOfMonth" integer,
    "NextDueDateUtc" timestamp with time zone,
    "LeadDays" integer DEFAULT 0 NOT NULL,
    "Notes" character varying(1000),
    "CreatedBy" character varying(50),
    "CreatedAt" timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "UpdatedBy" character varying(50),
    "UpdatedAt" timestamp with time zone
);


--
-- Name: PMSchedules_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."PMSchedules_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: PMSchedules_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."PMSchedules_Id_seq" OWNED BY public."PMSchedules"."Id";


--
-- Name: PMTemplateAssets; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMTemplateAssets" (
    "Id" integer NOT NULL,
    "PMTemplateId" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "OverrideCalendarInterval" integer,
    "OverrideCalendarValue" integer,
    "OverrideMeterInterval" numeric(18,2),
    "LastCompletedDate" timestamp with time zone,
    "LastMeterReading" numeric(18,2),
    "NextDueDate" timestamp with time zone,
    "NextDueMeter" numeric(18,2),
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: PMTemplateAssets_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PMTemplateAssets" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PMTemplateAssets_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PMTemplateItems; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMTemplateItems" (
    "Id" integer NOT NULL,
    "PMTemplateId" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "IsRequired" boolean NOT NULL,
    "Notes" character varying(500),
    "Sequence" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: PMTemplateItems_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PMTemplateItems" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PMTemplateItems_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PMTemplateRevisionOperations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMTemplateRevisionOperations" (
    "Id" integer NOT NULL,
    "PMTemplateRevisionId" integer NOT NULL,
    "Sequence" integer NOT NULL,
    "Description" character varying(500) NOT NULL,
    "EstimatedHours" numeric(8,2),
    "Craft" character varying(50),
    "Notes" character varying(500),
    "IsRequired" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL
);


--
-- Name: PMTemplateRevisionOperations_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PMTemplateRevisionOperations" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PMTemplateRevisionOperations_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PMTemplateRevisions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMTemplateRevisions" (
    "Id" integer NOT NULL,
    "PMTemplateId" integer NOT NULL,
    "RevisionCode" character varying(10) NOT NULL,
    "Status" integer NOT NULL,
    "EffectiveFromUtc" timestamp with time zone,
    "EffectiveToUtc" timestamp with time zone,
    "SupersedesRevisionId" integer,
    "ChangeReason" character varying(500),
    "ApprovedByUserId" character varying(100),
    "ApprovedAtUtc" timestamp with time zone,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "Type" integer NOT NULL,
    "Priority" integer NOT NULL,
    "TriggerType" integer NOT NULL,
    "CalendarInterval" integer NOT NULL,
    "CalendarIntervalValue" integer NOT NULL,
    "MeterType" integer,
    "MeterInterval" numeric(18,2),
    "EstimatedHours" numeric(8,2) NOT NULL,
    "EstimatedLaborCost" numeric(18,2),
    "EstimatedPartsCost" numeric(18,2),
    "EstimatedTotalCost" numeric(18,2),
    "RequiresShutdown" boolean NOT NULL,
    "RequiresLOTO" boolean NOT NULL,
    "SkillLevel" character varying(50),
    "Craft" character varying(50),
    "Procedure" text,
    "SafetyInstructions" text,
    "ToolsRequired" text,
    "ReferenceDocuments" text,
    "AssetCategoryId" integer,
    "ManufacturerId" integer,
    "ModelPattern" character varying(100),
    "IsOEMRecommended" boolean NOT NULL,
    "OEMReference" character varying(100),
    "IsRegulatoryRequired" boolean NOT NULL,
    "RegulatoryReference" character varying(100),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CreatedByUserId" character varying(100),
    "ReleasedAtUtc" timestamp with time zone,
    "ObsoletedAtUtc" timestamp with time zone
);


--
-- Name: PMTemplateRevisions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PMTemplateRevisions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PMTemplateRevisions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PMTemplates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PMTemplates" (
    "Id" integer NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "Type" integer NOT NULL,
    "Priority" integer NOT NULL,
    "TriggerType" integer NOT NULL,
    "CalendarInterval" integer NOT NULL,
    "CalendarIntervalValue" integer NOT NULL,
    "MeterType" integer,
    "MeterInterval" numeric(18,2),
    "EstimatedHours" numeric(8,2) NOT NULL,
    "EstimatedLaborCost" numeric(18,2),
    "EstimatedPartsCost" numeric(18,2),
    "EstimatedTotalCost" numeric(18,2),
    "RequiresShutdown" boolean NOT NULL,
    "RequiresLOTO" boolean NOT NULL,
    "SkillLevel" character varying(50),
    "Craft" character varying(50),
    "Procedure" text,
    "SafetyInstructions" text,
    "ToolsRequired" text,
    "ReferenceDocuments" text,
    "AssetCategoryId" integer,
    "ManufacturerId" integer,
    "ModelPattern" character varying(100),
    "IsOEMRecommended" boolean NOT NULL,
    "OEMReference" character varying(100),
    "IsRegulatoryRequired" boolean NOT NULL,
    "RegulatoryReference" character varying(100),
    "CompanyId" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedBy" character varying(50),
    "UpdatedBy" character varying(50),
    "CurrentReleasedRevisionId" integer
);


--
-- Name: PMTemplates_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PMTemplates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PMTemplates_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PartialDisposals; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PartialDisposals" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "DisposalDate" timestamp with time zone NOT NULL,
    "PercentageDisposed" numeric(5,4) NOT NULL,
    "OriginalCostDisposed" numeric(18,2) NOT NULL,
    "AccumulatedDepreciationDisposed" numeric(18,2) NOT NULL,
    "BookValueDisposed" numeric(18,2) NOT NULL,
    "SaleProceeds" numeric(18,2) NOT NULL,
    "GainLoss" numeric(18,2) NOT NULL,
    "Reason" integer NOT NULL,
    "Notes" character varying(500),
    "Buyer" character varying(100),
    "ReferenceNumber" character varying(50),
    "JournalEntryId" integer,
    "ProcessedBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: PartialDisposals_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PartialDisposals" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PartialDisposals_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PaymentTerms; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PaymentTerms" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "DueDays" integer NOT NULL,
    "DiscountPercent" numeric NOT NULL,
    "DiscountDays" integer NOT NULL,
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: PaymentTerms_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PaymentTerms" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PaymentTerms_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PeriodLocks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PeriodLocks" (
    "Id" integer NOT NULL,
    "Period" integer NOT NULL,
    "LockedAt" timestamp with time zone NOT NULL,
    "LockedBy" character varying(100),
    "Reason" character varying(500),
    "IsLocked" boolean NOT NULL
);


--
-- Name: PeriodLocks_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PeriodLocks" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PeriodLocks_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PolicyCategoryDefaults; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PolicyCategoryDefaults" (
    "Id" integer NOT NULL,
    "DepreciationPolicyId" integer NOT NULL,
    "AssetCategoryId" integer NOT NULL,
    "BookId" integer NOT NULL,
    "CompanyId" integer,
    "Priority" integer NOT NULL,
    "IsActive" boolean NOT NULL
);


--
-- Name: PolicyCategoryDefaults_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PolicyCategoryDefaults" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PolicyCategoryDefaults_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PriorityLevels; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PriorityLevels" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(50) NOT NULL,
    "Description" character varying(255),
    "Level" integer NOT NULL,
    "ResponseTimeHours" integer NOT NULL,
    "TargetCompletionHours" integer NOT NULL,
    "Color" character varying(20) NOT NULL,
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: PriorityLevels_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PriorityLevels" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PriorityLevels_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ProblemCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ProblemCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Category" integer NOT NULL,
    "DefaultSeverity" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: ProblemCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ProblemCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ProblemCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ProjectManagers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ProjectManagers" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Email" character varying(100),
    "Phone" character varying(30),
    "Department" character varying(100),
    "DepartmentId" integer,
    "CostCenterId" integer,
    "Title" character varying(100),
    "Notes" character varying(500),
    "Active" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: ProjectManagers_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ProjectManagers" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ProjectManagers_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PurchaseOrderLines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PurchaseOrderLines" (
    "Id" integer NOT NULL,
    "PurchaseOrderId" integer NOT NULL,
    "LineNumber" integer NOT NULL,
    "IsNonItemMaster" boolean NOT NULL,
    "ItemId" integer,
    "Description" character varying(200) NOT NULL,
    "PartNumber" character varying(50),
    "ManufacturerPartNumber" character varying(50),
    "VendorPartNumber" character varying(50),
    "Revision" character varying(10),
    "ExpenseCategoryId" integer,
    "UOM" character varying(20) NOT NULL,
    "QuantityOrdered" numeric(18,4) NOT NULL,
    "QuantityReceived" numeric(18,4) NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL,
    "GlAccountId" integer,
    "CostCenterId" integer,
    "AssetId" integer,
    "ShipToLocationId" integer,
    "IsReceived" boolean NOT NULL,
    "IsClosed" boolean NOT NULL,
    "Notes" character varying(500),
    "RequiredDate" timestamp with time zone
);


--
-- Name: PurchaseOrderLines_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PurchaseOrderLines" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PurchaseOrderLines_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PurchaseOrderReleases; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PurchaseOrderReleases" (
    "Id" integer NOT NULL,
    "PurchaseOrderLineId" integer NOT NULL,
    "ReleaseNumber" integer NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "QuantityReceived" numeric(18,4) NOT NULL,
    "ShipToLocationId" integer,
    "DueDate" timestamp with time zone,
    "Status" integer NOT NULL,
    "Notes" character varying(200),
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: PurchaseOrderReleases_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PurchaseOrderReleases" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PurchaseOrderReleases_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PurchaseOrders; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PurchaseOrders" (
    "Id" integer NOT NULL,
    "PONumber" character varying(20) NOT NULL,
    "POType" integer NOT NULL,
    "Status" integer NOT NULL,
    "VendorId" integer NOT NULL,
    "OrderDate" timestamp with time zone NOT NULL,
    "RequiredDate" timestamp with time zone,
    "PromiseDate" timestamp with time zone,
    "Currency" character varying(3) NOT NULL,
    "Subtotal" numeric(18,2) NOT NULL,
    "TaxAmount" numeric(18,2) NOT NULL,
    "ShippingAmount" numeric(18,2) NOT NULL,
    "Total" numeric(18,2) NOT NULL,
    "Notes" character varying(500),
    "InternalNotes" character varying(500),
    "WorkOrderId" integer,
    "CipProjectId" integer,
    "RequestedById" integer,
    "ApprovedById" integer,
    "ApprovedAt" timestamp with time zone,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "BillToAddress" character varying(200),
    "BillToSiteId" integer,
    "DefaultShipToLocationId" integer,
    "ShipToAddress" character varying(200),
    "ShipToSiteId" integer
);


--
-- Name: PurchaseOrders_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PurchaseOrders" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PurchaseOrders_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PurchaseRequisitionLines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PurchaseRequisitionLines" (
    "Id" integer NOT NULL,
    "RequisitionId" integer NOT NULL,
    "LineNumber" integer NOT NULL,
    "IsNonItemMaster" boolean NOT NULL,
    "ItemId" integer,
    "PartNumber" character varying(50),
    "Description" character varying(200),
    "ManufacturerPartNumber" character varying(50),
    "VendorPartNumber" character varying(50),
    "Revision" character varying(10),
    "ExpenseCategoryId" integer,
    "Quantity" numeric(18,4) NOT NULL,
    "UOM" character varying(20) NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "SuggestedVendorId" integer,
    "CurrentStock" numeric(18,4),
    "ReorderPoint" numeric(18,4),
    "RequiredDate" timestamp with time zone,
    "DeliverTo" character varying(100),
    "Notes" character varying(200),
    "GlAccountId" integer,
    "CostCenterId" integer,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: PurchaseRequisitionLines_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PurchaseRequisitionLines" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PurchaseRequisitionLines_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: PurchaseRequisitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."PurchaseRequisitions" (
    "Id" integer NOT NULL,
    "RequisitionNumber" character varying(20) NOT NULL,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "Source" integer NOT NULL,
    "RequisitionDate" timestamp with time zone NOT NULL,
    "RequiredDate" timestamp with time zone,
    "Requestor" character varying(100),
    "RequestorId" integer,
    "Department" character varying(100),
    "DepartmentId" integer,
    "Buyer" character varying(100),
    "BuyerId" integer,
    "SuggestedVendorId" integer,
    "TotalAmount" numeric(18,2) NOT NULL,
    "Justification" character varying(500),
    "Notes" character varying(500),
    "DeliverTo" character varying(100),
    "DeliveryAddress" character varying(200),
    "WorkOrderReference" character varying(50),
    "WorkOrderId" integer,
    "PMScheduleReference" character varying(50),
    "ApprovedBy" character varying(100),
    "ApprovedDate" timestamp with time zone,
    "RejectionReason" character varying(500),
    "ConvertedToPOId" integer,
    "ConvertedDate" timestamp with time zone,
    "ExportedToERP" boolean NOT NULL,
    "ERPExportDate" timestamp with time zone,
    "ERPReference" character varying(50),
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedBy" character varying(50),
    "UpdatedBy" character varying(50),
    "DeliverToLocationId" integer,
    "DeliverToSiteId" integer
);


--
-- Name: PurchaseRequisitions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."PurchaseRequisitions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."PurchaseRequisitions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ReorderAlerts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ReorderAlerts" (
    "Id" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "AlertType" integer NOT NULL,
    "CurrentStock" numeric(18,4) NOT NULL,
    "ReorderPoint" numeric(18,4) NOT NULL,
    "SafetyStock" numeric(18,4) NOT NULL,
    "SuggestedQuantity" numeric(18,4) NOT NULL,
    "IsAcknowledged" boolean NOT NULL,
    "AcknowledgedBy" character varying(100),
    "AcknowledgedDate" timestamp with time zone,
    "RequisitionId" integer,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: ReorderAlerts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ReorderAlerts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ReorderAlerts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Section179Limits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Section179Limits" (
    "Id" integer NOT NULL,
    "TaxYear" integer NOT NULL,
    "MaxDeduction" numeric(18,2) NOT NULL,
    "PhaseoutThreshold" numeric(18,2) NOT NULL,
    "SuvLimit" numeric(18,2) NOT NULL,
    "AutoDepreciationCap" numeric(18,2) NOT NULL,
    "TruckDepreciationCap" numeric(18,2) NOT NULL
);


--
-- Name: Section179Limits_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Section179Limits" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Section179Limits_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ShippingMethods; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ShippingMethods" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Carrier" character varying(100),
    "EstimatedDays" integer NOT NULL,
    "DefaultCost" numeric,
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: ShippingMethods_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."ShippingMethods" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."ShippingMethods_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Sites; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Sites" (
    "Id" integer NOT NULL,
    "SiteCode" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "Address1" character varying(200),
    "Address2" character varying(200),
    "City" character varying(100),
    "StateProvince" character varying(100),
    "PostalCode" character varying(20),
    "Country" character varying(100),
    "TimeZone" character varying(50),
    "SiteManager" character varying(100),
    "ManagerEmail" character varying(50),
    "ManagerPhone" character varying(30),
    "MainPhone" character varying(50),
    "Fax" character varying(50),
    "SquareFootage" integer,
    "NumberOfBuildings" integer,
    "EmployeeCount" integer,
    "IsPrimarySite" boolean NOT NULL,
    "Latitude" numeric,
    "Longitude" numeric,
    "OperatingHours" character varying(100),
    "NumberOfShifts" integer NOT NULL,
    "ShiftPattern" character varying(200),
    "Is24x7" boolean NOT NULL,
    "AssetCapacity" integer,
    "CurrentAssetCount" integer NOT NULL,
    "ProductionCapacity" character varying(100),
    "LoadingDocks" integer,
    "ParkingSpaces" integer,
    "EmergencyContact" character varying(200),
    "EmergencyPhone" character varying(50),
    "HasFireSuppression" boolean NOT NULL,
    "HasSecuritySystem" boolean NOT NULL,
    "HasClimateControl" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" text,
    "ModifiedAt" timestamp with time zone,
    "ModifiedBy" text
);


--
-- Name: Sites_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Sites" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Sites_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Skills; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Skills" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Level" integer NOT NULL,
    "CraftId" integer,
    "RequiresTraining" boolean NOT NULL,
    "TrainingHoursRequired" integer,
    "RequiresCertification" boolean NOT NULL,
    "CertificationName" character varying(255),
    "CertificationValidityMonths" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: Skills_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Skills" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Skills_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: TaxCodes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TaxCodes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Rate" numeric NOT NULL,
    "Type" integer NOT NULL,
    "TaxAuthority" character varying(50),
    "GlAccountId" integer,
    "IsRecoverable" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: TaxCodes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."TaxCodes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."TaxCodes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Technicians; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Technicians" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Email" character varying(100),
    "Phone" character varying(30),
    "Specialty" character varying(100),
    "Department" character varying(100),
    "DepartmentId" integer,
    "CostCenterId" integer,
    "HourlyRate" numeric,
    "Notes" character varying(500),
    "Active" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Technicians_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Technicians" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Technicians_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Tenants; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Tenants" (
    "Id" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Description" character varying(500),
    "IsActive" boolean DEFAULT true,
    "CreatedAt" timestamp without time zone DEFAULT now(),
    "CreatedBy" character varying(100),
    "ModifiedAt" timestamp without time zone,
    "ModifiedBy" character varying(100)
);


--
-- Name: Tenants_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."Tenants_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: Tenants_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."Tenants_Id_seq" OWNED BY public."Tenants"."Id";


--
-- Name: UOMDefinitions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UOMDefinitions" (
    "Id" integer NOT NULL,
    "Code" character varying(10) NOT NULL,
    "Name" character varying(50) NOT NULL,
    "Description" character varying(100),
    "Type" integer NOT NULL,
    "IsBaseUnit" boolean NOT NULL,
    "BaseUnitId" integer,
    "ConversionFactor" numeric NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: UOMDefinitions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."UOMDefinitions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."UOMDefinitions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: UsTaxSettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UsTaxSettings" (
    "Id" integer NOT NULL,
    "AssetId" integer NOT NULL,
    "PropertyClass" integer NOT NULL,
    "Convention" integer NOT NULL,
    "UseADS" boolean NOT NULL,
    "Section179Amount" numeric(18,2) NOT NULL,
    "Section179Elected" boolean NOT NULL,
    "BonusDepreciationPercent" numeric(5,2) NOT NULL,
    "BonusDepreciationAmount" numeric(18,2) NOT NULL,
    "QualifiedImprovementProperty" boolean NOT NULL,
    "ListedProperty" boolean NOT NULL,
    "BusinessUsePercent" numeric(5,2) NOT NULL,
    "PlacedInServiceDate" timestamp with time zone,
    "TaxYear" integer NOT NULL,
    "DepreciableBasis" numeric(18,2) NOT NULL,
    "AccumulatedTaxDepreciation" numeric(18,2) NOT NULL,
    "Notes" character varying(200)
);


--
-- Name: UsTaxSettings_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."UsTaxSettings" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."UsTaxSettings_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: UsefulLifeEntries; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UsefulLifeEntries" (
    "Id" integer NOT NULL,
    "UsefulLifeTableId" integer NOT NULL,
    "AssetClassCode" character varying(50) NOT NULL,
    "AssetClassName" character varying(200) NOT NULL,
    "Description" character varying(500),
    "GaapLifeMonths" integer NOT NULL,
    "TaxLifeMonths" integer,
    "MacrsRecoveryYears" integer,
    "CcaClassNumber" integer,
    "CcaRate" numeric,
    "RecommendedMethod" integer NOT NULL,
    "RecommendedConvention" integer NOT NULL,
    "IrsAssetClass" character varying(100),
    "CraAssetClass" character varying(100),
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: UsefulLifeEntries_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."UsefulLifeEntries" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."UsefulLifeEntries_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: UsefulLifeTables; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UsefulLifeTables" (
    "Id" integer NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Jurisdiction" integer NOT NULL,
    "Source" integer NOT NULL,
    "IsActive" boolean NOT NULL
);


--
-- Name: UsefulLifeTables_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."UsefulLifeTables" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."UsefulLifeTables_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Users" (
    "Id" integer NOT NULL,
    "Username" character varying(100) NOT NULL,
    "PasswordHash" character varying(256) NOT NULL,
    "FullName" character varying(200),
    "Email" character varying(200),
    "Role" character varying(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastLoginAt" timestamp with time zone,
    "Language" character varying(5) NOT NULL,
    "TimeZone" character varying(50),
    "CompanyId" integer,
    "MustChangePassword" boolean NOT NULL,
    "PasswordChangedAt" timestamp with time zone
);


--
-- Name: Users_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Users" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Users_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: VendorInvoiceLines; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VendorInvoiceLines" (
    "Id" integer NOT NULL,
    "VendorInvoiceId" integer NOT NULL,
    "LineNumber" integer NOT NULL,
    "Description" character varying(200) NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL,
    "PurchaseOrderLineId" integer,
    "GoodsReceiptLineId" integer,
    "GlAccountId" integer,
    "CostCenterId" integer,
    "Notes" character varying(500)
);


--
-- Name: VendorInvoiceLines_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."VendorInvoiceLines" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."VendorInvoiceLines_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: VendorInvoices; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VendorInvoices" (
    "Id" integer NOT NULL,
    "InvoiceNumber" character varying(50) NOT NULL,
    "VendorId" integer NOT NULL,
    "Status" integer NOT NULL,
    "MatchStatus" integer NOT NULL,
    "InvoiceDate" timestamp with time zone NOT NULL,
    "ReceivedDate" timestamp with time zone NOT NULL,
    "DueDate" timestamp with time zone NOT NULL,
    "PaymentTerms" integer NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Subtotal" numeric(18,2) NOT NULL,
    "TaxAmount" numeric(18,2) NOT NULL,
    "ShippingAmount" numeric(18,2) NOT NULL,
    "Total" numeric(18,2) NOT NULL,
    "AmountPaid" numeric(18,2) NOT NULL,
    "BalanceDue" numeric(18,2) NOT NULL,
    "Notes" character varying(500),
    "InternalNotes" character varying(500),
    "ApprovedById" integer,
    "ApprovedAt" timestamp with time zone,
    "CompanyId" integer,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: VendorInvoices_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."VendorInvoices" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."VendorInvoices_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: VendorItemParts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."VendorItemParts" (
    "Id" integer NOT NULL,
    "VendorId" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "VendorPartNumber" character varying(100) NOT NULL,
    "ItemManufacturerPartId" integer,
    "VendorUom" character varying(20),
    "PackQty" numeric(18,4),
    "LeadTimeDays" integer,
    "MinOrderQty" numeric(18,4),
    "Preferred" boolean NOT NULL,
    "UnitPrice" numeric(18,4),
    "ProductPageUrl" character varying(500),
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    "CreatedBy" character varying(100),
    "ImageUrl" character varying(500),
    "CatalogUrl" character varying(500),
    "ExternalImageUrl" character varying(500),
    "ExtractedMpn" character varying(100),
    "ExtractedSku" character varying(100),
    "LastEnrichedUtc" timestamp with time zone,
    "LastEnrichStatus" character varying(50),
    "PriceEffectiveDate" timestamp with time zone,
    "DatasheetUrl" text,
    "EnrichmentNotes" text
);


--
-- Name: VendorItemParts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."VendorItemParts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."VendorItemParts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: Vendors; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Vendors" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "LegalName" character varying(100),
    "VendorType" integer NOT NULL,
    "Status" integer NOT NULL,
    "ContactName" character varying(100),
    "Phone" character varying(50),
    "Fax" character varying(20),
    "Email" character varying(100),
    "Website" character varying(200),
    "Address" character varying(200),
    "City" character varying(50),
    "State" character varying(50),
    "PostalCode" character varying(20),
    "Country" character varying(50),
    "TaxId" character varying(50),
    "PaymentTerms" integer NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "CreditLimit" numeric(18,2),
    "AccountNumber" character varying(50),
    "Notes" character varying(500),
    "DefaultGlAccountId" integer,
    "CompanyId" integer,
    "IsPreferred" boolean NOT NULL,
    "Is1099Vendor" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: Vendors_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."Vendors" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."Vendors_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: WebhookDeliveryLogs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WebhookDeliveryLogs" (
    "Id" integer NOT NULL,
    "WebhookSubscriptionId" integer NOT NULL,
    "OutboxEventId" integer NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "ResponseStatusCode" integer,
    "DurationMs" integer NOT NULL,
    "Error" character varying(1000),
    "CreatedAt" timestamp with time zone NOT NULL,
    "PayloadSent" text
);


--
-- Name: WebhookDeliveryLogs_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."WebhookDeliveryLogs" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."WebhookDeliveryLogs_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: WebhookSubscriptions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WebhookSubscriptions" (
    "Id" integer NOT NULL,
    "CompanyId" integer NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Url" character varying(500) NOT NULL,
    "IsActive" boolean NOT NULL,
    "EventTypesCsv" character varying(500) NOT NULL,
    "Secret" character varying(64) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "LastDeliveryAt" timestamp with time zone,
    "ConsecutiveFailures" integer DEFAULT 0 NOT NULL,
    "DisabledAt" timestamp with time zone,
    "DisabledReason" character varying(500),
    "FailureCountLifetime" integer DEFAULT 0 NOT NULL,
    "MaxConsecutiveFailures" integer DEFAULT 0 NOT NULL,
    "SuccessCountLifetime" integer DEFAULT 0 NOT NULL
);


--
-- Name: WebhookSubscriptions_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."WebhookSubscriptions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."WebhookSubscriptions_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: WorkOrderOperationLabors; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderOperationLabors" (
    "Id" integer NOT NULL,
    "WorkOrderOperationId" integer NOT NULL,
    "TechnicianId" integer,
    "CraftId" integer,
    "LaborTypeId" integer,
    "WorkDate" timestamp with time zone DEFAULT now() NOT NULL,
    "StartTime" interval,
    "EndTime" interval,
    "Hours" numeric(10,2) DEFAULT 0 NOT NULL,
    "HourlyRate" numeric(10,2) DEFAULT 0 NOT NULL,
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "CreatedBy" character varying(100)
);


--
-- Name: WorkOrderOperationLabor_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkOrderOperationLabor_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: WorkOrderOperationLabor_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkOrderOperationLabor_Id_seq" OWNED BY public."WorkOrderOperationLabors"."Id";


--
-- Name: WorkOrderOperationParts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderOperationParts" (
    "Id" integer NOT NULL,
    "WorkOrderOperationId" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "QuantityPlanned" numeric(18,4) DEFAULT 0 NOT NULL,
    "QuantityIssued" numeric(18,4) DEFAULT 0 NOT NULL,
    "QuantityUsed" numeric(18,4) DEFAULT 0 NOT NULL,
    "QuantityReturned" numeric(18,4) DEFAULT 0 NOT NULL,
    "UnitCost" numeric(18,4) DEFAULT 0 NOT NULL,
    "IssuedFromLocationId" integer,
    "LotNumber" character varying(50),
    "SerialNumber" character varying(50),
    "Notes" character varying(500),
    "IssuedBy" character varying(50),
    "IssuedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: WorkOrderOperationParts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkOrderOperationParts_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: WorkOrderOperationParts_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkOrderOperationParts_Id_seq" OWNED BY public."WorkOrderOperationParts"."Id";


--
-- Name: WorkOrderOperationTools; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderOperationTools" (
    "Id" integer NOT NULL,
    "WorkOrderOperationId" integer NOT NULL,
    "ToolName" character varying(100) NOT NULL,
    "ToolAssetTag" character varying(50),
    "ToolAssetId" integer,
    "QuantityRequired" integer DEFAULT 1 NOT NULL,
    "QuantityUsed" integer DEFAULT 0 NOT NULL,
    "IsCheckedOut" boolean DEFAULT false NOT NULL,
    "CheckedOutAt" timestamp with time zone,
    "ReturnedAt" timestamp with time zone,
    "CheckedOutBy" character varying(100),
    "Notes" character varying(500),
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: WorkOrderOperationTools_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkOrderOperationTools_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: WorkOrderOperationTools_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkOrderOperationTools_Id_seq" OWNED BY public."WorkOrderOperationTools"."Id";


--
-- Name: WorkOrderOperations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderOperations" (
    "Id" integer NOT NULL,
    "MaintenanceEventId" integer NOT NULL,
    "OperationNumber" character varying(20) NOT NULL,
    "Sequence" integer DEFAULT 10 NOT NULL,
    "Type" integer DEFAULT 0 NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Description" character varying(2000),
    "Instructions" character varying(2000),
    "Status" integer DEFAULT 0 NOT NULL,
    "AssignedTechnicianId" integer,
    "CraftId" integer,
    "PlannedHours" numeric(10,2) DEFAULT 0 NOT NULL,
    "ActualHours" numeric(10,2) DEFAULT 0 NOT NULL,
    "PlannedStartDate" timestamp with time zone,
    "PlannedEndDate" timestamp with time zone,
    "ActualStartDate" timestamp with time zone,
    "ActualEndDate" timestamp with time zone,
    "RequiresShutdown" boolean DEFAULT false NOT NULL,
    "RequiresLOTO" boolean DEFAULT false NOT NULL,
    "LOTOProcedureId" character varying(100),
    "RequiresConfinedSpaceEntry" boolean DEFAULT false NOT NULL,
    "RequiresHotWorkPermit" boolean DEFAULT false NOT NULL,
    "Notes" character varying(1000),
    "CompletedBy" character varying(100),
    "CompletedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "CreatedBy" character varying(100),
    "ModifiedAt" timestamp with time zone,
    "ModifiedBy" character varying(100)
);


--
-- Name: WorkOrderOperations_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public."WorkOrderOperations_Id_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: WorkOrderOperations_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public."WorkOrderOperations_Id_seq" OWNED BY public."WorkOrderOperations"."Id";


--
-- Name: WorkOrderParts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderParts" (
    "Id" integer NOT NULL,
    "MaintenanceEventId" integer NOT NULL,
    "ItemId" integer NOT NULL,
    "QuantityPlanned" numeric(18,4) NOT NULL,
    "QuantityIssued" numeric(18,4) NOT NULL,
    "QuantityUsed" numeric(18,4) NOT NULL,
    "QuantityReturned" numeric(18,4) NOT NULL,
    "UnitCost" numeric(18,4) NOT NULL,
    "IssuedFromLocationId" integer,
    "LotNumber" character varying(50),
    "SerialNumber" character varying(50),
    "Notes" character varying(500),
    "IssuedBy" character varying(50),
    "IssuedDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: WorkOrderParts_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."WorkOrderParts" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."WorkOrderParts_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: WorkOrderTypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkOrderTypes" (
    "Id" integer NOT NULL,
    "Code" character varying(20) NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(255),
    "Category" integer NOT NULL,
    "RequiresApproval" boolean NOT NULL,
    "ApprovalThreshold" numeric,
    "DefaultPriorityId" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL
);


--
-- Name: WorkOrderTypes_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."WorkOrderTypes" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."WorkOrderTypes_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: WorkRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkRequests" (
    "Id" integer NOT NULL,
    "RequestNumber" character varying(50) NOT NULL,
    "RequestText" character varying(2000) NOT NULL,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "SiteId" integer,
    "LocationId" integer,
    "AssetId" integer,
    "RequestedBy" character varying(100),
    "RequestedAt" timestamp with time zone NOT NULL,
    "ContactPhone" character varying(100),
    "ContactEmail" character varying(100),
    "AttachmentPaths" character varying(500),
    "GeneratedWorkOrderId" integer,
    "IsAIAssisted" boolean NOT NULL,
    "AIConfidence" character varying(50),
    "AIExplanation" character varying(2000),
    "CreatedBy" character varying(100),
    "CreatedAt" timestamp with time zone NOT NULL,
    "ModifiedBy" character varying(100),
    "ModifiedAt" timestamp with time zone,
    "CompanyId" integer
);


--
-- Name: WorkRequests_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."WorkRequests" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."WorkRequests_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: PMOccurrences Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences" ALTER COLUMN "Id" SET DEFAULT nextval('public."PMOccurrences_Id_seq"'::regclass);


--
-- Name: PMSchedules Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMSchedules" ALTER COLUMN "Id" SET DEFAULT nextval('public."PMSchedules_Id_seq"'::regclass);


--
-- Name: Tenants Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenants" ALTER COLUMN "Id" SET DEFAULT nextval('public."Tenants_Id_seq"'::regclass);


--
-- Name: WorkOrderOperationLabors Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationLabors" ALTER COLUMN "Id" SET DEFAULT nextval('public."WorkOrderOperationLabor_Id_seq"'::regclass);


--
-- Name: WorkOrderOperationParts Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationParts" ALTER COLUMN "Id" SET DEFAULT nextval('public."WorkOrderOperationParts_Id_seq"'::regclass);


--
-- Name: WorkOrderOperationTools Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationTools" ALTER COLUMN "Id" SET DEFAULT nextval('public."WorkOrderOperationTools_Id_seq"'::regclass);


--
-- Name: WorkOrderOperations Id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperations" ALTER COLUMN "Id" SET DEFAULT nextval('public."WorkOrderOperations_Id_seq"'::regclass);


--
-- Name: ActionCodes PK_ActionCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ActionCodes"
    ADD CONSTRAINT "PK_ActionCodes" PRIMARY KEY ("Id");


--
-- Name: ApiKeys PK_ApiKeys; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ApiKeys"
    ADD CONSTRAINT "PK_ApiKeys" PRIMARY KEY ("Id");


--
-- Name: ApprovalWorkflows PK_ApprovalWorkflows; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ApprovalWorkflows"
    ADD CONSTRAINT "PK_ApprovalWorkflows" PRIMARY KEY ("Id");


--
-- Name: AssetBookSettings PK_AssetBookSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetBookSettings"
    ADD CONSTRAINT "PK_AssetBookSettings" PRIMARY KEY ("Id");


--
-- Name: AssetCategories PK_AssetCategories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "PK_AssetCategories" PRIMARY KEY ("Id");


--
-- Name: AssetInventories PK_AssetInventories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetInventories"
    ADD CONSTRAINT "PK_AssetInventories" PRIMARY KEY ("Id");


--
-- Name: AssetTaxSettings PK_AssetTaxSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetTaxSettings"
    ADD CONSTRAINT "PK_AssetTaxSettings" PRIMARY KEY ("Id");


--
-- Name: AssetTransfers PK_AssetTransfers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetTransfers"
    ADD CONSTRAINT "PK_AssetTransfers" PRIMARY KEY ("Id");


--
-- Name: Assets PK_Assets; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "PK_Assets" PRIMARY KEY ("Id");


--
-- Name: Attachments PK_Attachments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "PK_Attachments" PRIMARY KEY ("Id");


--
-- Name: AuditLogs PK_AuditLogs; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuditLogs"
    ADD CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id");


--
-- Name: BonusDepreciationRates PK_BonusDepreciationRates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BonusDepreciationRates"
    ADD CONSTRAINT "PK_BonusDepreciationRates" PRIMARY KEY ("Id");


--
-- Name: BookGlAccounts PK_BookGlAccounts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BookGlAccounts"
    ADD CONSTRAINT "PK_BookGlAccounts" PRIMARY KEY ("Id");


--
-- Name: Books PK_Books; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Books"
    ADD CONSTRAINT "PK_Books" PRIMARY KEY ("Id");


--
-- Name: BulkOperations PK_BulkOperations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BulkOperations"
    ADD CONSTRAINT "PK_BulkOperations" PRIMARY KEY ("Id");


--
-- Name: CapitalImprovements PK_CapitalImprovements; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CapitalImprovements"
    ADD CONSTRAINT "PK_CapitalImprovements" PRIMARY KEY ("Id");


--
-- Name: CauseCodes PK_CauseCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CauseCodes"
    ADD CONSTRAINT "PK_CauseCodes" PRIMARY KEY ("Id");


--
-- Name: CcaClassBalances PK_CcaClassBalances; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaClassBalances"
    ADD CONSTRAINT "PK_CcaClassBalances" PRIMARY KEY ("Id");


--
-- Name: CcaClasses PK_CcaClasses; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaClasses"
    ADD CONSTRAINT "PK_CcaClasses" PRIMARY KEY ("Id");


--
-- Name: CcaTransactions PK_CcaTransactions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaTransactions"
    ADD CONSTRAINT "PK_CcaTransactions" PRIMARY KEY ("Id");


--
-- Name: CipCosts PK_CipCosts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipCosts"
    ADD CONSTRAINT "PK_CipCosts" PRIMARY KEY ("Id");


--
-- Name: CipProjects PK_CipProjects; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "PK_CipProjects" PRIMARY KEY ("Id");


--
-- Name: Companies PK_Companies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Companies"
    ADD CONSTRAINT "PK_Companies" PRIMARY KEY ("Id");


--
-- Name: CostCenters PK_CostCenters; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CostCenters"
    ADD CONSTRAINT "PK_CostCenters" PRIMARY KEY ("Id");


--
-- Name: Crafts PK_Crafts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Crafts"
    ADD CONSTRAINT "PK_Crafts" PRIMARY KEY ("Id");


--
-- Name: Currencies PK_Currencies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Currencies"
    ADD CONSTRAINT "PK_Currencies" PRIMARY KEY ("Id");


--
-- Name: Departments PK_Departments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Departments"
    ADD CONSTRAINT "PK_Departments" PRIMARY KEY ("Id");


--
-- Name: DepreciationPolicies PK_DepreciationPolicies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationPolicies"
    ADD CONSTRAINT "PK_DepreciationPolicies" PRIMARY KEY ("Id");


--
-- Name: DepreciationRunDetails PK_DepreciationRunDetails; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRunDetails"
    ADD CONSTRAINT "PK_DepreciationRunDetails" PRIMARY KEY ("Id");


--
-- Name: DepreciationRuns PK_DepreciationRuns; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRuns"
    ADD CONSTRAINT "PK_DepreciationRuns" PRIMARY KEY ("Id");


--
-- Name: ExchangeRates PK_ExchangeRates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ExchangeRates"
    ADD CONSTRAINT "PK_ExchangeRates" PRIMARY KEY ("Id");


--
-- Name: FailureCodes PK_FailureCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FailureCodes"
    ADD CONSTRAINT "PK_FailureCodes" PRIMARY KEY ("Id");


--
-- Name: FiscalPeriods PK_FiscalPeriods; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FiscalPeriods"
    ADD CONSTRAINT "PK_FiscalPeriods" PRIMARY KEY ("Id");


--
-- Name: FiscalYears PK_FiscalYears; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FiscalYears"
    ADD CONSTRAINT "PK_FiscalYears" PRIMARY KEY ("Id");


--
-- Name: GlAccounts PK_GlAccounts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GlAccounts"
    ADD CONSTRAINT "PK_GlAccounts" PRIMARY KEY ("Id");


--
-- Name: GoodsReceiptLines PK_GoodsReceiptLines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceiptLines"
    ADD CONSTRAINT "PK_GoodsReceiptLines" PRIMARY KEY ("Id");


--
-- Name: GoodsReceipts PK_GoodsReceipts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceipts"
    ADD CONSTRAINT "PK_GoodsReceipts" PRIMARY KEY ("Id");


--
-- Name: InboundEvents PK_InboundEvents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InboundEvents"
    ADD CONSTRAINT "PK_InboundEvents" PRIMARY KEY ("Id");


--
-- Name: IntegrationEndpoints PK_IntegrationEndpoints; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IntegrationEndpoints"
    ADD CONSTRAINT "PK_IntegrationEndpoints" PRIMARY KEY ("Id");


--
-- Name: IntegrationMappings PK_IntegrationMappings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IntegrationMappings"
    ADD CONSTRAINT "PK_IntegrationMappings" PRIMARY KEY ("Id");


--
-- Name: InventoryLists PK_InventoryLists; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InventoryLists"
    ADD CONSTRAINT "PK_InventoryLists" PRIMARY KEY ("Id");


--
-- Name: InventoryScans PK_InventoryScans; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InventoryScans"
    ADD CONSTRAINT "PK_InventoryScans" PRIMARY KEY ("Id");


--
-- Name: InvoicePayments PK_InvoicePayments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InvoicePayments"
    ADD CONSTRAINT "PK_InvoicePayments" PRIMARY KEY ("Id");


--
-- Name: ItemAlternates PK_ItemAlternates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemAlternates"
    ADD CONSTRAINT "PK_ItemAlternates" PRIMARY KEY ("Id");


--
-- Name: ItemApprovedVendors PK_ItemApprovedVendors; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "PK_ItemApprovedVendors" PRIMARY KEY ("Id");


--
-- Name: ItemCategories PK_ItemCategories; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCategories"
    ADD CONSTRAINT "PK_ItemCategories" PRIMARY KEY ("Id");


--
-- Name: ItemCompanyStockings PK_ItemCompanyStockings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCompanyStockings"
    ADD CONSTRAINT "PK_ItemCompanyStockings" PRIMARY KEY ("Id");


--
-- Name: ItemImages PK_ItemImages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemImages"
    ADD CONSTRAINT "PK_ItemImages" PRIMARY KEY ("Id");


--
-- Name: ItemInventories2 PK_ItemInventories2; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemInventories2"
    ADD CONSTRAINT "PK_ItemInventories2" PRIMARY KEY ("Id");


--
-- Name: ItemManufacturerParts PK_ItemManufacturerParts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemManufacturerParts"
    ADD CONSTRAINT "PK_ItemManufacturerParts" PRIMARY KEY ("Id");


--
-- Name: ItemRevisions PK_ItemRevisions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemRevisions"
    ADD CONSTRAINT "PK_ItemRevisions" PRIMARY KEY ("Id");


--
-- Name: ItemSupersessions PK_ItemSupersessions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemSupersessions"
    ADD CONSTRAINT "PK_ItemSupersessions" PRIMARY KEY ("Id");


--
-- Name: ItemTransactions PK_ItemTransactions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "PK_ItemTransactions" PRIMARY KEY ("Id");


--
-- Name: ItemVendors PK_ItemVendors; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemVendors"
    ADD CONSTRAINT "PK_ItemVendors" PRIMARY KEY ("Id");


--
-- Name: Items PK_Items; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "PK_Items" PRIMARY KEY ("Id");


--
-- Name: JournalEntries PK_JournalEntries; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."JournalEntries"
    ADD CONSTRAINT "PK_JournalEntries" PRIMARY KEY ("Id");


--
-- Name: JournalLines PK_JournalLines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."JournalLines"
    ADD CONSTRAINT "PK_JournalLines" PRIMARY KEY ("Id");


--
-- Name: KitItems PK_KitItems; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."KitItems"
    ADD CONSTRAINT "PK_KitItems" PRIMARY KEY ("Id");


--
-- Name: Kits PK_Kits; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Kits"
    ADD CONSTRAINT "PK_Kits" PRIMARY KEY ("Id");


--
-- Name: LaborRates PK_LaborRates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LaborRates"
    ADD CONSTRAINT "PK_LaborRates" PRIMARY KEY ("Id");


--
-- Name: LaborTypes PK_LaborTypes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LaborTypes"
    ADD CONSTRAINT "PK_LaborTypes" PRIMARY KEY ("Id");


--
-- Name: LessonsLearned PK_LessonsLearned; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LessonsLearned"
    ADD CONSTRAINT "PK_LessonsLearned" PRIMARY KEY ("Id");


--
-- Name: Locations PK_Locations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Locations"
    ADD CONSTRAINT "PK_Locations" PRIMARY KEY ("Id");


--
-- Name: MaintenanceEvents PK_MaintenanceEvents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceEvents"
    ADD CONSTRAINT "PK_MaintenanceEvents" PRIMARY KEY ("Id");


--
-- Name: MaintenanceSchedules PK_MaintenanceSchedules; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceSchedules"
    ADD CONSTRAINT "PK_MaintenanceSchedules" PRIMARY KEY ("Id");


--
-- Name: MaintenanceTypeCodes PK_MaintenanceTypeCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceTypeCodes"
    ADD CONSTRAINT "PK_MaintenanceTypeCodes" PRIMARY KEY ("Id");


--
-- Name: Manufacturers PK_Manufacturers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Manufacturers"
    ADD CONSTRAINT "PK_Manufacturers" PRIMARY KEY ("Id");


--
-- Name: MeterReadings PK_MeterReadings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeterReadings"
    ADD CONSTRAINT "PK_MeterReadings" PRIMARY KEY ("Id");


--
-- Name: NumberingSequences PK_NumberingSequences; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NumberingSequences"
    ADD CONSTRAINT "PK_NumberingSequences" PRIMARY KEY ("Id");


--
-- Name: OutboxEvents PK_OutboxEvents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OutboxEvents"
    ADD CONSTRAINT "PK_OutboxEvents" PRIMARY KEY ("Id");


--
-- Name: PMTemplateAssets PK_PMTemplateAssets; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateAssets"
    ADD CONSTRAINT "PK_PMTemplateAssets" PRIMARY KEY ("Id");


--
-- Name: PMTemplateItems PK_PMTemplateItems; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateItems"
    ADD CONSTRAINT "PK_PMTemplateItems" PRIMARY KEY ("Id");


--
-- Name: PMTemplateRevisionOperations PK_PMTemplateRevisionOperations; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateRevisionOperations"
    ADD CONSTRAINT "PK_PMTemplateRevisionOperations" PRIMARY KEY ("Id");


--
-- Name: PMTemplateRevisions PK_PMTemplateRevisions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateRevisions"
    ADD CONSTRAINT "PK_PMTemplateRevisions" PRIMARY KEY ("Id");


--
-- Name: PMTemplates PK_PMTemplates; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplates"
    ADD CONSTRAINT "PK_PMTemplates" PRIMARY KEY ("Id");


--
-- Name: PartialDisposals PK_PartialDisposals; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PartialDisposals"
    ADD CONSTRAINT "PK_PartialDisposals" PRIMARY KEY ("Id");


--
-- Name: PaymentTerms PK_PaymentTerms; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PaymentTerms"
    ADD CONSTRAINT "PK_PaymentTerms" PRIMARY KEY ("Id");


--
-- Name: PeriodLocks PK_PeriodLocks; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PeriodLocks"
    ADD CONSTRAINT "PK_PeriodLocks" PRIMARY KEY ("Id");


--
-- Name: PolicyCategoryDefaults PK_PolicyCategoryDefaults; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PolicyCategoryDefaults"
    ADD CONSTRAINT "PK_PolicyCategoryDefaults" PRIMARY KEY ("Id");


--
-- Name: PriorityLevels PK_PriorityLevels; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PriorityLevels"
    ADD CONSTRAINT "PK_PriorityLevels" PRIMARY KEY ("Id");


--
-- Name: ProblemCodes PK_ProblemCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProblemCodes"
    ADD CONSTRAINT "PK_ProblemCodes" PRIMARY KEY ("Id");


--
-- Name: ProjectManagers PK_ProjectManagers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectManagers"
    ADD CONSTRAINT "PK_ProjectManagers" PRIMARY KEY ("Id");


--
-- Name: PurchaseOrderLines PK_PurchaseOrderLines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "PK_PurchaseOrderLines" PRIMARY KEY ("Id");


--
-- Name: PurchaseOrderReleases PK_PurchaseOrderReleases; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderReleases"
    ADD CONSTRAINT "PK_PurchaseOrderReleases" PRIMARY KEY ("Id");


--
-- Name: PurchaseOrders PK_PurchaseOrders; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "PK_PurchaseOrders" PRIMARY KEY ("Id");


--
-- Name: PurchaseRequisitionLines PK_PurchaseRequisitionLines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "PK_PurchaseRequisitionLines" PRIMARY KEY ("Id");


--
-- Name: PurchaseRequisitions PK_PurchaseRequisitions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "PK_PurchaseRequisitions" PRIMARY KEY ("Id");


--
-- Name: ReorderAlerts PK_ReorderAlerts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReorderAlerts"
    ADD CONSTRAINT "PK_ReorderAlerts" PRIMARY KEY ("Id");


--
-- Name: Section179Limits PK_Section179Limits; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Section179Limits"
    ADD CONSTRAINT "PK_Section179Limits" PRIMARY KEY ("Id");


--
-- Name: ShippingMethods PK_ShippingMethods; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ShippingMethods"
    ADD CONSTRAINT "PK_ShippingMethods" PRIMARY KEY ("Id");


--
-- Name: Sites PK_Sites; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sites"
    ADD CONSTRAINT "PK_Sites" PRIMARY KEY ("Id");


--
-- Name: Skills PK_Skills; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Skills"
    ADD CONSTRAINT "PK_Skills" PRIMARY KEY ("Id");


--
-- Name: TaxCodes PK_TaxCodes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaxCodes"
    ADD CONSTRAINT "PK_TaxCodes" PRIMARY KEY ("Id");


--
-- Name: Technicians PK_Technicians; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Technicians"
    ADD CONSTRAINT "PK_Technicians" PRIMARY KEY ("Id");


--
-- Name: UOMDefinitions PK_UOMDefinitions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UOMDefinitions"
    ADD CONSTRAINT "PK_UOMDefinitions" PRIMARY KEY ("Id");


--
-- Name: UsTaxSettings PK_UsTaxSettings; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UsTaxSettings"
    ADD CONSTRAINT "PK_UsTaxSettings" PRIMARY KEY ("Id");


--
-- Name: UsefulLifeEntries PK_UsefulLifeEntries; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UsefulLifeEntries"
    ADD CONSTRAINT "PK_UsefulLifeEntries" PRIMARY KEY ("Id");


--
-- Name: UsefulLifeTables PK_UsefulLifeTables; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UsefulLifeTables"
    ADD CONSTRAINT "PK_UsefulLifeTables" PRIMARY KEY ("Id");


--
-- Name: Users PK_Users; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Users"
    ADD CONSTRAINT "PK_Users" PRIMARY KEY ("Id");


--
-- Name: VendorInvoiceLines PK_VendorInvoiceLines; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "PK_VendorInvoiceLines" PRIMARY KEY ("Id");


--
-- Name: VendorInvoices PK_VendorInvoices; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoices"
    ADD CONSTRAINT "PK_VendorInvoices" PRIMARY KEY ("Id");


--
-- Name: VendorItemParts PK_VendorItemParts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorItemParts"
    ADD CONSTRAINT "PK_VendorItemParts" PRIMARY KEY ("Id");


--
-- Name: Vendors PK_Vendors; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Vendors"
    ADD CONSTRAINT "PK_Vendors" PRIMARY KEY ("Id");


--
-- Name: WebhookDeliveryLogs PK_WebhookDeliveryLogs; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebhookDeliveryLogs"
    ADD CONSTRAINT "PK_WebhookDeliveryLogs" PRIMARY KEY ("Id");


--
-- Name: WebhookSubscriptions PK_WebhookSubscriptions; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebhookSubscriptions"
    ADD CONSTRAINT "PK_WebhookSubscriptions" PRIMARY KEY ("Id");


--
-- Name: WorkOrderParts PK_WorkOrderParts; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderParts"
    ADD CONSTRAINT "PK_WorkOrderParts" PRIMARY KEY ("Id");


--
-- Name: WorkOrderTypes PK_WorkOrderTypes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderTypes"
    ADD CONSTRAINT "PK_WorkOrderTypes" PRIMARY KEY ("Id");


--
-- Name: WorkRequests PK_WorkRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "PK_WorkRequests" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: PMOccurrences PMOccurrences_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "PMOccurrences_pkey" PRIMARY KEY ("Id");


--
-- Name: PMSchedules PMSchedules_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMSchedules"
    ADD CONSTRAINT "PMSchedules_pkey" PRIMARY KEY ("Id");


--
-- Name: Tenants Tenants_Code_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenants"
    ADD CONSTRAINT "Tenants_Code_key" UNIQUE ("Code");


--
-- Name: Tenants Tenants_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenants"
    ADD CONSTRAINT "Tenants_pkey" PRIMARY KEY ("Id");


--
-- Name: WorkOrderOperationLabors WorkOrderOperationLabor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationLabors"
    ADD CONSTRAINT "WorkOrderOperationLabor_pkey" PRIMARY KEY ("Id");


--
-- Name: WorkOrderOperationParts WorkOrderOperationParts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationParts"
    ADD CONSTRAINT "WorkOrderOperationParts_pkey" PRIMARY KEY ("Id");


--
-- Name: WorkOrderOperationTools WorkOrderOperationTools_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationTools"
    ADD CONSTRAINT "WorkOrderOperationTools_pkey" PRIMARY KEY ("Id");


--
-- Name: WorkOrderOperations WorkOrderOperations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperations"
    ADD CONSTRAINT "WorkOrderOperations_pkey" PRIMARY KEY ("Id");


--
-- Name: IX_AssetBookSettings_AssetId_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_AssetBookSettings_AssetId_BookId" ON public."AssetBookSettings" USING btree ("AssetId", "BookId");


--
-- Name: IX_AssetBookSettings_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetBookSettings_BookId" ON public."AssetBookSettings" USING btree ("BookId");


--
-- Name: IX_AssetBookSettings_BookId1; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetBookSettings_BookId1" ON public."AssetBookSettings" USING btree ("BookId1");


--
-- Name: IX_AssetCategories_AccumDepGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_AccumDepGlAccountId" ON public."AssetCategories" USING btree ("AccumDepGlAccountId");


--
-- Name: IX_AssetCategories_AssetGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_AssetGlAccountId" ON public."AssetCategories" USING btree ("AssetGlAccountId");


--
-- Name: IX_AssetCategories_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_Code" ON public."AssetCategories" USING btree ("Code");


--
-- Name: IX_AssetCategories_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_CompanyId" ON public."AssetCategories" USING btree ("CompanyId");


--
-- Name: IX_AssetCategories_DepExpGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_DepExpGlAccountId" ON public."AssetCategories" USING btree ("DepExpGlAccountId");


--
-- Name: IX_AssetCategories_DepreciationPolicyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetCategories_DepreciationPolicyId" ON public."AssetCategories" USING btree ("DepreciationPolicyId");


--
-- Name: IX_AssetInventories_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_AssetInventories_AssetId" ON public."AssetInventories" USING btree ("AssetId");


--
-- Name: IX_AssetInventories_BarcodeNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetInventories_BarcodeNumber" ON public."AssetInventories" USING btree ("BarcodeNumber");


--
-- Name: IX_AssetInventories_LastInventoryListId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetInventories_LastInventoryListId" ON public."AssetInventories" USING btree ("LastInventoryListId");


--
-- Name: IX_AssetTaxSettings_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_AssetTaxSettings_AssetId" ON public."AssetTaxSettings" USING btree ("AssetId");


--
-- Name: IX_AssetTaxSettings_CcaClassId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetTaxSettings_CcaClassId" ON public."AssetTaxSettings" USING btree ("CcaClassId");


--
-- Name: IX_AssetTransfers_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetTransfers_AssetId" ON public."AssetTransfers" USING btree ("AssetId");


--
-- Name: IX_AssetTransfers_TransferDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AssetTransfers_TransferDate" ON public."AssetTransfers" USING btree ("TransferDate");


--
-- Name: IX_Assets_AssetCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_AssetCategoryId" ON public."Assets" USING btree ("AssetCategoryId");


--
-- Name: IX_Assets_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_CompanyId" ON public."Assets" USING btree ("CompanyId");


--
-- Name: IX_Assets_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_CostCenterId" ON public."Assets" USING btree ("CostCenterId");


--
-- Name: IX_Assets_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_DepartmentId" ON public."Assets" USING btree ("DepartmentId");


--
-- Name: IX_Assets_LocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_LocationId" ON public."Assets" USING btree ("LocationId");


--
-- Name: IX_Assets_ManufacturerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_ManufacturerId" ON public."Assets" USING btree ("ManufacturerId");


--
-- Name: IX_Assets_ParentAssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_ParentAssetId" ON public."Assets" USING btree ("ParentAssetId");


--
-- Name: IX_Assets_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_SiteId" ON public."Assets" USING btree ("SiteId");


--
-- Name: IX_Assets_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Assets_VendorId" ON public."Assets" USING btree ("VendorId");


--
-- Name: IX_Attachments_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_AssetId" ON public."Attachments" USING btree ("AssetId");


--
-- Name: IX_Attachments_AssetTransferId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_AssetTransferId" ON public."Attachments" USING btree ("AssetTransferId");


--
-- Name: IX_Attachments_CapitalImprovementId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_CapitalImprovementId" ON public."Attachments" USING btree ("CapitalImprovementId");


--
-- Name: IX_Attachments_CipCostId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_CipCostId" ON public."Attachments" USING btree ("CipCostId");


--
-- Name: IX_Attachments_CipProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_CipProjectId" ON public."Attachments" USING btree ("CipProjectId");


--
-- Name: IX_Attachments_MaintenanceEventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachments_MaintenanceEventId" ON public."Attachments" USING btree ("MaintenanceEventId");


--
-- Name: IX_AuditLogs_EntityType; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLogs_EntityType" ON public."AuditLogs" USING btree ("EntityType");


--
-- Name: IX_AuditLogs_EntityType_EntityId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLogs_EntityType_EntityId" ON public."AuditLogs" USING btree ("EntityType", "EntityId");


--
-- Name: IX_AuditLogs_Timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLogs_Timestamp" ON public."AuditLogs" USING btree ("Timestamp");


--
-- Name: IX_BonusDepreciationRates_TaxYear; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_BonusDepreciationRates_TaxYear" ON public."BonusDepreciationRates" USING btree ("TaxYear");


--
-- Name: IX_BookGlAccounts_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_BookGlAccounts_BookId" ON public."BookGlAccounts" USING btree ("BookId");


--
-- Name: IX_Books_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Books_CompanyId" ON public."Books" USING btree ("CompanyId");


--
-- Name: IX_Books_DefaultPolicyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Books_DefaultPolicyId" ON public."Books" USING btree ("DefaultPolicyId");


--
-- Name: IX_CapitalImprovements_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CapitalImprovements_AssetId" ON public."CapitalImprovements" USING btree ("AssetId");


--
-- Name: IX_CapitalImprovements_ImprovementDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CapitalImprovements_ImprovementDate" ON public."CapitalImprovements" USING btree ("ImprovementDate");


--
-- Name: IX_CauseCodes_ParentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CauseCodes_ParentId" ON public."CauseCodes" USING btree ("ParentId");


--
-- Name: IX_CcaClassBalances_CcaClassId_FiscalYear; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CcaClassBalances_CcaClassId_FiscalYear" ON public."CcaClassBalances" USING btree ("CcaClassId", "FiscalYear");


--
-- Name: IX_CcaClasses_ClassNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CcaClasses_ClassNumber" ON public."CcaClasses" USING btree ("ClassNumber");


--
-- Name: IX_CcaTransactions_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CcaTransactions_AssetId" ON public."CcaTransactions" USING btree ("AssetId");


--
-- Name: IX_CcaTransactions_CcaClassId_FiscalYear; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CcaTransactions_CcaClassId_FiscalYear" ON public."CcaTransactions" USING btree ("CcaClassId", "FiscalYear");


--
-- Name: IX_CipCosts_CipProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipCosts_CipProjectId" ON public."CipCosts" USING btree ("CipProjectId");


--
-- Name: IX_CipCosts_TransactionDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipCosts_TransactionDate" ON public."CipCosts" USING btree ("TransactionDate");


--
-- Name: IX_CipProjects_ConvertedAssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_ConvertedAssetId" ON public."CipProjects" USING btree ("ConvertedAssetId");


--
-- Name: IX_CipProjects_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_CostCenterId" ON public."CipProjects" USING btree ("CostCenterId");


--
-- Name: IX_CipProjects_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_DepartmentId" ON public."CipProjects" USING btree ("DepartmentId");


--
-- Name: IX_CipProjects_GlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_GlAccountId" ON public."CipProjects" USING btree ("GlAccountId");


--
-- Name: IX_CipProjects_ProjectManagerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_ProjectManagerId" ON public."CipProjects" USING btree ("ProjectManagerId");


--
-- Name: IX_CipProjects_ProjectNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_CipProjects_ProjectNumber" ON public."CipProjects" USING btree ("ProjectNumber");


--
-- Name: IX_CipProjects_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CipProjects_Status" ON public."CipProjects" USING btree ("Status");


--
-- Name: IX_Companies_CompanyCode; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Companies_CompanyCode" ON public."Companies" USING btree ("CompanyCode");


--
-- Name: IX_Companies_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Companies_Name" ON public."Companies" USING btree ("Name");


--
-- Name: IX_Companies_ParentCompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Companies_ParentCompanyId" ON public."Companies" USING btree ("ParentCompanyId");


--
-- Name: IX_CostCenters_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CostCenters_Code" ON public."CostCenters" USING btree ("Code");


--
-- Name: IX_CostCenters_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CostCenters_CompanyId" ON public."CostCenters" USING btree ("CompanyId");


--
-- Name: IX_CostCenters_ParentCostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_CostCenters_ParentCostCenterId" ON public."CostCenters" USING btree ("ParentCostCenterId");


--
-- Name: IX_Departments_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Departments_Code" ON public."Departments" USING btree ("Code");


--
-- Name: IX_Departments_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Departments_CompanyId" ON public."Departments" USING btree ("CompanyId");


--
-- Name: IX_Departments_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Departments_CostCenterId" ON public."Departments" USING btree ("CostCenterId");


--
-- Name: IX_DepreciationPolicies_CcaClassId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationPolicies_CcaClassId" ON public."DepreciationPolicies" USING btree ("CcaClassId");


--
-- Name: IX_DepreciationPolicies_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationPolicies_CompanyId" ON public."DepreciationPolicies" USING btree ("CompanyId");


--
-- Name: IX_DepreciationRunDetails_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationRunDetails_AssetId" ON public."DepreciationRunDetails" USING btree ("AssetId");


--
-- Name: IX_DepreciationRunDetails_DepreciationRunId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationRunDetails_DepreciationRunId" ON public."DepreciationRunDetails" USING btree ("DepreciationRunId");


--
-- Name: IX_DepreciationRuns_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationRuns_BookId" ON public."DepreciationRuns" USING btree ("BookId");


--
-- Name: IX_DepreciationRuns_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationRuns_CompanyId" ON public."DepreciationRuns" USING btree ("CompanyId");


--
-- Name: IX_DepreciationRuns_FiscalPeriodId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DepreciationRuns_FiscalPeriodId" ON public."DepreciationRuns" USING btree ("FiscalPeriodId");


--
-- Name: IX_FailureCodes_ParentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FailureCodes_ParentId" ON public."FailureCodes" USING btree ("ParentId");


--
-- Name: IX_FiscalPeriods_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FiscalPeriods_CompanyId" ON public."FiscalPeriods" USING btree ("CompanyId");


--
-- Name: IX_FiscalPeriods_FiscalYearId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FiscalPeriods_FiscalYearId" ON public."FiscalPeriods" USING btree ("FiscalYearId");


--
-- Name: IX_FiscalYears_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_FiscalYears_CompanyId" ON public."FiscalYears" USING btree ("CompanyId");


--
-- Name: IX_GlAccounts_AccountNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GlAccounts_AccountNumber" ON public."GlAccounts" USING btree ("AccountNumber");


--
-- Name: IX_GlAccounts_Category; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GlAccounts_Category" ON public."GlAccounts" USING btree ("Category");


--
-- Name: IX_GlAccounts_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GlAccounts_CompanyId" ON public."GlAccounts" USING btree ("CompanyId");


--
-- Name: IX_GlAccounts_ParentAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GlAccounts_ParentAccountId" ON public."GlAccounts" USING btree ("ParentAccountId");


--
-- Name: IX_GoodsReceiptLines_GoodsReceiptId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GoodsReceiptLines_GoodsReceiptId" ON public."GoodsReceiptLines" USING btree ("GoodsReceiptId");


--
-- Name: IX_GoodsReceiptLines_PurchaseOrderLineId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GoodsReceiptLines_PurchaseOrderLineId" ON public."GoodsReceiptLines" USING btree ("PurchaseOrderLineId");


--
-- Name: IX_GoodsReceiptLines_ReceivingLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GoodsReceiptLines_ReceivingLocationId" ON public."GoodsReceiptLines" USING btree ("ReceivingLocationId");


--
-- Name: IX_GoodsReceipts_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GoodsReceipts_CompanyId" ON public."GoodsReceipts" USING btree ("CompanyId");


--
-- Name: IX_GoodsReceipts_PurchaseOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_GoodsReceipts_PurchaseOrderId" ON public."GoodsReceipts" USING btree ("PurchaseOrderId");


--
-- Name: IX_InboundEvents_IntegrationEndpointId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InboundEvents_IntegrationEndpointId" ON public."InboundEvents" USING btree ("IntegrationEndpointId");


--
-- Name: IX_InboundEvents_TenantId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InboundEvents_TenantId" ON public."InboundEvents" USING btree ("TenantId");


--
-- Name: IX_IntegrationEndpoints_TenantId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_IntegrationEndpoints_TenantId" ON public."IntegrationEndpoints" USING btree ("TenantId");


--
-- Name: IX_IntegrationMappings_IntegrationEndpointId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_IntegrationMappings_IntegrationEndpointId" ON public."IntegrationMappings" USING btree ("IntegrationEndpointId");


--
-- Name: IX_InventoryLists_CreatedDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InventoryLists_CreatedDate" ON public."InventoryLists" USING btree ("CreatedDate");


--
-- Name: IX_InventoryLists_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InventoryLists_Status" ON public."InventoryLists" USING btree ("Status");


--
-- Name: IX_InventoryScans_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InventoryScans_AssetId" ON public."InventoryScans" USING btree ("AssetId");


--
-- Name: IX_InventoryScans_InventoryListId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InventoryScans_InventoryListId" ON public."InventoryScans" USING btree ("InventoryListId");


--
-- Name: IX_InventoryScans_ScanDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InventoryScans_ScanDate" ON public."InventoryScans" USING btree ("ScanDate");


--
-- Name: IX_InvoicePayments_VendorInvoiceId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_InvoicePayments_VendorInvoiceId" ON public."InvoicePayments" USING btree ("VendorInvoiceId");


--
-- Name: IX_ItemAlternates_AlternateItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemAlternates_AlternateItemId" ON public."ItemAlternates" USING btree ("AlternateItemId");


--
-- Name: IX_ItemAlternates_CreatedByUserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemAlternates_CreatedByUserId" ON public."ItemAlternates" USING btree ("CreatedByUserId");


--
-- Name: IX_ItemAlternates_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemAlternates_ItemId" ON public."ItemAlternates" USING btree ("ItemId");


--
-- Name: IX_ItemAlternates_TenantId_ItemId_AlternateItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemAlternates_TenantId_ItemId_AlternateItemId" ON public."ItemAlternates" USING btree ("TenantId", "ItemId", "AlternateItemId");


--
-- Name: IX_ItemApprovedVendors_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemApprovedVendors_CompanyId" ON public."ItemApprovedVendors" USING btree ("CompanyId");


--
-- Name: IX_ItemApprovedVendors_CreatedByUserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemApprovedVendors_CreatedByUserId" ON public."ItemApprovedVendors" USING btree ("CreatedByUserId");


--
-- Name: IX_ItemApprovedVendors_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemApprovedVendors_ItemId" ON public."ItemApprovedVendors" USING btree ("ItemId");


--
-- Name: IX_ItemApprovedVendors_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemApprovedVendors_SiteId" ON public."ItemApprovedVendors" USING btree ("SiteId");


--
-- Name: IX_ItemApprovedVendors_TenantId_ItemId_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemApprovedVendors_TenantId_ItemId_VendorId" ON public."ItemApprovedVendors" USING btree ("TenantId", "ItemId", "VendorId");


--
-- Name: IX_ItemApprovedVendors_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemApprovedVendors_VendorId" ON public."ItemApprovedVendors" USING btree ("VendorId");


--
-- Name: IX_ItemCategories_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCategories_Code" ON public."ItemCategories" USING btree ("Code");


--
-- Name: IX_ItemCategories_DefaultGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCategories_DefaultGlAccountId" ON public."ItemCategories" USING btree ("DefaultGlAccountId");


--
-- Name: IX_ItemCategories_ExpenseGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCategories_ExpenseGlAccountId" ON public."ItemCategories" USING btree ("ExpenseGlAccountId");


--
-- Name: IX_ItemCategories_ParentCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCategories_ParentCategoryId" ON public."ItemCategories" USING btree ("ParentCategoryId");


--
-- Name: IX_ItemCompanyStockings_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCompanyStockings_CompanyId" ON public."ItemCompanyStockings" USING btree ("CompanyId");


--
-- Name: IX_ItemCompanyStockings_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCompanyStockings_ItemId" ON public."ItemCompanyStockings" USING btree ("ItemId");


--
-- Name: IX_ItemCompanyStockings_PreferredVendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemCompanyStockings_PreferredVendorId" ON public."ItemCompanyStockings" USING btree ("PreferredVendorId");


--
-- Name: IX_ItemImages_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemImages_ItemId" ON public."ItemImages" USING btree ("ItemId");


--
-- Name: IX_ItemInventories2_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemInventories2_CompanyId" ON public."ItemInventories2" USING btree ("CompanyId");


--
-- Name: IX_ItemInventories2_ItemId_LocationId_Bin; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemInventories2_ItemId_LocationId_Bin" ON public."ItemInventories2" USING btree ("ItemId", "LocationId", "Bin");


--
-- Name: IX_ItemInventories2_LocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemInventories2_LocationId" ON public."ItemInventories2" USING btree ("LocationId");


--
-- Name: IX_ItemManufacturerParts_ItemId_ManufacturerId_MfrPartNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemManufacturerParts_ItemId_ManufacturerId_MfrPartNumber" ON public."ItemManufacturerParts" USING btree ("ItemId", "ManufacturerId", "MfrPartNumber");


--
-- Name: IX_ItemManufacturerParts_ManufacturerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemManufacturerParts_ManufacturerId" ON public."ItemManufacturerParts" USING btree ("ManufacturerId");


--
-- Name: IX_ItemManufacturerParts_MfrPartNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemManufacturerParts_MfrPartNumber" ON public."ItemManufacturerParts" USING btree ("MfrPartNumber");


--
-- Name: IX_ItemRevisions_ItemId_RevisionCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemRevisions_ItemId_RevisionCode" ON public."ItemRevisions" USING btree ("ItemId", "RevisionCode");


--
-- Name: IX_ItemRevisions_SupersedesItemRevisionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemRevisions_SupersedesItemRevisionId" ON public."ItemRevisions" USING btree ("SupersedesItemRevisionId");


--
-- Name: IX_ItemSupersessions_CreatedByUserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemSupersessions_CreatedByUserId" ON public."ItemSupersessions" USING btree ("CreatedByUserId");


--
-- Name: IX_ItemSupersessions_NewItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemSupersessions_NewItemId" ON public."ItemSupersessions" USING btree ("NewItemId");


--
-- Name: IX_ItemSupersessions_OldItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemSupersessions_OldItemId" ON public."ItemSupersessions" USING btree ("OldItemId");


--
-- Name: IX_ItemSupersessions_TenantId_OldItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemSupersessions_TenantId_OldItemId" ON public."ItemSupersessions" USING btree ("TenantId", "OldItemId");


--
-- Name: IX_ItemTransactions_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_CompanyId" ON public."ItemTransactions" USING btree ("CompanyId");


--
-- Name: IX_ItemTransactions_FromLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_FromLocationId" ON public."ItemTransactions" USING btree ("FromLocationId");


--
-- Name: IX_ItemTransactions_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_ItemId" ON public."ItemTransactions" USING btree ("ItemId");


--
-- Name: IX_ItemTransactions_PurchaseOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_PurchaseOrderId" ON public."ItemTransactions" USING btree ("PurchaseOrderId");


--
-- Name: IX_ItemTransactions_ToLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_ToLocationId" ON public."ItemTransactions" USING btree ("ToLocationId");


--
-- Name: IX_ItemTransactions_TransactionDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_TransactionDate" ON public."ItemTransactions" USING btree ("TransactionDate");


--
-- Name: IX_ItemTransactions_TransactionNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemTransactions_TransactionNumber" ON public."ItemTransactions" USING btree ("TransactionNumber");


--
-- Name: IX_ItemVendors_ItemId_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ItemVendors_ItemId_VendorId" ON public."ItemVendors" USING btree ("ItemId", "VendorId");


--
-- Name: IX_ItemVendors_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ItemVendors_VendorId" ON public."ItemVendors" USING btree ("VendorId");


--
-- Name: IX_Items_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_CategoryId" ON public."Items" USING btree ("CategoryId");


--
-- Name: IX_Items_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_CompanyId" ON public."Items" USING btree ("CompanyId");


--
-- Name: IX_Items_CurrentReleasedRevisionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_CurrentReleasedRevisionId" ON public."Items" USING btree ("CurrentReleasedRevisionId");


--
-- Name: IX_Items_ImagePath; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_ImagePath" ON public."Items" USING btree ("ImagePath");


--
-- Name: IX_Items_ManufacturerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_ManufacturerId" ON public."Items" USING btree ("ManufacturerId");


--
-- Name: IX_Items_PartNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_PartNumber" ON public."Items" USING btree ("PartNumber");


--
-- Name: IX_Items_PrimaryVendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Items_PrimaryVendorId" ON public."Items" USING btree ("PrimaryVendorId");


--
-- Name: IX_JournalEntries_Batch; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_JournalEntries_Batch" ON public."JournalEntries" USING btree ("Batch");


--
-- Name: IX_JournalEntries_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_JournalEntries_BookId" ON public."JournalEntries" USING btree ("BookId");


--
-- Name: IX_JournalEntries_Period; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_JournalEntries_Period" ON public."JournalEntries" USING btree ("Period");


--
-- Name: IX_JournalLines_JournalEntryId_LineNo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_JournalLines_JournalEntryId_LineNo" ON public."JournalLines" USING btree ("JournalEntryId", "LineNo");


--
-- Name: IX_KitItems_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_KitItems_ItemId" ON public."KitItems" USING btree ("ItemId");


--
-- Name: IX_KitItems_KitId_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_KitItems_KitId_ItemId" ON public."KitItems" USING btree ("KitId", "ItemId");


--
-- Name: IX_Kits_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Kits_CategoryId" ON public."Kits" USING btree ("CategoryId");


--
-- Name: IX_Kits_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Kits_CompanyId" ON public."Kits" USING btree ("CompanyId");


--
-- Name: IX_Kits_KitNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Kits_KitNumber" ON public."Kits" USING btree ("KitNumber");


--
-- Name: IX_LaborRates_CraftId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LaborRates_CraftId" ON public."LaborRates" USING btree ("CraftId");


--
-- Name: IX_LaborRates_SkillId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LaborRates_SkillId" ON public."LaborRates" USING btree ("SkillId");


--
-- Name: IX_LessonsLearned_AssetCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LessonsLearned_AssetCategoryId" ON public."LessonsLearned" USING btree ("AssetCategoryId");


--
-- Name: IX_LessonsLearned_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LessonsLearned_CompanyId" ON public."LessonsLearned" USING btree ("CompanyId");


--
-- Name: IX_LessonsLearned_FailureCode; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LessonsLearned_FailureCode" ON public."LessonsLearned" USING btree ("FailureCode");


--
-- Name: IX_LessonsLearned_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LessonsLearned_SiteId" ON public."LessonsLearned" USING btree ("SiteId");


--
-- Name: IX_LessonsLearned_SourceWorkOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_LessonsLearned_SourceWorkOrderId" ON public."LessonsLearned" USING btree ("SourceWorkOrderId");


--
-- Name: IX_Locations_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Locations_Code" ON public."Locations" USING btree ("Code");


--
-- Name: IX_Locations_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Locations_CompanyId" ON public."Locations" USING btree ("CompanyId");


--
-- Name: IX_Locations_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Locations_CostCenterId" ON public."Locations" USING btree ("CostCenterId");


--
-- Name: IX_Locations_ParentLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Locations_ParentLocationId" ON public."Locations" USING btree ("ParentLocationId");


--
-- Name: IX_Locations_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Locations_SiteId" ON public."Locations" USING btree ("SiteId");


--
-- Name: IX_MaintenanceEvents_ApprovedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_ApprovedById" ON public."MaintenanceEvents" USING btree ("ApprovedById");


--
-- Name: IX_MaintenanceEvents_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_AssetId" ON public."MaintenanceEvents" USING btree ("AssetId");


--
-- Name: IX_MaintenanceEvents_RequestedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_RequestedById" ON public."MaintenanceEvents" USING btree ("RequestedById");


--
-- Name: IX_MaintenanceEvents_ScheduledDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_ScheduledDate" ON public."MaintenanceEvents" USING btree ("ScheduledDate");


--
-- Name: IX_MaintenanceEvents_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_Status" ON public."MaintenanceEvents" USING btree ("Status");


--
-- Name: IX_MaintenanceEvents_TechnicianId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceEvents_TechnicianId" ON public."MaintenanceEvents" USING btree ("TechnicianId");


--
-- Name: IX_MaintenanceSchedules_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceSchedules_AssetId" ON public."MaintenanceSchedules" USING btree ("AssetId");


--
-- Name: IX_MaintenanceSchedules_NextDueDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MaintenanceSchedules_NextDueDate" ON public."MaintenanceSchedules" USING btree ("NextDueDate");


--
-- Name: IX_Manufacturers_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Manufacturers_Active" ON public."Manufacturers" USING btree ("Active");


--
-- Name: IX_Manufacturers_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Manufacturers_Name" ON public."Manufacturers" USING btree ("Name");


--
-- Name: IX_Manufacturers_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Manufacturers_TenantId_Code" ON public."Manufacturers" USING btree ("TenantId", "Code");


--
-- Name: IX_MeterReadings_AssetId_MeterType_ReadingDate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeterReadings_AssetId_MeterType_ReadingDate" ON public."MeterReadings" USING btree ("AssetId", "MeterType", "ReadingDate");


--
-- Name: IX_MeterReadings_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_MeterReadings_CompanyId" ON public."MeterReadings" USING btree ("CompanyId");


--
-- Name: IX_OutboxEvents_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OutboxEvents_CompanyId" ON public."OutboxEvents" USING btree ("CompanyId");


--
-- Name: IX_OutboxEvents_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OutboxEvents_SiteId" ON public."OutboxEvents" USING btree ("SiteId");


--
-- Name: IX_OutboxEvents_Status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OutboxEvents_Status" ON public."OutboxEvents" USING btree ("Status");


--
-- Name: IX_OutboxEvents_Status_NextAttemptAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_OutboxEvents_Status_NextAttemptAt" ON public."OutboxEvents" USING btree ("Status", "NextAttemptAt");


--
-- Name: IX_PMOccurrences_Unique; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PMOccurrences_Unique" ON public."PMOccurrences" USING btree ("TenantId", "CompanyId", "SiteId", "PMTemplateId", "DueDateUtc");


--
-- Name: IX_PMSchedules_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMSchedules_Active" ON public."PMSchedules" USING btree ("TenantId", "CompanyId", "SiteId", "PMTemplateId", "Active");


--
-- Name: IX_PMTemplateAssets_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplateAssets_AssetId" ON public."PMTemplateAssets" USING btree ("AssetId");


--
-- Name: IX_PMTemplateAssets_PMTemplateId_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PMTemplateAssets_PMTemplateId_AssetId" ON public."PMTemplateAssets" USING btree ("PMTemplateId", "AssetId");


--
-- Name: IX_PMTemplateItems_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplateItems_ItemId" ON public."PMTemplateItems" USING btree ("ItemId");


--
-- Name: IX_PMTemplateItems_PMTemplateId_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PMTemplateItems_PMTemplateId_ItemId" ON public."PMTemplateItems" USING btree ("PMTemplateId", "ItemId");


--
-- Name: IX_PMTemplateRevisionOperations_PMTemplateRevisionId_Sequence; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplateRevisionOperations_PMTemplateRevisionId_Sequence" ON public."PMTemplateRevisionOperations" USING btree ("PMTemplateRevisionId", "Sequence");


--
-- Name: IX_PMTemplateRevisions_PMTemplateId_RevisionCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PMTemplateRevisions_PMTemplateId_RevisionCode" ON public."PMTemplateRevisions" USING btree ("PMTemplateId", "RevisionCode");


--
-- Name: IX_PMTemplateRevisions_SupersedesRevisionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplateRevisions_SupersedesRevisionId" ON public."PMTemplateRevisions" USING btree ("SupersedesRevisionId");


--
-- Name: IX_PMTemplates_AssetCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplates_AssetCategoryId" ON public."PMTemplates" USING btree ("AssetCategoryId");


--
-- Name: IX_PMTemplates_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplates_Code" ON public."PMTemplates" USING btree ("Code");


--
-- Name: IX_PMTemplates_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplates_CompanyId" ON public."PMTemplates" USING btree ("CompanyId");


--
-- Name: IX_PMTemplates_CurrentReleasedRevisionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplates_CurrentReleasedRevisionId" ON public."PMTemplates" USING btree ("CurrentReleasedRevisionId");


--
-- Name: IX_PMTemplates_ManufacturerId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PMTemplates_ManufacturerId" ON public."PMTemplates" USING btree ("ManufacturerId");


--
-- Name: IX_PartialDisposals_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PartialDisposals_AssetId" ON public."PartialDisposals" USING btree ("AssetId");


--
-- Name: IX_PeriodLocks_Period; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_PeriodLocks_Period" ON public."PeriodLocks" USING btree ("Period");


--
-- Name: IX_PolicyCategoryDefaults_AssetCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PolicyCategoryDefaults_AssetCategoryId" ON public."PolicyCategoryDefaults" USING btree ("AssetCategoryId");


--
-- Name: IX_PolicyCategoryDefaults_BookId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PolicyCategoryDefaults_BookId" ON public."PolicyCategoryDefaults" USING btree ("BookId");


--
-- Name: IX_PolicyCategoryDefaults_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PolicyCategoryDefaults_CompanyId" ON public."PolicyCategoryDefaults" USING btree ("CompanyId");


--
-- Name: IX_PolicyCategoryDefaults_DepreciationPolicyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PolicyCategoryDefaults_DepreciationPolicyId" ON public."PolicyCategoryDefaults" USING btree ("DepreciationPolicyId");


--
-- Name: IX_ProjectManagers_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectManagers_Active" ON public."ProjectManagers" USING btree ("Active");


--
-- Name: IX_ProjectManagers_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectManagers_CostCenterId" ON public."ProjectManagers" USING btree ("CostCenterId");


--
-- Name: IX_ProjectManagers_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectManagers_DepartmentId" ON public."ProjectManagers" USING btree ("DepartmentId");


--
-- Name: IX_ProjectManagers_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ProjectManagers_Name" ON public."ProjectManagers" USING btree ("Name");


--
-- Name: IX_PurchaseOrderLines_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_AssetId" ON public."PurchaseOrderLines" USING btree ("AssetId");


--
-- Name: IX_PurchaseOrderLines_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_CostCenterId" ON public."PurchaseOrderLines" USING btree ("CostCenterId");


--
-- Name: IX_PurchaseOrderLines_ExpenseCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_ExpenseCategoryId" ON public."PurchaseOrderLines" USING btree ("ExpenseCategoryId");


--
-- Name: IX_PurchaseOrderLines_GlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_GlAccountId" ON public."PurchaseOrderLines" USING btree ("GlAccountId");


--
-- Name: IX_PurchaseOrderLines_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_ItemId" ON public."PurchaseOrderLines" USING btree ("ItemId");


--
-- Name: IX_PurchaseOrderLines_PurchaseOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_PurchaseOrderId" ON public."PurchaseOrderLines" USING btree ("PurchaseOrderId");


--
-- Name: IX_PurchaseOrderLines_ShipToLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderLines_ShipToLocationId" ON public."PurchaseOrderLines" USING btree ("ShipToLocationId");


--
-- Name: IX_PurchaseOrderReleases_PurchaseOrderLineId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderReleases_PurchaseOrderLineId" ON public."PurchaseOrderReleases" USING btree ("PurchaseOrderLineId");


--
-- Name: IX_PurchaseOrderReleases_ShipToLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrderReleases_ShipToLocationId" ON public."PurchaseOrderReleases" USING btree ("ShipToLocationId");


--
-- Name: IX_PurchaseOrders_ApprovedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_ApprovedById" ON public."PurchaseOrders" USING btree ("ApprovedById");


--
-- Name: IX_PurchaseOrders_BillToSiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_BillToSiteId" ON public."PurchaseOrders" USING btree ("BillToSiteId");


--
-- Name: IX_PurchaseOrders_CipProjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_CipProjectId" ON public."PurchaseOrders" USING btree ("CipProjectId");


--
-- Name: IX_PurchaseOrders_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_CompanyId" ON public."PurchaseOrders" USING btree ("CompanyId");


--
-- Name: IX_PurchaseOrders_DefaultShipToLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_DefaultShipToLocationId" ON public."PurchaseOrders" USING btree ("DefaultShipToLocationId");


--
-- Name: IX_PurchaseOrders_RequestedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_RequestedById" ON public."PurchaseOrders" USING btree ("RequestedById");


--
-- Name: IX_PurchaseOrders_ShipToSiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_ShipToSiteId" ON public."PurchaseOrders" USING btree ("ShipToSiteId");


--
-- Name: IX_PurchaseOrders_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_VendorId" ON public."PurchaseOrders" USING btree ("VendorId");


--
-- Name: IX_PurchaseOrders_WorkOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseOrders_WorkOrderId" ON public."PurchaseOrders" USING btree ("WorkOrderId");


--
-- Name: IX_PurchaseRequisitionLines_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_CostCenterId" ON public."PurchaseRequisitionLines" USING btree ("CostCenterId");


--
-- Name: IX_PurchaseRequisitionLines_ExpenseCategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_ExpenseCategoryId" ON public."PurchaseRequisitionLines" USING btree ("ExpenseCategoryId");


--
-- Name: IX_PurchaseRequisitionLines_GlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_GlAccountId" ON public."PurchaseRequisitionLines" USING btree ("GlAccountId");


--
-- Name: IX_PurchaseRequisitionLines_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_ItemId" ON public."PurchaseRequisitionLines" USING btree ("ItemId");


--
-- Name: IX_PurchaseRequisitionLines_RequisitionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_RequisitionId" ON public."PurchaseRequisitionLines" USING btree ("RequisitionId");


--
-- Name: IX_PurchaseRequisitionLines_SuggestedVendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitionLines_SuggestedVendorId" ON public."PurchaseRequisitionLines" USING btree ("SuggestedVendorId");


--
-- Name: IX_PurchaseRequisitions_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitions_CompanyId" ON public."PurchaseRequisitions" USING btree ("CompanyId");


--
-- Name: IX_PurchaseRequisitions_ConvertedToPOId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitions_ConvertedToPOId" ON public."PurchaseRequisitions" USING btree ("ConvertedToPOId");


--
-- Name: IX_PurchaseRequisitions_DeliverToLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitions_DeliverToLocationId" ON public."PurchaseRequisitions" USING btree ("DeliverToLocationId");


--
-- Name: IX_PurchaseRequisitions_DeliverToSiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitions_DeliverToSiteId" ON public."PurchaseRequisitions" USING btree ("DeliverToSiteId");


--
-- Name: IX_PurchaseRequisitions_SuggestedVendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_PurchaseRequisitions_SuggestedVendorId" ON public."PurchaseRequisitions" USING btree ("SuggestedVendorId");


--
-- Name: IX_ReorderAlerts_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ReorderAlerts_CompanyId" ON public."ReorderAlerts" USING btree ("CompanyId");


--
-- Name: IX_ReorderAlerts_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ReorderAlerts_ItemId" ON public."ReorderAlerts" USING btree ("ItemId");


--
-- Name: IX_ReorderAlerts_RequisitionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ReorderAlerts_RequisitionId" ON public."ReorderAlerts" USING btree ("RequisitionId");


--
-- Name: IX_Section179Limits_TaxYear; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Section179Limits_TaxYear" ON public."Section179Limits" USING btree ("TaxYear");


--
-- Name: IX_Sites_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Sites_CompanyId" ON public."Sites" USING btree ("CompanyId");


--
-- Name: IX_Skills_CraftId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Skills_CraftId" ON public."Skills" USING btree ("CraftId");


--
-- Name: IX_Technicians_Active; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Technicians_Active" ON public."Technicians" USING btree ("Active");


--
-- Name: IX_Technicians_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Technicians_CostCenterId" ON public."Technicians" USING btree ("CostCenterId");


--
-- Name: IX_Technicians_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Technicians_DepartmentId" ON public."Technicians" USING btree ("DepartmentId");


--
-- Name: IX_Technicians_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Technicians_Name" ON public."Technicians" USING btree ("Name");


--
-- Name: IX_UsTaxSettings_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_UsTaxSettings_AssetId" ON public."UsTaxSettings" USING btree ("AssetId");


--
-- Name: IX_UsefulLifeEntries_UsefulLifeTableId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_UsefulLifeEntries_UsefulLifeTableId" ON public."UsefulLifeEntries" USING btree ("UsefulLifeTableId");


--
-- Name: IX_Users_Email; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Users_Email" ON public."Users" USING btree ("Email");


--
-- Name: IX_Users_Username; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Users_Username" ON public."Users" USING btree ("Username");


--
-- Name: IX_VendorInvoiceLines_CostCenterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoiceLines_CostCenterId" ON public."VendorInvoiceLines" USING btree ("CostCenterId");


--
-- Name: IX_VendorInvoiceLines_GlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoiceLines_GlAccountId" ON public."VendorInvoiceLines" USING btree ("GlAccountId");


--
-- Name: IX_VendorInvoiceLines_GoodsReceiptLineId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoiceLines_GoodsReceiptLineId" ON public."VendorInvoiceLines" USING btree ("GoodsReceiptLineId");


--
-- Name: IX_VendorInvoiceLines_PurchaseOrderLineId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoiceLines_PurchaseOrderLineId" ON public."VendorInvoiceLines" USING btree ("PurchaseOrderLineId");


--
-- Name: IX_VendorInvoiceLines_VendorInvoiceId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoiceLines_VendorInvoiceId" ON public."VendorInvoiceLines" USING btree ("VendorInvoiceId");


--
-- Name: IX_VendorInvoices_ApprovedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoices_ApprovedById" ON public."VendorInvoices" USING btree ("ApprovedById");


--
-- Name: IX_VendorInvoices_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoices_CompanyId" ON public."VendorInvoices" USING btree ("CompanyId");


--
-- Name: IX_VendorInvoices_VendorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorInvoices_VendorId" ON public."VendorInvoices" USING btree ("VendorId");


--
-- Name: IX_VendorItemParts_CatalogUrl; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorItemParts_CatalogUrl" ON public."VendorItemParts" USING btree ("CatalogUrl");


--
-- Name: IX_VendorItemParts_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorItemParts_ItemId" ON public."VendorItemParts" USING btree ("ItemId");


--
-- Name: IX_VendorItemParts_ItemManufacturerPartId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorItemParts_ItemManufacturerPartId" ON public."VendorItemParts" USING btree ("ItemManufacturerPartId");


--
-- Name: IX_VendorItemParts_VendorId_VendorPartNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_VendorItemParts_VendorId_VendorPartNumber" ON public."VendorItemParts" USING btree ("VendorId", "VendorPartNumber");


--
-- Name: IX_VendorItemParts_VendorPartNumber; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_VendorItemParts_VendorPartNumber" ON public."VendorItemParts" USING btree ("VendorPartNumber");


--
-- Name: IX_Vendors_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Vendors_CompanyId" ON public."Vendors" USING btree ("CompanyId");


--
-- Name: IX_Vendors_DefaultGlAccountId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Vendors_DefaultGlAccountId" ON public."Vendors" USING btree ("DefaultGlAccountId");


--
-- Name: IX_WebhookDeliveryLogs_OutboxEventId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WebhookDeliveryLogs_OutboxEventId" ON public."WebhookDeliveryLogs" USING btree ("OutboxEventId");


--
-- Name: IX_WebhookDeliveryLogs_WebhookSubscriptionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WebhookDeliveryLogs_WebhookSubscriptionId" ON public."WebhookDeliveryLogs" USING btree ("WebhookSubscriptionId");


--
-- Name: IX_WebhookSubscriptions_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WebhookSubscriptions_CompanyId" ON public."WebhookSubscriptions" USING btree ("CompanyId");


--
-- Name: IX_WebhookSubscriptions_IsActive; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WebhookSubscriptions_IsActive" ON public."WebhookSubscriptions" USING btree ("IsActive");


--
-- Name: IX_WorkOrderParts_IssuedFromLocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkOrderParts_IssuedFromLocationId" ON public."WorkOrderParts" USING btree ("IssuedFromLocationId");


--
-- Name: IX_WorkOrderParts_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkOrderParts_ItemId" ON public."WorkOrderParts" USING btree ("ItemId");


--
-- Name: IX_WorkOrderParts_MaintenanceEventId_ItemId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkOrderParts_MaintenanceEventId_ItemId" ON public."WorkOrderParts" USING btree ("MaintenanceEventId", "ItemId");


--
-- Name: IX_WorkRequests_AssetId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkRequests_AssetId" ON public."WorkRequests" USING btree ("AssetId");


--
-- Name: IX_WorkRequests_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkRequests_CompanyId" ON public."WorkRequests" USING btree ("CompanyId");


--
-- Name: IX_WorkRequests_GeneratedWorkOrderId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkRequests_GeneratedWorkOrderId" ON public."WorkRequests" USING btree ("GeneratedWorkOrderId");


--
-- Name: IX_WorkRequests_LocationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkRequests_LocationId" ON public."WorkRequests" USING btree ("LocationId");


--
-- Name: IX_WorkRequests_SiteId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkRequests_SiteId" ON public."WorkRequests" USING btree ("SiteId");


--
-- Name: Companies Companies_TenantId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Companies"
    ADD CONSTRAINT "Companies_TenantId_fkey" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id");


--
-- Name: AssetBookSettings FK_AssetBookSettings_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetBookSettings"
    ADD CONSTRAINT "FK_AssetBookSettings_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: AssetBookSettings FK_AssetBookSettings_Books_BookId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetBookSettings"
    ADD CONSTRAINT "FK_AssetBookSettings_Books_BookId" FOREIGN KEY ("BookId") REFERENCES public."Books"("Id") ON DELETE RESTRICT;


--
-- Name: AssetBookSettings FK_AssetBookSettings_Books_BookId1; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetBookSettings"
    ADD CONSTRAINT "FK_AssetBookSettings_Books_BookId1" FOREIGN KEY ("BookId1") REFERENCES public."Books"("Id");


--
-- Name: AssetCategories FK_AssetCategories_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "FK_AssetCategories_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: AssetCategories FK_AssetCategories_DepreciationPolicies_DepreciationPolicyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "FK_AssetCategories_DepreciationPolicies_DepreciationPolicyId" FOREIGN KEY ("DepreciationPolicyId") REFERENCES public."DepreciationPolicies"("Id");


--
-- Name: AssetCategories FK_AssetCategories_GlAccounts_AccumDepGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "FK_AssetCategories_GlAccounts_AccumDepGlAccountId" FOREIGN KEY ("AccumDepGlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: AssetCategories FK_AssetCategories_GlAccounts_AssetGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "FK_AssetCategories_GlAccounts_AssetGlAccountId" FOREIGN KEY ("AssetGlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: AssetCategories FK_AssetCategories_GlAccounts_DepExpGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetCategories"
    ADD CONSTRAINT "FK_AssetCategories_GlAccounts_DepExpGlAccountId" FOREIGN KEY ("DepExpGlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: AssetInventories FK_AssetInventories_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetInventories"
    ADD CONSTRAINT "FK_AssetInventories_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: AssetInventories FK_AssetInventories_InventoryLists_LastInventoryListId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetInventories"
    ADD CONSTRAINT "FK_AssetInventories_InventoryLists_LastInventoryListId" FOREIGN KEY ("LastInventoryListId") REFERENCES public."InventoryLists"("Id") ON DELETE SET NULL;


--
-- Name: AssetTaxSettings FK_AssetTaxSettings_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetTaxSettings"
    ADD CONSTRAINT "FK_AssetTaxSettings_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: AssetTaxSettings FK_AssetTaxSettings_CcaClasses_CcaClassId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetTaxSettings"
    ADD CONSTRAINT "FK_AssetTaxSettings_CcaClasses_CcaClassId" FOREIGN KEY ("CcaClassId") REFERENCES public."CcaClasses"("Id") ON DELETE RESTRICT;


--
-- Name: AssetTransfers FK_AssetTransfers_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AssetTransfers"
    ADD CONSTRAINT "FK_AssetTransfers_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: Assets FK_Assets_AssetCategories_AssetCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_AssetCategories_AssetCategoryId" FOREIGN KEY ("AssetCategoryId") REFERENCES public."AssetCategories"("Id") ON DELETE SET NULL;


--
-- Name: Assets FK_Assets_Assets_ParentAssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Assets_ParentAssetId" FOREIGN KEY ("ParentAssetId") REFERENCES public."Assets"("Id");


--
-- Name: Assets FK_Assets_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE RESTRICT;


--
-- Name: Assets FK_Assets_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: Assets FK_Assets_Departments_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES public."Departments"("Id") ON DELETE SET NULL;


--
-- Name: Assets FK_Assets_Locations_LocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES public."Locations"("Id") ON DELETE SET NULL;


--
-- Name: Assets FK_Assets_Manufacturers_ManufacturerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Manufacturers_ManufacturerId" FOREIGN KEY ("ManufacturerId") REFERENCES public."Manufacturers"("Id") ON DELETE SET NULL;


--
-- Name: Assets FK_Assets_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id");


--
-- Name: Assets FK_Assets_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Assets"
    ADD CONSTRAINT "FK_Assets_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id");


--
-- Name: Attachments FK_Attachments_AssetTransfers_AssetTransferId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_AssetTransfers_AssetTransferId" FOREIGN KEY ("AssetTransferId") REFERENCES public."AssetTransfers"("Id") ON DELETE SET NULL;


--
-- Name: Attachments FK_Attachments_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: Attachments FK_Attachments_CapitalImprovements_CapitalImprovementId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_CapitalImprovements_CapitalImprovementId" FOREIGN KEY ("CapitalImprovementId") REFERENCES public."CapitalImprovements"("Id") ON DELETE SET NULL;


--
-- Name: Attachments FK_Attachments_CipCosts_CipCostId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_CipCosts_CipCostId" FOREIGN KEY ("CipCostId") REFERENCES public."CipCosts"("Id") ON DELETE SET NULL;


--
-- Name: Attachments FK_Attachments_CipProjects_CipProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_CipProjects_CipProjectId" FOREIGN KEY ("CipProjectId") REFERENCES public."CipProjects"("Id") ON DELETE SET NULL;


--
-- Name: Attachments FK_Attachments_MaintenanceEvents_MaintenanceEventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachments"
    ADD CONSTRAINT "FK_Attachments_MaintenanceEvents_MaintenanceEventId" FOREIGN KEY ("MaintenanceEventId") REFERENCES public."MaintenanceEvents"("Id") ON DELETE SET NULL;


--
-- Name: BookGlAccounts FK_BookGlAccounts_Books_BookId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BookGlAccounts"
    ADD CONSTRAINT "FK_BookGlAccounts_Books_BookId" FOREIGN KEY ("BookId") REFERENCES public."Books"("Id") ON DELETE CASCADE;


--
-- Name: Books FK_Books_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Books"
    ADD CONSTRAINT "FK_Books_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE RESTRICT;


--
-- Name: Books FK_Books_DepreciationPolicies_DefaultPolicyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Books"
    ADD CONSTRAINT "FK_Books_DepreciationPolicies_DefaultPolicyId" FOREIGN KEY ("DefaultPolicyId") REFERENCES public."DepreciationPolicies"("Id");


--
-- Name: CapitalImprovements FK_CapitalImprovements_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CapitalImprovements"
    ADD CONSTRAINT "FK_CapitalImprovements_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: CauseCodes FK_CauseCodes_CauseCodes_ParentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CauseCodes"
    ADD CONSTRAINT "FK_CauseCodes_CauseCodes_ParentId" FOREIGN KEY ("ParentId") REFERENCES public."CauseCodes"("Id");


--
-- Name: CcaClassBalances FK_CcaClassBalances_CcaClasses_CcaClassId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaClassBalances"
    ADD CONSTRAINT "FK_CcaClassBalances_CcaClasses_CcaClassId" FOREIGN KEY ("CcaClassId") REFERENCES public."CcaClasses"("Id") ON DELETE RESTRICT;


--
-- Name: CcaTransactions FK_CcaTransactions_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaTransactions"
    ADD CONSTRAINT "FK_CcaTransactions_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE SET NULL;


--
-- Name: CcaTransactions FK_CcaTransactions_CcaClasses_CcaClassId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CcaTransactions"
    ADD CONSTRAINT "FK_CcaTransactions_CcaClasses_CcaClassId" FOREIGN KEY ("CcaClassId") REFERENCES public."CcaClasses"("Id") ON DELETE RESTRICT;


--
-- Name: CipCosts FK_CipCosts_CipProjects_CipProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipCosts"
    ADD CONSTRAINT "FK_CipCosts_CipProjects_CipProjectId" FOREIGN KEY ("CipProjectId") REFERENCES public."CipProjects"("Id") ON DELETE CASCADE;


--
-- Name: CipProjects FK_CipProjects_Assets_ConvertedAssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "FK_CipProjects_Assets_ConvertedAssetId" FOREIGN KEY ("ConvertedAssetId") REFERENCES public."Assets"("Id") ON DELETE SET NULL;


--
-- Name: CipProjects FK_CipProjects_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "FK_CipProjects_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: CipProjects FK_CipProjects_Departments_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "FK_CipProjects_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES public."Departments"("Id") ON DELETE SET NULL;


--
-- Name: CipProjects FK_CipProjects_GlAccounts_GlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "FK_CipProjects_GlAccounts_GlAccountId" FOREIGN KEY ("GlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: CipProjects FK_CipProjects_ProjectManagers_ProjectManagerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CipProjects"
    ADD CONSTRAINT "FK_CipProjects_ProjectManagers_ProjectManagerId" FOREIGN KEY ("ProjectManagerId") REFERENCES public."ProjectManagers"("Id") ON DELETE SET NULL;


--
-- Name: Companies FK_Companies_Companies_ParentCompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Companies"
    ADD CONSTRAINT "FK_Companies_Companies_ParentCompanyId" FOREIGN KEY ("ParentCompanyId") REFERENCES public."Companies"("Id") ON DELETE RESTRICT;


--
-- Name: CostCenters FK_CostCenters_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CostCenters"
    ADD CONSTRAINT "FK_CostCenters_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: CostCenters FK_CostCenters_CostCenters_ParentCostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."CostCenters"
    ADD CONSTRAINT "FK_CostCenters_CostCenters_ParentCostCenterId" FOREIGN KEY ("ParentCostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE RESTRICT;


--
-- Name: Departments FK_Departments_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Departments"
    ADD CONSTRAINT "FK_Departments_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: Departments FK_Departments_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Departments"
    ADD CONSTRAINT "FK_Departments_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: DepreciationPolicies FK_DepreciationPolicies_CcaClasses_CcaClassId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationPolicies"
    ADD CONSTRAINT "FK_DepreciationPolicies_CcaClasses_CcaClassId" FOREIGN KEY ("CcaClassId") REFERENCES public."CcaClasses"("Id");


--
-- Name: DepreciationPolicies FK_DepreciationPolicies_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationPolicies"
    ADD CONSTRAINT "FK_DepreciationPolicies_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: DepreciationRunDetails FK_DepreciationRunDetails_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRunDetails"
    ADD CONSTRAINT "FK_DepreciationRunDetails_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: DepreciationRunDetails FK_DepreciationRunDetails_DepreciationRuns_DepreciationRunId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRunDetails"
    ADD CONSTRAINT "FK_DepreciationRunDetails_DepreciationRuns_DepreciationRunId" FOREIGN KEY ("DepreciationRunId") REFERENCES public."DepreciationRuns"("Id") ON DELETE CASCADE;


--
-- Name: DepreciationRuns FK_DepreciationRuns_Books_BookId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRuns"
    ADD CONSTRAINT "FK_DepreciationRuns_Books_BookId" FOREIGN KEY ("BookId") REFERENCES public."Books"("Id") ON DELETE CASCADE;


--
-- Name: DepreciationRuns FK_DepreciationRuns_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRuns"
    ADD CONSTRAINT "FK_DepreciationRuns_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: DepreciationRuns FK_DepreciationRuns_FiscalPeriods_FiscalPeriodId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DepreciationRuns"
    ADD CONSTRAINT "FK_DepreciationRuns_FiscalPeriods_FiscalPeriodId" FOREIGN KEY ("FiscalPeriodId") REFERENCES public."FiscalPeriods"("Id") ON DELETE CASCADE;


--
-- Name: FailureCodes FK_FailureCodes_FailureCodes_ParentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FailureCodes"
    ADD CONSTRAINT "FK_FailureCodes_FailureCodes_ParentId" FOREIGN KEY ("ParentId") REFERENCES public."FailureCodes"("Id");


--
-- Name: FiscalPeriods FK_FiscalPeriods_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FiscalPeriods"
    ADD CONSTRAINT "FK_FiscalPeriods_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: FiscalPeriods FK_FiscalPeriods_FiscalYears_FiscalYearId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FiscalPeriods"
    ADD CONSTRAINT "FK_FiscalPeriods_FiscalYears_FiscalYearId" FOREIGN KEY ("FiscalYearId") REFERENCES public."FiscalYears"("Id") ON DELETE CASCADE;


--
-- Name: FiscalYears FK_FiscalYears_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."FiscalYears"
    ADD CONSTRAINT "FK_FiscalYears_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: GlAccounts FK_GlAccounts_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GlAccounts"
    ADD CONSTRAINT "FK_GlAccounts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: GlAccounts FK_GlAccounts_GlAccounts_ParentAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GlAccounts"
    ADD CONSTRAINT "FK_GlAccounts_GlAccounts_ParentAccountId" FOREIGN KEY ("ParentAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE RESTRICT;


--
-- Name: GoodsReceiptLines FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceiptLines"
    ADD CONSTRAINT "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId" FOREIGN KEY ("GoodsReceiptId") REFERENCES public."GoodsReceipts"("Id") ON DELETE CASCADE;


--
-- Name: GoodsReceiptLines FK_GoodsReceiptLines_Locations_ReceivingLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceiptLines"
    ADD CONSTRAINT "FK_GoodsReceiptLines_Locations_ReceivingLocationId" FOREIGN KEY ("ReceivingLocationId") REFERENCES public."Locations"("Id");


--
-- Name: GoodsReceiptLines FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceiptLines"
    ADD CONSTRAINT "FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES public."PurchaseOrderLines"("Id") ON DELETE CASCADE;


--
-- Name: GoodsReceipts FK_GoodsReceipts_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceipts"
    ADD CONSTRAINT "FK_GoodsReceipts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: GoodsReceipts FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."GoodsReceipts"
    ADD CONSTRAINT "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES public."PurchaseOrders"("Id") ON DELETE CASCADE;


--
-- Name: InboundEvents FK_InboundEvents_Companies_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InboundEvents"
    ADD CONSTRAINT "FK_InboundEvents_Companies_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Companies"("Id");


--
-- Name: InboundEvents FK_InboundEvents_IntegrationEndpoints_IntegrationEndpointId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InboundEvents"
    ADD CONSTRAINT "FK_InboundEvents_IntegrationEndpoints_IntegrationEndpointId" FOREIGN KEY ("IntegrationEndpointId") REFERENCES public."IntegrationEndpoints"("Id") ON DELETE CASCADE;


--
-- Name: IntegrationEndpoints FK_IntegrationEndpoints_Tenants_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IntegrationEndpoints"
    ADD CONSTRAINT "FK_IntegrationEndpoints_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id");


--
-- Name: IntegrationMappings FK_IntegrationMappings_IntegrationEndpoints_IntegrationEndpoin~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."IntegrationMappings"
    ADD CONSTRAINT "FK_IntegrationMappings_IntegrationEndpoints_IntegrationEndpoin~" FOREIGN KEY ("IntegrationEndpointId") REFERENCES public."IntegrationEndpoints"("Id") ON DELETE CASCADE;


--
-- Name: InventoryScans FK_InventoryScans_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InventoryScans"
    ADD CONSTRAINT "FK_InventoryScans_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE SET NULL;


--
-- Name: InventoryScans FK_InventoryScans_InventoryLists_InventoryListId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InventoryScans"
    ADD CONSTRAINT "FK_InventoryScans_InventoryLists_InventoryListId" FOREIGN KEY ("InventoryListId") REFERENCES public."InventoryLists"("Id") ON DELETE CASCADE;


--
-- Name: InvoicePayments FK_InvoicePayments_VendorInvoices_VendorInvoiceId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."InvoicePayments"
    ADD CONSTRAINT "FK_InvoicePayments_VendorInvoices_VendorInvoiceId" FOREIGN KEY ("VendorInvoiceId") REFERENCES public."VendorInvoices"("Id") ON DELETE CASCADE;


--
-- Name: ItemAlternates FK_ItemAlternates_Items_AlternateItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemAlternates"
    ADD CONSTRAINT "FK_ItemAlternates_Items_AlternateItemId" FOREIGN KEY ("AlternateItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemAlternates FK_ItemAlternates_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemAlternates"
    ADD CONSTRAINT "FK_ItemAlternates_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemAlternates FK_ItemAlternates_Tenants_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemAlternates"
    ADD CONSTRAINT "FK_ItemAlternates_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id") ON DELETE SET NULL;


--
-- Name: ItemAlternates FK_ItemAlternates_Users_CreatedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemAlternates"
    ADD CONSTRAINT "FK_ItemAlternates_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES public."Users"("Id") ON DELETE SET NULL;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE SET NULL;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id") ON DELETE SET NULL;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Tenants_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id") ON DELETE SET NULL;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Users_CreatedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES public."Users"("Id") ON DELETE SET NULL;


--
-- Name: ItemApprovedVendors FK_ItemApprovedVendors_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemApprovedVendors"
    ADD CONSTRAINT "FK_ItemApprovedVendors_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id") ON DELETE CASCADE;


--
-- Name: ItemCategories FK_ItemCategories_GlAccounts_DefaultGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCategories"
    ADD CONSTRAINT "FK_ItemCategories_GlAccounts_DefaultGlAccountId" FOREIGN KEY ("DefaultGlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: ItemCategories FK_ItemCategories_GlAccounts_ExpenseGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCategories"
    ADD CONSTRAINT "FK_ItemCategories_GlAccounts_ExpenseGlAccountId" FOREIGN KEY ("ExpenseGlAccountId") REFERENCES public."GlAccounts"("Id") ON DELETE SET NULL;


--
-- Name: ItemCategories FK_ItemCategories_ItemCategories_ParentCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCategories"
    ADD CONSTRAINT "FK_ItemCategories_ItemCategories_ParentCategoryId" FOREIGN KEY ("ParentCategoryId") REFERENCES public."ItemCategories"("Id") ON DELETE RESTRICT;


--
-- Name: ItemCompanyStockings FK_ItemCompanyStockings_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCompanyStockings"
    ADD CONSTRAINT "FK_ItemCompanyStockings_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: ItemCompanyStockings FK_ItemCompanyStockings_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCompanyStockings"
    ADD CONSTRAINT "FK_ItemCompanyStockings_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemCompanyStockings FK_ItemCompanyStockings_Vendors_PreferredVendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemCompanyStockings"
    ADD CONSTRAINT "FK_ItemCompanyStockings_Vendors_PreferredVendorId" FOREIGN KEY ("PreferredVendorId") REFERENCES public."Vendors"("Id");


--
-- Name: ItemImages FK_ItemImages_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemImages"
    ADD CONSTRAINT "FK_ItemImages_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemInventories2 FK_ItemInventories2_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemInventories2"
    ADD CONSTRAINT "FK_ItemInventories2_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: ItemInventories2 FK_ItemInventories2_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemInventories2"
    ADD CONSTRAINT "FK_ItemInventories2_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemInventories2 FK_ItemInventories2_Locations_LocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemInventories2"
    ADD CONSTRAINT "FK_ItemInventories2_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES public."Locations"("Id") ON DELETE SET NULL;


--
-- Name: ItemManufacturerParts FK_ItemManufacturerParts_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemManufacturerParts"
    ADD CONSTRAINT "FK_ItemManufacturerParts_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemManufacturerParts FK_ItemManufacturerParts_Manufacturers_ManufacturerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemManufacturerParts"
    ADD CONSTRAINT "FK_ItemManufacturerParts_Manufacturers_ManufacturerId" FOREIGN KEY ("ManufacturerId") REFERENCES public."Manufacturers"("Id") ON DELETE RESTRICT;


--
-- Name: ItemRevisions FK_ItemRevisions_ItemRevisions_SupersedesItemRevisionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemRevisions"
    ADD CONSTRAINT "FK_ItemRevisions_ItemRevisions_SupersedesItemRevisionId" FOREIGN KEY ("SupersedesItemRevisionId") REFERENCES public."ItemRevisions"("Id") ON DELETE SET NULL;


--
-- Name: ItemRevisions FK_ItemRevisions_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemRevisions"
    ADD CONSTRAINT "FK_ItemRevisions_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemSupersessions FK_ItemSupersessions_Items_NewItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemSupersessions"
    ADD CONSTRAINT "FK_ItemSupersessions_Items_NewItemId" FOREIGN KEY ("NewItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemSupersessions FK_ItemSupersessions_Items_OldItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemSupersessions"
    ADD CONSTRAINT "FK_ItemSupersessions_Items_OldItemId" FOREIGN KEY ("OldItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemSupersessions FK_ItemSupersessions_Tenants_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemSupersessions"
    ADD CONSTRAINT "FK_ItemSupersessions_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id") ON DELETE SET NULL;


--
-- Name: ItemSupersessions FK_ItemSupersessions_Users_CreatedByUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemSupersessions"
    ADD CONSTRAINT "FK_ItemSupersessions_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES public."Users"("Id") ON DELETE SET NULL;


--
-- Name: ItemTransactions FK_ItemTransactions_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "FK_ItemTransactions_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: ItemTransactions FK_ItemTransactions_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "FK_ItemTransactions_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE RESTRICT;


--
-- Name: ItemTransactions FK_ItemTransactions_Locations_FromLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "FK_ItemTransactions_Locations_FromLocationId" FOREIGN KEY ("FromLocationId") REFERENCES public."Locations"("Id") ON DELETE SET NULL;


--
-- Name: ItemTransactions FK_ItemTransactions_Locations_ToLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "FK_ItemTransactions_Locations_ToLocationId" FOREIGN KEY ("ToLocationId") REFERENCES public."Locations"("Id") ON DELETE SET NULL;


--
-- Name: ItemTransactions FK_ItemTransactions_PurchaseOrders_PurchaseOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemTransactions"
    ADD CONSTRAINT "FK_ItemTransactions_PurchaseOrders_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES public."PurchaseOrders"("Id") ON DELETE SET NULL;


--
-- Name: ItemVendors FK_ItemVendors_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemVendors"
    ADD CONSTRAINT "FK_ItemVendors_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ItemVendors FK_ItemVendors_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ItemVendors"
    ADD CONSTRAINT "FK_ItemVendors_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id") ON DELETE CASCADE;


--
-- Name: Items FK_Items_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "FK_Items_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: Items FK_Items_ItemCategories_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "FK_Items_ItemCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES public."ItemCategories"("Id") ON DELETE SET NULL;


--
-- Name: Items FK_Items_ItemRevisions_CurrentReleasedRevisionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "FK_Items_ItemRevisions_CurrentReleasedRevisionId" FOREIGN KEY ("CurrentReleasedRevisionId") REFERENCES public."ItemRevisions"("Id") ON DELETE SET NULL;


--
-- Name: Items FK_Items_Manufacturers_ManufacturerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "FK_Items_Manufacturers_ManufacturerId" FOREIGN KEY ("ManufacturerId") REFERENCES public."Manufacturers"("Id") ON DELETE SET NULL;


--
-- Name: Items FK_Items_Vendors_PrimaryVendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Items"
    ADD CONSTRAINT "FK_Items_Vendors_PrimaryVendorId" FOREIGN KEY ("PrimaryVendorId") REFERENCES public."Vendors"("Id") ON DELETE SET NULL;


--
-- Name: JournalEntries FK_JournalEntries_Books_BookId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."JournalEntries"
    ADD CONSTRAINT "FK_JournalEntries_Books_BookId" FOREIGN KEY ("BookId") REFERENCES public."Books"("Id") ON DELETE RESTRICT;


--
-- Name: JournalLines FK_JournalLines_JournalEntries_JournalEntryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."JournalLines"
    ADD CONSTRAINT "FK_JournalLines_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES public."JournalEntries"("Id") ON DELETE CASCADE;


--
-- Name: KitItems FK_KitItems_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."KitItems"
    ADD CONSTRAINT "FK_KitItems_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: KitItems FK_KitItems_Kits_KitId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."KitItems"
    ADD CONSTRAINT "FK_KitItems_Kits_KitId" FOREIGN KEY ("KitId") REFERENCES public."Kits"("Id") ON DELETE CASCADE;


--
-- Name: Kits FK_Kits_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Kits"
    ADD CONSTRAINT "FK_Kits_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: Kits FK_Kits_ItemCategories_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Kits"
    ADD CONSTRAINT "FK_Kits_ItemCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES public."ItemCategories"("Id") ON DELETE SET NULL;


--
-- Name: LaborRates FK_LaborRates_Crafts_CraftId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LaborRates"
    ADD CONSTRAINT "FK_LaborRates_Crafts_CraftId" FOREIGN KEY ("CraftId") REFERENCES public."Crafts"("Id");


--
-- Name: LaborRates FK_LaborRates_Skills_SkillId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LaborRates"
    ADD CONSTRAINT "FK_LaborRates_Skills_SkillId" FOREIGN KEY ("SkillId") REFERENCES public."Skills"("Id");


--
-- Name: LessonsLearned FK_LessonsLearned_AssetCategories_AssetCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LessonsLearned"
    ADD CONSTRAINT "FK_LessonsLearned_AssetCategories_AssetCategoryId" FOREIGN KEY ("AssetCategoryId") REFERENCES public."AssetCategories"("Id") ON DELETE SET NULL;


--
-- Name: LessonsLearned FK_LessonsLearned_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LessonsLearned"
    ADD CONSTRAINT "FK_LessonsLearned_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: LessonsLearned FK_LessonsLearned_MaintenanceEvents_SourceWorkOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LessonsLearned"
    ADD CONSTRAINT "FK_LessonsLearned_MaintenanceEvents_SourceWorkOrderId" FOREIGN KEY ("SourceWorkOrderId") REFERENCES public."MaintenanceEvents"("Id") ON DELETE SET NULL;


--
-- Name: LessonsLearned FK_LessonsLearned_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."LessonsLearned"
    ADD CONSTRAINT "FK_LessonsLearned_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id") ON DELETE SET NULL;


--
-- Name: Locations FK_Locations_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Locations"
    ADD CONSTRAINT "FK_Locations_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: Locations FK_Locations_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Locations"
    ADD CONSTRAINT "FK_Locations_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: Locations FK_Locations_Locations_ParentLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Locations"
    ADD CONSTRAINT "FK_Locations_Locations_ParentLocationId" FOREIGN KEY ("ParentLocationId") REFERENCES public."Locations"("Id") ON DELETE RESTRICT;


--
-- Name: Locations FK_Locations_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Locations"
    ADD CONSTRAINT "FK_Locations_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id");


--
-- Name: MaintenanceEvents FK_MaintenanceEvents_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceEvents"
    ADD CONSTRAINT "FK_MaintenanceEvents_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: MaintenanceEvents FK_MaintenanceEvents_Technicians_TechnicianId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceEvents"
    ADD CONSTRAINT "FK_MaintenanceEvents_Technicians_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES public."Technicians"("Id") ON DELETE SET NULL;


--
-- Name: MaintenanceEvents FK_MaintenanceEvents_Users_ApprovedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceEvents"
    ADD CONSTRAINT "FK_MaintenanceEvents_Users_ApprovedById" FOREIGN KEY ("ApprovedById") REFERENCES public."Users"("Id");


--
-- Name: MaintenanceEvents FK_MaintenanceEvents_Users_RequestedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceEvents"
    ADD CONSTRAINT "FK_MaintenanceEvents_Users_RequestedById" FOREIGN KEY ("RequestedById") REFERENCES public."Users"("Id");


--
-- Name: MaintenanceSchedules FK_MaintenanceSchedules_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MaintenanceSchedules"
    ADD CONSTRAINT "FK_MaintenanceSchedules_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: Manufacturers FK_Manufacturers_Tenants_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Manufacturers"
    ADD CONSTRAINT "FK_Manufacturers_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id");


--
-- Name: MeterReadings FK_MeterReadings_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeterReadings"
    ADD CONSTRAINT "FK_MeterReadings_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: MeterReadings FK_MeterReadings_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."MeterReadings"
    ADD CONSTRAINT "FK_MeterReadings_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: OutboxEvents FK_OutboxEvents_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OutboxEvents"
    ADD CONSTRAINT "FK_OutboxEvents_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: OutboxEvents FK_OutboxEvents_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OutboxEvents"
    ADD CONSTRAINT "FK_OutboxEvents_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id") ON DELETE SET NULL;


--
-- Name: PMOccurrences FK_PMOccurrences_Companies; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "FK_PMOccurrences_Companies" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: PMOccurrences FK_PMOccurrences_MaintenanceEvents; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "FK_PMOccurrences_MaintenanceEvents" FOREIGN KEY ("WorkOrderId") REFERENCES public."MaintenanceEvents"("Id");


--
-- Name: PMOccurrences FK_PMOccurrences_PMSchedules; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "FK_PMOccurrences_PMSchedules" FOREIGN KEY ("PMScheduleId") REFERENCES public."PMSchedules"("Id");


--
-- Name: PMOccurrences FK_PMOccurrences_PMTemplates; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "FK_PMOccurrences_PMTemplates" FOREIGN KEY ("PMTemplateId") REFERENCES public."PMTemplates"("Id");


--
-- Name: PMOccurrences FK_PMOccurrences_Sites; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMOccurrences"
    ADD CONSTRAINT "FK_PMOccurrences_Sites" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id");


--
-- Name: PMSchedules FK_PMSchedules_Companies; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMSchedules"
    ADD CONSTRAINT "FK_PMSchedules_Companies" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: PMSchedules FK_PMSchedules_PMTemplates; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMSchedules"
    ADD CONSTRAINT "FK_PMSchedules_PMTemplates" FOREIGN KEY ("PMTemplateId") REFERENCES public."PMTemplates"("Id");


--
-- Name: PMSchedules FK_PMSchedules_Sites; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMSchedules"
    ADD CONSTRAINT "FK_PMSchedules_Sites" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id");


--
-- Name: PMTemplateAssets FK_PMTemplateAssets_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateAssets"
    ADD CONSTRAINT "FK_PMTemplateAssets_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplateAssets FK_PMTemplateAssets_PMTemplates_PMTemplateId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateAssets"
    ADD CONSTRAINT "FK_PMTemplateAssets_PMTemplates_PMTemplateId" FOREIGN KEY ("PMTemplateId") REFERENCES public."PMTemplates"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplateItems FK_PMTemplateItems_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateItems"
    ADD CONSTRAINT "FK_PMTemplateItems_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplateItems FK_PMTemplateItems_PMTemplates_PMTemplateId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateItems"
    ADD CONSTRAINT "FK_PMTemplateItems_PMTemplates_PMTemplateId" FOREIGN KEY ("PMTemplateId") REFERENCES public."PMTemplates"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplateRevisionOperations FK_PMTemplateRevisionOperations_PMTemplateRevisions_PMTemplateR; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateRevisionOperations"
    ADD CONSTRAINT "FK_PMTemplateRevisionOperations_PMTemplateRevisions_PMTemplateR" FOREIGN KEY ("PMTemplateRevisionId") REFERENCES public."PMTemplateRevisions"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplateRevisions FK_PMTemplateRevisions_PMTemplateRevisions_SupersedesRevisionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateRevisions"
    ADD CONSTRAINT "FK_PMTemplateRevisions_PMTemplateRevisions_SupersedesRevisionId" FOREIGN KEY ("SupersedesRevisionId") REFERENCES public."PMTemplateRevisions"("Id") ON DELETE SET NULL;


--
-- Name: PMTemplateRevisions FK_PMTemplateRevisions_PMTemplates_PMTemplateId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplateRevisions"
    ADD CONSTRAINT "FK_PMTemplateRevisions_PMTemplates_PMTemplateId" FOREIGN KEY ("PMTemplateId") REFERENCES public."PMTemplates"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplates FK_PMTemplates_AssetCategories_AssetCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplates"
    ADD CONSTRAINT "FK_PMTemplates_AssetCategories_AssetCategoryId" FOREIGN KEY ("AssetCategoryId") REFERENCES public."AssetCategories"("Id") ON DELETE SET NULL;


--
-- Name: PMTemplates FK_PMTemplates_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplates"
    ADD CONSTRAINT "FK_PMTemplates_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: PMTemplates FK_PMTemplates_Manufacturers_ManufacturerId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplates"
    ADD CONSTRAINT "FK_PMTemplates_Manufacturers_ManufacturerId" FOREIGN KEY ("ManufacturerId") REFERENCES public."Manufacturers"("Id") ON DELETE SET NULL;


--
-- Name: PMTemplates FK_PMTemplates_PMTemplateRevisions_CurrentReleasedRevisionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PMTemplates"
    ADD CONSTRAINT "FK_PMTemplates_PMTemplateRevisions_CurrentReleasedRevisionId" FOREIGN KEY ("CurrentReleasedRevisionId") REFERENCES public."PMTemplateRevisions"("Id") ON DELETE SET NULL;


--
-- Name: PartialDisposals FK_PartialDisposals_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PartialDisposals"
    ADD CONSTRAINT "FK_PartialDisposals_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: PolicyCategoryDefaults FK_PolicyCategoryDefaults_AssetCategories_AssetCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PolicyCategoryDefaults"
    ADD CONSTRAINT "FK_PolicyCategoryDefaults_AssetCategories_AssetCategoryId" FOREIGN KEY ("AssetCategoryId") REFERENCES public."AssetCategories"("Id") ON DELETE CASCADE;


--
-- Name: PolicyCategoryDefaults FK_PolicyCategoryDefaults_Books_BookId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PolicyCategoryDefaults"
    ADD CONSTRAINT "FK_PolicyCategoryDefaults_Books_BookId" FOREIGN KEY ("BookId") REFERENCES public."Books"("Id") ON DELETE CASCADE;


--
-- Name: PolicyCategoryDefaults FK_PolicyCategoryDefaults_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PolicyCategoryDefaults"
    ADD CONSTRAINT "FK_PolicyCategoryDefaults_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: PolicyCategoryDefaults FK_PolicyCategoryDefaults_DepreciationPolicies_DepreciationPol~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PolicyCategoryDefaults"
    ADD CONSTRAINT "FK_PolicyCategoryDefaults_DepreciationPolicies_DepreciationPol~" FOREIGN KEY ("DepreciationPolicyId") REFERENCES public."DepreciationPolicies"("Id") ON DELETE CASCADE;


--
-- Name: ProjectManagers FK_ProjectManagers_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectManagers"
    ADD CONSTRAINT "FK_ProjectManagers_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: ProjectManagers FK_ProjectManagers_Departments_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ProjectManagers"
    ADD CONSTRAINT "FK_ProjectManagers_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES public."Departments"("Id") ON DELETE SET NULL;


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_GlAccounts_GlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_GlAccounts_GlAccountId" FOREIGN KEY ("GlAccountId") REFERENCES public."GlAccounts"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_ItemCategories_ExpenseCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_ItemCategories_ExpenseCategoryId" FOREIGN KEY ("ExpenseCategoryId") REFERENCES public."ItemCategories"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_Locations_ShipToLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_Locations_ShipToLocationId" FOREIGN KEY ("ShipToLocationId") REFERENCES public."Locations"("Id");


--
-- Name: PurchaseOrderLines FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderLines"
    ADD CONSTRAINT "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES public."PurchaseOrders"("Id") ON DELETE CASCADE;


--
-- Name: PurchaseOrderReleases FK_PurchaseOrderReleases_Locations_ShipToLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderReleases"
    ADD CONSTRAINT "FK_PurchaseOrderReleases_Locations_ShipToLocationId" FOREIGN KEY ("ShipToLocationId") REFERENCES public."Locations"("Id");


--
-- Name: PurchaseOrderReleases FK_PurchaseOrderReleases_PurchaseOrderLines_PurchaseOrderLineId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrderReleases"
    ADD CONSTRAINT "FK_PurchaseOrderReleases_PurchaseOrderLines_PurchaseOrderLineId" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES public."PurchaseOrderLines"("Id") ON DELETE CASCADE;


--
-- Name: PurchaseOrders FK_PurchaseOrders_CipProjects_CipProjectId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_CipProjects_CipProjectId" FOREIGN KEY ("CipProjectId") REFERENCES public."CipProjects"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Locations_DefaultShipToLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Locations_DefaultShipToLocationId" FOREIGN KEY ("DefaultShipToLocationId") REFERENCES public."Locations"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_MaintenanceEvents_WorkOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_MaintenanceEvents_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES public."MaintenanceEvents"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Sites_BillToSiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Sites_BillToSiteId" FOREIGN KEY ("BillToSiteId") REFERENCES public."Sites"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Sites_ShipToSiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Sites_ShipToSiteId" FOREIGN KEY ("ShipToSiteId") REFERENCES public."Sites"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Users_ApprovedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Users_ApprovedById" FOREIGN KEY ("ApprovedById") REFERENCES public."Users"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Users_RequestedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Users_RequestedById" FOREIGN KEY ("RequestedById") REFERENCES public."Users"("Id");


--
-- Name: PurchaseOrders FK_PurchaseOrders_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseOrders"
    ADD CONSTRAINT "FK_PurchaseOrders_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id") ON DELETE CASCADE;


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id");


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_GlAccounts_GlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_GlAccounts_GlAccountId" FOREIGN KEY ("GlAccountId") REFERENCES public."GlAccounts"("Id");


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_ItemCategories_ExpenseCategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_ItemCategories_ExpenseCategoryId" FOREIGN KEY ("ExpenseCategoryId") REFERENCES public."ItemCategories"("Id");


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id");


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_PurchaseRequisitions_RequisitionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_PurchaseRequisitions_RequisitionId" FOREIGN KEY ("RequisitionId") REFERENCES public."PurchaseRequisitions"("Id") ON DELETE CASCADE;


--
-- Name: PurchaseRequisitionLines FK_PurchaseRequisitionLines_Vendors_SuggestedVendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitionLines"
    ADD CONSTRAINT "FK_PurchaseRequisitionLines_Vendors_SuggestedVendorId" FOREIGN KEY ("SuggestedVendorId") REFERENCES public."Vendors"("Id");


--
-- Name: PurchaseRequisitions FK_PurchaseRequisitions_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "FK_PurchaseRequisitions_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: PurchaseRequisitions FK_PurchaseRequisitions_Locations_DeliverToLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "FK_PurchaseRequisitions_Locations_DeliverToLocationId" FOREIGN KEY ("DeliverToLocationId") REFERENCES public."Locations"("Id");


--
-- Name: PurchaseRequisitions FK_PurchaseRequisitions_PurchaseOrders_ConvertedToPOId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "FK_PurchaseRequisitions_PurchaseOrders_ConvertedToPOId" FOREIGN KEY ("ConvertedToPOId") REFERENCES public."PurchaseOrders"("Id");


--
-- Name: PurchaseRequisitions FK_PurchaseRequisitions_Sites_DeliverToSiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "FK_PurchaseRequisitions_Sites_DeliverToSiteId" FOREIGN KEY ("DeliverToSiteId") REFERENCES public."Sites"("Id");


--
-- Name: PurchaseRequisitions FK_PurchaseRequisitions_Vendors_SuggestedVendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."PurchaseRequisitions"
    ADD CONSTRAINT "FK_PurchaseRequisitions_Vendors_SuggestedVendorId" FOREIGN KEY ("SuggestedVendorId") REFERENCES public."Vendors"("Id");


--
-- Name: ReorderAlerts FK_ReorderAlerts_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReorderAlerts"
    ADD CONSTRAINT "FK_ReorderAlerts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: ReorderAlerts FK_ReorderAlerts_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReorderAlerts"
    ADD CONSTRAINT "FK_ReorderAlerts_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: ReorderAlerts FK_ReorderAlerts_PurchaseRequisitions_RequisitionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ReorderAlerts"
    ADD CONSTRAINT "FK_ReorderAlerts_PurchaseRequisitions_RequisitionId" FOREIGN KEY ("RequisitionId") REFERENCES public."PurchaseRequisitions"("Id");


--
-- Name: Sites FK_Sites_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Sites"
    ADD CONSTRAINT "FK_Sites_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: Skills FK_Skills_Crafts_CraftId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Skills"
    ADD CONSTRAINT "FK_Skills_Crafts_CraftId" FOREIGN KEY ("CraftId") REFERENCES public."Crafts"("Id");


--
-- Name: Technicians FK_Technicians_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Technicians"
    ADD CONSTRAINT "FK_Technicians_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id") ON DELETE SET NULL;


--
-- Name: Technicians FK_Technicians_Departments_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Technicians"
    ADD CONSTRAINT "FK_Technicians_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES public."Departments"("Id") ON DELETE SET NULL;


--
-- Name: UsTaxSettings FK_UsTaxSettings_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UsTaxSettings"
    ADD CONSTRAINT "FK_UsTaxSettings_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id") ON DELETE CASCADE;


--
-- Name: UsefulLifeEntries FK_UsefulLifeEntries_UsefulLifeTables_UsefulLifeTableId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UsefulLifeEntries"
    ADD CONSTRAINT "FK_UsefulLifeEntries_UsefulLifeTables_UsefulLifeTableId" FOREIGN KEY ("UsefulLifeTableId") REFERENCES public."UsefulLifeTables"("Id") ON DELETE CASCADE;


--
-- Name: VendorInvoiceLines FK_VendorInvoiceLines_CostCenters_CostCenterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "FK_VendorInvoiceLines_CostCenters_CostCenterId" FOREIGN KEY ("CostCenterId") REFERENCES public."CostCenters"("Id");


--
-- Name: VendorInvoiceLines FK_VendorInvoiceLines_GlAccounts_GlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "FK_VendorInvoiceLines_GlAccounts_GlAccountId" FOREIGN KEY ("GlAccountId") REFERENCES public."GlAccounts"("Id");


--
-- Name: VendorInvoiceLines FK_VendorInvoiceLines_GoodsReceiptLines_GoodsReceiptLineId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "FK_VendorInvoiceLines_GoodsReceiptLines_GoodsReceiptLineId" FOREIGN KEY ("GoodsReceiptLineId") REFERENCES public."GoodsReceiptLines"("Id");


--
-- Name: VendorInvoiceLines FK_VendorInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "FK_VendorInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES public."PurchaseOrderLines"("Id");


--
-- Name: VendorInvoiceLines FK_VendorInvoiceLines_VendorInvoices_VendorInvoiceId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoiceLines"
    ADD CONSTRAINT "FK_VendorInvoiceLines_VendorInvoices_VendorInvoiceId" FOREIGN KEY ("VendorInvoiceId") REFERENCES public."VendorInvoices"("Id") ON DELETE CASCADE;


--
-- Name: VendorInvoices FK_VendorInvoices_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoices"
    ADD CONSTRAINT "FK_VendorInvoices_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: VendorInvoices FK_VendorInvoices_Users_ApprovedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoices"
    ADD CONSTRAINT "FK_VendorInvoices_Users_ApprovedById" FOREIGN KEY ("ApprovedById") REFERENCES public."Users"("Id");


--
-- Name: VendorInvoices FK_VendorInvoices_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorInvoices"
    ADD CONSTRAINT "FK_VendorInvoices_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id") ON DELETE CASCADE;


--
-- Name: VendorItemParts FK_VendorItemParts_ItemManufacturerParts_ItemManufacturerPartId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorItemParts"
    ADD CONSTRAINT "FK_VendorItemParts_ItemManufacturerParts_ItemManufacturerPartId" FOREIGN KEY ("ItemManufacturerPartId") REFERENCES public."ItemManufacturerParts"("Id") ON DELETE SET NULL;


--
-- Name: VendorItemParts FK_VendorItemParts_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorItemParts"
    ADD CONSTRAINT "FK_VendorItemParts_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE CASCADE;


--
-- Name: VendorItemParts FK_VendorItemParts_Vendors_VendorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."VendorItemParts"
    ADD CONSTRAINT "FK_VendorItemParts_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES public."Vendors"("Id") ON DELETE CASCADE;


--
-- Name: Vendors FK_Vendors_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Vendors"
    ADD CONSTRAINT "FK_Vendors_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: Vendors FK_Vendors_GlAccounts_DefaultGlAccountId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Vendors"
    ADD CONSTRAINT "FK_Vendors_GlAccounts_DefaultGlAccountId" FOREIGN KEY ("DefaultGlAccountId") REFERENCES public."GlAccounts"("Id");


--
-- Name: WebhookDeliveryLogs FK_WebhookDeliveryLogs_OutboxEvents_OutboxEventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebhookDeliveryLogs"
    ADD CONSTRAINT "FK_WebhookDeliveryLogs_OutboxEvents_OutboxEventId" FOREIGN KEY ("OutboxEventId") REFERENCES public."OutboxEvents"("Id") ON DELETE CASCADE;


--
-- Name: WebhookDeliveryLogs FK_WebhookDeliveryLogs_WebhookSubscriptions_WebhookSubscriptio~; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebhookDeliveryLogs"
    ADD CONSTRAINT "FK_WebhookDeliveryLogs_WebhookSubscriptions_WebhookSubscriptio~" FOREIGN KEY ("WebhookSubscriptionId") REFERENCES public."WebhookSubscriptions"("Id") ON DELETE CASCADE;


--
-- Name: WebhookSubscriptions FK_WebhookSubscriptions_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebhookSubscriptions"
    ADD CONSTRAINT "FK_WebhookSubscriptions_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id") ON DELETE CASCADE;


--
-- Name: WorkOrderOperationLabors FK_WorkOrderOperationLabor_WorkOrderOperations; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationLabors"
    ADD CONSTRAINT "FK_WorkOrderOperationLabor_WorkOrderOperations" FOREIGN KEY ("WorkOrderOperationId") REFERENCES public."WorkOrderOperations"("Id") ON DELETE CASCADE;


--
-- Name: WorkOrderOperationParts FK_WorkOrderOperationParts_Items; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationParts"
    ADD CONSTRAINT "FK_WorkOrderOperationParts_Items" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id");


--
-- Name: WorkOrderOperationParts FK_WorkOrderOperationParts_WorkOrderOperations; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationParts"
    ADD CONSTRAINT "FK_WorkOrderOperationParts_WorkOrderOperations" FOREIGN KEY ("WorkOrderOperationId") REFERENCES public."WorkOrderOperations"("Id") ON DELETE CASCADE;


--
-- Name: WorkOrderOperationTools FK_WorkOrderOperationTools_WorkOrderOperations; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperationTools"
    ADD CONSTRAINT "FK_WorkOrderOperationTools_WorkOrderOperations" FOREIGN KEY ("WorkOrderOperationId") REFERENCES public."WorkOrderOperations"("Id") ON DELETE CASCADE;


--
-- Name: WorkOrderOperations FK_WorkOrderOperations_Crafts; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperations"
    ADD CONSTRAINT "FK_WorkOrderOperations_Crafts" FOREIGN KEY ("CraftId") REFERENCES public."Crafts"("Id");


--
-- Name: WorkOrderOperations FK_WorkOrderOperations_MaintenanceEvents; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperations"
    ADD CONSTRAINT "FK_WorkOrderOperations_MaintenanceEvents" FOREIGN KEY ("MaintenanceEventId") REFERENCES public."MaintenanceEvents"("Id") ON DELETE CASCADE;


--
-- Name: WorkOrderOperations FK_WorkOrderOperations_Technicians; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderOperations"
    ADD CONSTRAINT "FK_WorkOrderOperations_Technicians" FOREIGN KEY ("AssignedTechnicianId") REFERENCES public."Technicians"("Id");


--
-- Name: WorkOrderParts FK_WorkOrderParts_Items_ItemId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderParts"
    ADD CONSTRAINT "FK_WorkOrderParts_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES public."Items"("Id") ON DELETE RESTRICT;


--
-- Name: WorkOrderParts FK_WorkOrderParts_Locations_IssuedFromLocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderParts"
    ADD CONSTRAINT "FK_WorkOrderParts_Locations_IssuedFromLocationId" FOREIGN KEY ("IssuedFromLocationId") REFERENCES public."Locations"("Id") ON DELETE SET NULL;


--
-- Name: WorkOrderParts FK_WorkOrderParts_MaintenanceEvents_MaintenanceEventId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkOrderParts"
    ADD CONSTRAINT "FK_WorkOrderParts_MaintenanceEvents_MaintenanceEventId" FOREIGN KEY ("MaintenanceEventId") REFERENCES public."MaintenanceEvents"("Id") ON DELETE CASCADE;


--
-- Name: WorkRequests FK_WorkRequests_Assets_AssetId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "FK_WorkRequests_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES public."Assets"("Id");


--
-- Name: WorkRequests FK_WorkRequests_Companies_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "FK_WorkRequests_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Companies"("Id");


--
-- Name: WorkRequests FK_WorkRequests_Locations_LocationId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "FK_WorkRequests_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES public."Locations"("Id");


--
-- Name: WorkRequests FK_WorkRequests_MaintenanceEvents_GeneratedWorkOrderId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "FK_WorkRequests_MaintenanceEvents_GeneratedWorkOrderId" FOREIGN KEY ("GeneratedWorkOrderId") REFERENCES public."MaintenanceEvents"("Id");


--
-- Name: WorkRequests FK_WorkRequests_Sites_SiteId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkRequests"
    ADD CONSTRAINT "FK_WorkRequests_Sites_SiteId" FOREIGN KEY ("SiteId") REFERENCES public."Sites"("Id");


--
-- Name: OutboxEvents OutboxEvents_TenantId_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."OutboxEvents"
    ADD CONSTRAINT "OutboxEvents_TenantId_fkey" FOREIGN KEY ("TenantId") REFERENCES public."Tenants"("Id");


--
-- PostgreSQL database dump complete
--

\unrestrict txyMOos8tBVwmnl1MujdknFDIQQphvkVHqeFYXKQt1ciTxFd8O6YcrCybPjULKY

