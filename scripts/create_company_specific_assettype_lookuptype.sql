INSERT INTO "LookupTypes" ("TenantId", "CompanyId", "Key", "Name", "IsSystem", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 1, 1, 'AssetType', 'Asset Type (Company 1)', true, true, now(), now()
WHERE NOT EXISTS (
    SELECT 1 FROM "LookupTypes"
    WHERE "TenantId" = 1 AND "CompanyId" = 1 AND "Key" = 'AssetType'
);
