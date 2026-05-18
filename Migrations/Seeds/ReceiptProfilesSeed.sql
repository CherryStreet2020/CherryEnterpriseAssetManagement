-- =====================================================================
-- ReceiptProfilesSeed.sql
-- ---------------------------------------------------------------------
-- Seeds the 12 starter ReceiptProfiles for ADR-015
-- ("Industry-Agnostic Receipt Schema").
--
-- Source ADR:
--   docs/ADR-015-industry-agnostic-receipt-schema.md  (D2, D8)
-- Source research:
--   docs/research/industry-agnostic-receipt-schema.md  Section 7
--   docs/research/voice-ai-spike-adr015-d10.md         Section 3 + 6
--
-- This file is invoked from Migration PR #1
-- (`migrationBuilder.Sql(File.ReadAllText(...))`).
--
-- Idempotency:
--   Every INSERT is `ON CONFLICT ("Code") DO NOTHING`. Safe to re-run
--   on a database that already has any subset of the 12 profiles.
--
-- 12-profile inventory:
--   01. STEEL            -- structural / sheet metal / ASTM
--   02. PHARMA           -- DSCSA + 21 CFR 211 finished dose
--   03. FOOD             -- FSMA 204 Food Traceability Rule
--   04. CHEMICAL         -- REACH / SDS / DOT hazmat
--   05. ELECTRONICS      -- IPC J-STD-033 MSL + RoHS
--   06. MEDICAL_DEVICE   -- EU MDR / EUDAMED UDI + 21 CFR 820
--   07. AEROSPACE        -- AS9100 + AMS + DFARS specialty metals
--   08. CANNABIS         -- METRC state seed-to-sale
--   09. AUTOMOTIVE       -- IATF 16949 + PPAP + IMDS
--   10. APPAREL          -- roll/dye lot + GOTS / OEKO-TEX
--   11. CONSTRUCTION     -- batch concrete / ready-mix / MRO
--   12. OIL_GAS          -- API line pipe / OCTG sour-service
--
-- All four UiFormSpec field-spec keys mandated by the D10 spike are
-- present on every field:
--   - scope            (which profile codes the field belongs to)
--   - exampleQueries   (2-4 voice phrases that map to this field)
--   - disambiguation   (what NOT to confuse this field with — null OK)
--   - semanticAction   (tool name to call instead of SQL — null OK)
--
-- RegulatoryProfileIds reference the RegulatoryRegime enum values
-- in Models/Production/RegulatoryProfile.cs. The actual int IDs are
-- assumed to match the enum's numeric values after RegulatoryProfile
-- seeding runs in a sibling migration.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 01. STEEL
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('STEEL',
     'Steel / Structural Metals',
     'Heat number + mill cert + ASTM / AMS traceability for sheet, plate, bar, pipe, tube stock. Anchor of ASME, AWS, AS9100 audit chains. Default profile for the legacy sheet-metal-centric receipt schema.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["heatNumber"],
  "properties": {
    "heatNumber":      { "type": "string", "maxLength": 64,  "description": "Mill-issued heat / melt identifier. Killer trace field for steel." },
    "mill":            { "type": "string", "maxLength": 128, "description": "Producing mill (Nucor, ArcelorMittal, etc.)." },
    "millCertUrl":     { "type": "string", "format": "uri",  "maxLength": 500, "description": "URL to the Certified Mill Test Report (CMTR) PDF." },
    "astmDesignation": { "type": "string", "maxLength": 64,  "description": "ASTM / ASME spec designation (A36, A572-50, etc.)." },
    "amsSpec":         { "type": "string", "maxLength": 64,  "description": "AMS specification when applicable (e.g. AMS 5510)." },
    "countryOfMelt":   { "type": "string", "maxLength": 2,   "description": "ISO 3166-1 alpha-2 country of melt (DFARS-relevant)." },
    "lengthMm":        { "type": "number", "minimum": 0,     "description": "Physical length in mm." },
    "widthMm":         { "type": "number", "minimum": 0,     "description": "Physical width in mm." },
    "thicknessMm":     { "type": "number", "minimum": 0,     "description": "Physical thickness in mm." },
    "usableLengthMm":  { "type": "number", "minimum": 0,     "description": "Length remaining after cuts." },
    "usableWidthMm":   { "type": "number", "minimum": 0,     "description": "Width remaining after cuts." }
  }
}
$$::jsonb,
$$["heatNumber","mill","astmDesignation","amsSpec","countryOfMelt"]$$::jsonb,
$${"uom":"sheet"}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Traceability",
      "fields": [
        {
          "key": "heatNumber", "label": "Heat #", "type": "text", "required": true,
          "voice": ["heat","heat number","melt id","melt number","heat #"],
          "scope": ["STEEL","AEROSPACE","OIL_GAS"],
          "exampleQueries": ["receipts of heat H-12345","all heats from Nucor","heats melted in US"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","batch","serial","tag"],
            "confusableWith": ["LotNumber (core column, all profiles)","traceabilityLotCode (FOOD only)","metrcTag (CANNABIS only)"]
          },
          "semanticAction": null
        },
        {
          "key": "mill", "label": "Mill", "type": "text",
          "voice": ["mill","steel mill","melt source","producer"],
          "scope": ["STEEL","AEROSPACE","OIL_GAS"],
          "exampleQueries": ["heats from Nucor","material milled by ArcelorMittal"],
          "disambiguation": null,
          "semanticAction": null
        },
        {
          "key": "millCertUrl", "label": "Mill Cert URL", "type": "url",
          "voice": ["mill cert","mtr","cmtr","mill test report","cert"],
          "scope": ["STEEL","AEROSPACE","OIL_GAS"],
          "exampleQueries": ["receipts missing mill cert","heats without MTR"],
          "disambiguation": null,
          "semanticAction": null
        },
        {
          "key": "astmDesignation", "label": "ASTM", "type": "text",
          "voice": ["astm","grade","spec","designation"],
          "scope": ["STEEL"],
          "exampleQueries": ["all A572-50 receipts","material graded A36"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["ams","api spec"],
            "confusableWith": ["amsSpec (STEEL/AEROSPACE)","apiSpec (OIL_GAS)"]
          },
          "semanticAction": null
        },
        {
          "key": "amsSpec", "label": "AMS Spec", "type": "text",
          "voice": ["ams","ams spec","aerospace spec"],
          "scope": ["STEEL","AEROSPACE"],
          "exampleQueries": ["AMS 5510 receipts","material to AMS 4911"],
          "disambiguation": null,
          "semanticAction": null
        },
        {
          "key": "countryOfMelt", "label": "Country of Melt", "type": "iso2",
          "voice": ["country of melt","melt origin","melted in"],
          "scope": ["STEEL","AEROSPACE","OIL_GAS"],
          "exampleQueries": ["heats melted in US","DFARS-compliant melt"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["country of origin"],
            "confusableWith": ["countryOfOrigin (PHARMA/FOOD/ELECTRONICS/APPAREL)"]
          },
          "semanticAction": null
        }
      ]
    },
    {
      "title": "Dimensions",
      "fields": [
        {
          "key": "lengthMm", "label": "Length (mm)", "type": "decimal",
          "voice": ["length","long"],
          "scope": ["STEEL"],
          "exampleQueries": ["sheets longer than 2000mm"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "widthMm", "label": "Width (mm)", "type": "decimal",
          "voice": ["width","wide"],
          "scope": ["STEEL"],
          "exampleQueries": ["sheets wider than 1500mm"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "thicknessMm", "label": "Thickness (mm)", "type": "decimal",
          "voice": ["thickness","thick","gauge"],
          "scope": ["STEEL"],
          "exampleQueries": ["plate thicker than 12mm"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "usableLengthMm", "label": "Usable Length (mm)", "type": "decimal",
          "voice": ["usable length","remaining length"],
          "scope": ["STEEL"],
          "exampleQueries": ["sheets with more than 1000mm usable length"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "usableWidthMm", "label": "Usable Width (mm)", "type": "decimal",
          "voice": ["usable width","remaining width"],
          "scope": ["STEEL"],
          "exampleQueries": ["sheets with more than 800mm usable width"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 02. PHARMA
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('PHARMA',
     'Pharmaceutical (DSCSA)',
     'Prescription-drug receipts under FDA DSCSA + 21 CFR 210/211. NDC + lot # + expiration are mandatory; serialization required at unit level for transactions on or after 2024-11-27 enforcement.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["expirationDate","ndc"],
  "properties": {
    "ndc":                 { "type": "string", "pattern": "^[0-9]{4,5}-[0-9]{3,4}-[0-9]{1,2}$", "description": "FDA National Drug Code (10-digit, 4-4-2 / 5-3-2 / 5-4-1 layout)." },
    "gtin":                { "type": "string", "pattern": "^[0-9]{14}$", "description": "GS1 GTIN-14 (DSCSA serialized package identifier root)." },
    "expirationDate":      { "type": "string", "format": "date", "description": "Manufacturer-stamped expiration date." },
    "manufactureDate":     { "type": "string", "format": "date", "description": "Manufacturer-stamped manufacture date." },
    "deaSchedule":         { "type": "string", "enum": ["I","II","III","IV","V",""], "description": "DEA controlled-substance schedule. Empty string for non-controlled." },
    "countryOfOrigin":     { "type": "string", "maxLength": 2,   "description": "ISO 3166-1 alpha-2 country of origin." },
    "epcisPedigreeUrl":    { "type": "string", "format": "uri",  "description": "URL to the DSCSA EPCIS T3 pedigree document." },
    "tempExcursionLogUrl": { "type": "string", "format": "uri",  "description": "URL to cold-chain temperature excursion log." }
  }
}
$$::jsonb,
$$["expirationDate","ndc","gtin","deaSchedule"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Identity",
      "fields": [
        {
          "key": "ndc", "label": "NDC", "type": "text", "required": true,
          "voice": ["ndc","national drug code","drug code"],
          "scope": ["PHARMA"],
          "exampleQueries": ["NDC 0002-3227-30","receipts with NDC 0002-7510-01","drug code 0093-5057-01"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","gtin","udi"],
            "confusableWith": ["gtin (PHARMA)","udiDi (MEDICAL_DEVICE)"]
          },
          "semanticAction": null
        },
        {
          "key": "gtin", "label": "GTIN", "type": "text",
          "voice": ["gtin","gs1 gtin","serialized package"],
          "scope": ["PHARMA","FOOD","MEDICAL_DEVICE"],
          "exampleQueries": ["all receipts under GTIN 00300670001019"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["ndc","udi"],
            "confusableWith": ["ndc (PHARMA)","udiDi (MEDICAL_DEVICE)"]
          },
          "semanticAction": null
        }
      ]
    },
    {
      "title": "Shelf Life",
      "fields": [
        {
          "key": "expirationDate", "label": "Expiration", "type": "date", "required": true,
          "voice": ["expiration","expiry","expires","exp date","use by"],
          "scope": ["PHARMA","FOOD","MEDICAL_DEVICE","CHEMICAL"],
          "exampleQueries": ["lots expiring within 30 days","expires before next month","find lot expiring in 30 days"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["best by","cure by"],
            "confusableWith": ["bestByDate (FOOD)","cureByDate (CONSTRUCTION)","shelfLifeDate (CHEMICAL)"]
          },
          "semanticAction": null
        },
        {
          "key": "manufactureDate", "label": "Mfg Date", "type": "date",
          "voice": ["manufactured","mfg date","made","manufacture date"],
          "scope": ["PHARMA","MEDICAL_DEVICE","CHEMICAL","CONSTRUCTION"],
          "exampleQueries": ["receipts manufactured in 2025","lots made before Jan 2026"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Controlled / Cold Chain / Pedigree",
      "fields": [
        {
          "key": "deaSchedule", "label": "DEA Schedule", "type": "enum",
          "voice": ["dea schedule","controlled","schedule","cii","schedule two"],
          "scope": ["PHARMA"],
          "exampleQueries": ["all schedule II receipts","CII controlled substances received this week"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in"],
          "scope": ["PHARMA","FOOD","ELECTRONICS","APPAREL","CHEMICAL"],
          "exampleQueries": ["receipts originating in India","made in US"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["country of melt"],
            "confusableWith": ["countryOfMelt (STEEL/AEROSPACE/OIL_GAS)"]
          },
          "semanticAction": null
        },
        {
          "key": "epcisPedigreeUrl", "label": "EPCIS Pedigree", "type": "url",
          "voice": ["pedigree","epcis","t3","chain of custody document","dscsa document"],
          "scope": ["PHARMA"],
          "exampleQueries": ["receipts missing pedigree","lots without EPCIS T3"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "tempExcursionLogUrl", "label": "Temp Log", "type": "url",
          "voice": ["temperature log","cold chain","excursion log","temp log"],
          "scope": ["PHARMA"],
          "exampleQueries": ["receipts with temperature excursions","cold chain log for lot X"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[2, 3]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 03. FOOD
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('FOOD',
     'Food / Beverage (FSMA 204)',
     'FDA FSMA Section 204 Food Traceability Rule receiving CTE (Critical Tracking Event). TLC + TLC source + 24-hour recordkeeping mandatory for Food Traceability List items.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["traceabilityLotCode","tlcSource"],
  "properties": {
    "traceabilityLotCode": { "type": "string", "maxLength": 64,  "description": "FSMA 204 Traceability Lot Code (TLC) — primary FSMA trace key." },
    "tlcSource":           { "type": "string", "maxLength": 200, "description": "Source of the TLC (one-up immediate supplier / grower / processor)." },
    "tlcSourceReference":  { "type": "string", "maxLength": 500, "description": "Reference document URL for the TLC source assignment." },
    "bestByDate":          { "type": "string", "format": "date", "description": "Best-by / use-by / sell-by date." },
    "harvestDate":         { "type": "string", "format": "date", "description": "Field harvest / catch date." },
    "packDate":            { "type": "string", "format": "date", "description": "Pack / co-pack date." },
    "allergens":           { "type": "array",  "items": { "type": "string" }, "description": "FDA Big-9 + sesame allergen tags." },
    "organicCertNumber":   { "type": "string", "maxLength": 64,  "description": "USDA NOP organic certifier number." },
    "gfsiScheme":          { "type": "string", "enum": ["SQF","BRCGS","FSSC22000","IFS","none"], "description": "GFSI-benchmarked food-safety scheme." },
    "gfsiCertNumber":      { "type": "string", "description": "GFSI-scheme certificate number." },
    "countryOfOrigin":     { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." },
    "coaUrl":              { "type": "string", "format": "uri", "description": "Certificate of Analysis URL." },
    "supplierName":        { "type": "string", "maxLength": 200, "description": "Supplier / grower name as printed on the BOL." }
  }
}
$$::jsonb,
$$["traceabilityLotCode","bestByDate","gfsiScheme","supplierName"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Traceability (FSMA 204)",
      "fields": [
        {
          "key": "traceabilityLotCode", "label": "TLC", "type": "text", "required": true,
          "voice": ["tlc","traceability lot code","trace code","fsma lot"],
          "scope": ["FOOD"],
          "exampleQueries": ["all receipts with TLC FF-2026-99","trace code SG-2026-44"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","heat","batch"],
            "confusableWith": ["LotNumber (core column, all profiles)","heatNumber (STEEL/AEROSPACE/OIL_GAS)"]
          },
          "semanticAction": null
        },
        {
          "key": "tlcSource", "label": "TLC Source", "type": "text", "required": true,
          "voice": ["tlc source","lot source","one-up","one up supplier","previous source"],
          "scope": ["FOOD"],
          "exampleQueries": ["TLC source SmartGreens","one-up source for lot FF-2026-99"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "tlcSourceReference", "label": "TLC Source Ref", "type": "url",
          "voice": ["source reference","tlc source document","tlc source url"],
          "scope": ["FOOD"],
          "exampleQueries": ["receipts missing TLC source document"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "supplierName", "label": "Supplier", "type": "text",
          "voice": ["supplier","grower","farm","from"],
          "scope": ["FOOD"],
          "exampleQueries": ["receipts from SmartGreens","spinach from supplier SmartGreens"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["vendor (always go to Vendors table)"],
            "confusableWith": ["Vendor.Name (core relation, joined via PO)"]
          },
          "semanticAction": null
        }
      ]
    },
    {
      "title": "Dates",
      "fields": [
        {
          "key": "bestByDate", "label": "Best By", "type": "date",
          "voice": ["best by","best-by","use by","sell by"],
          "scope": ["FOOD"],
          "exampleQueries": ["best by within 7 days","selling by tomorrow"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["expiration"],
            "confusableWith": ["expirationDate (PHARMA/MEDICAL_DEVICE)"]
          },
          "semanticAction": null
        },
        {
          "key": "harvestDate", "label": "Harvest", "type": "date",
          "voice": ["harvest","picked","caught","harvested"],
          "scope": ["FOOD","CANNABIS"],
          "exampleQueries": ["harvested within last 7 days","picked in October"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "packDate", "label": "Pack Date", "type": "date",
          "voice": ["pack date","packed","packaging date"],
          "scope": ["FOOD"],
          "exampleQueries": ["packed within last 3 days"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Compliance",
      "fields": [
        {
          "key": "allergens", "label": "Allergens", "type": "stringArray",
          "voice": ["allergen","allergens","big 9","big nine"],
          "scope": ["FOOD"],
          "exampleQueries": ["receipts with peanut allergen","sesame-containing lots"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "organicCertNumber", "label": "Organic Cert", "type": "text",
          "voice": ["organic","nop","usda organic"],
          "scope": ["FOOD"],
          "exampleQueries": ["USDA organic certified lots"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "gfsiScheme", "label": "GFSI", "type": "enum",
          "voice": ["sqf","brcgs","fssc","fssc22000","ifs","gfsi"],
          "scope": ["FOOD"],
          "exampleQueries": ["SQF certified suppliers","FSSC22000 receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "gfsiCertNumber", "label": "GFSI Cert #", "type": "text",
          "voice": ["gfsi number","gfsi cert number","sqf number"],
          "scope": ["FOOD"],
          "exampleQueries": ["receipts under SQF cert 12345"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","grown in","made in"],
          "scope": ["FOOD","PHARMA","ELECTRONICS","APPAREL","CHEMICAL"],
          "exampleQueries": ["receipts from Mexico","grown in US"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "coaUrl", "label": "COA", "type": "url",
          "voice": ["coa","certificate of analysis","analysis cert"],
          "scope": ["FOOD","CHEMICAL","CANNABIS"],
          "exampleQueries": ["receipts missing COA","lots without certificate of analysis"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[12]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 04. CHEMICAL
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('CHEMICAL',
     'Industrial Chemicals (REACH / SDS / DOT)',
     'Industrial / specialty chemicals. CAS number + UN hazmat ID + SDS revision mandatory; supports drums, totes, IBCs, cylinders. REACH SVHC + DOT 49 CFR 172.101 hazard-class compliance.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["casNumber"],
  "properties": {
    "casNumber":            { "type": "string", "pattern": "^[0-9]{1,7}-[0-9]{2}-[0-9]$", "description": "Chemical Abstracts Service registry number." },
    "unNumber":             { "type": "string", "pattern": "^UN[0-9]{4}$", "description": "UN/NA hazardous material identifier (DOT 49 CFR)." },
    "hazardClass":          { "type": "string", "description": "DOT hazard class (e.g. '3 - Flammable Liquid')." },
    "packingGroup":         { "type": "string", "enum": ["I","II","III",""], "description": "DOT packing group." },
    "grade":                { "type": "string", "description": "Material grade (ACS, USP, technical, etc.)." },
    "purity":               { "type": "number", "minimum": 0, "maximum": 100, "description": "Percent purity from COA." },
    "manufactureDate":      { "type": "string", "format": "date", "description": "Date of manufacture." },
    "shelfLifeDate":        { "type": "string", "format": "date", "description": "Manufacturer-stated shelf life expiration." },
    "sdsRevision":          { "type": "string", "description": "SDS document revision identifier." },
    "sdsUrl":               { "type": "string", "format": "uri",  "description": "URL to Safety Data Sheet PDF." },
    "containerType":        { "type": "string", "enum": ["drum","ibc","tote","cylinder","pail","bottle","bag","other"], "description": "Container kind." },
    "containerSizeLiters":  { "type": "number", "description": "Container size in liters." },
    "reachSvhcDeclared":    { "type": "boolean", "description": "REACH SVHC declared (> 0.1 percent w/w)." },
    "countryOfOrigin":      { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." }
  }
}
$$::jsonb,
$$["casNumber","unNumber","hazardClass","containerType","sdsRevision"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Chemical Identity",
      "fields": [
        {
          "key": "casNumber", "label": "CAS #", "type": "text", "required": true,
          "voice": ["cas","cas number","cas registry"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["CAS 67-64-1","all acetone receipts by CAS","receipts for cas 50-00-0"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["un number","mpn","ndc"],
            "confusableWith": ["unNumber (CHEMICAL)","ndc (PHARMA)"]
          },
          "semanticAction": null
        },
        {
          "key": "grade", "label": "Grade", "type": "text",
          "voice": ["grade","acs grade","usp grade","tech grade"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["ACS grade receipts","tech grade material"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "purity", "label": "Purity (%)", "type": "decimal",
          "voice": ["purity","percent purity","assay"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["purity above 99","assay below 98"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Hazmat / DOT",
      "fields": [
        {
          "key": "unNumber", "label": "UN #", "type": "text",
          "voice": ["un number","un id","dot un","hazmat number"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["UN1090 receipts","hazmat UN1230"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["cas","ndc"],
            "confusableWith": ["casNumber (CHEMICAL)"]
          },
          "semanticAction": null
        },
        {
          "key": "hazardClass", "label": "Hazard Class", "type": "text",
          "voice": ["hazard class","dot class","hazmat class"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["class 3 flammable liquid receipts","hazmat class 8"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "packingGroup", "label": "Packing Group", "type": "enum",
          "voice": ["packing group","pg","dot pg"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["PG I receipts","packing group II"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "reachSvhcDeclared", "label": "REACH SVHC", "type": "boolean",
          "voice": ["reach","svhc","substance of very high concern"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["receipts with REACH SVHC declared"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Container",
      "fields": [
        {
          "key": "containerType", "label": "Container", "type": "enum",
          "voice": ["container","drum","ibc","tote","cylinder","pail"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["drum receipts","IBC totes","cylinder receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "containerSizeLiters", "label": "Container Size (L)", "type": "decimal",
          "voice": ["container size","drum size","liters","capacity"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["55-gallon drums","containers over 200 liters"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Documents / Dates",
      "fields": [
        {
          "key": "sdsRevision", "label": "SDS Revision", "type": "text",
          "voice": ["sds revision","msds revision","safety data sheet rev"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["receipts on SDS revision 4.2","old SDS revisions"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "sdsUrl", "label": "SDS URL", "type": "url",
          "voice": ["sds","msds","safety data sheet"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["receipts missing SDS"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "manufactureDate", "label": "Mfg Date", "type": "date",
          "voice": ["manufactured","mfg date","made"],
          "scope": ["CHEMICAL","PHARMA","MEDICAL_DEVICE","CONSTRUCTION"],
          "exampleQueries": ["manufactured in last 90 days"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "shelfLifeDate", "label": "Shelf Life Date", "type": "date",
          "voice": ["shelf life","shelf life date","expires","good until"],
          "scope": ["CHEMICAL"],
          "exampleQueries": ["chemicals expiring within 6 months"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["best by","cure by"],
            "confusableWith": ["expirationDate (PHARMA/MEDICAL_DEVICE)","bestByDate (FOOD)"]
          },
          "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in"],
          "scope": ["CHEMICAL","PHARMA","FOOD","ELECTRONICS","APPAREL"],
          "exampleQueries": ["chemicals from Germany"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[13, 14]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 05. ELECTRONICS
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('ELECTRONICS',
     'Electronics / Semiconductors',
     'Component receipts under IPC J-STD-033 MSL, RoHS, REACH SVHC, conflict-minerals. MPN + manufacturer lot + date code mandatory; floor-life clock starts at bagOpenedAt.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["mpn","manufacturerLot","dateCode"],
  "properties": {
    "mpn":                 { "type": "string", "description": "Manufacturer Part Number." },
    "manufacturer":        { "type": "string", "description": "Manufacturer name (e.g. TI, Murata)." },
    "manufacturerLot":     { "type": "string", "description": "Manufacturer-assigned lot code." },
    "dateCode":            { "type": "string", "pattern": "^[0-9]{4}$", "description": "YYWW date code stamped on the part / reel." },
    "mslLevel":            { "type": "number", "enum": [1,2,2.5,3,4,5,5.5,6], "description": "IPC J-STD-033 Moisture Sensitivity Level." },
    "bagSealedAt":         { "type": "string", "format": "date-time", "description": "When the moisture-barrier bag was last sealed." },
    "bagOpenedAt":         { "type": "string", "format": "date-time", "description": "When the moisture-barrier bag was opened — starts floor-life clock." },
    "rohsCompliant":       { "type": "boolean", "description": "EU RoHS-compliant." },
    "reachSvhcDeclared":   { "type": "boolean", "description": "REACH SVHC declared." },
    "conflictMineralsRev": { "type": "string", "description": "CMRT (Conflict Minerals Reporting Template) revision." },
    "esdClass":            { "type": "string", "description": "ESDA S20.20 ESD class." },
    "reelId":              { "type": "string", "description": "Reel / tape ID for SMT pick-and-place." },
    "countryOfOrigin":     { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." }
  }
}
$$::jsonb,
$$["mpn","manufacturerLot","dateCode","mslLevel","bagOpenedAt"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Identity",
      "fields": [
        {
          "key": "mpn", "label": "MPN", "type": "text", "required": true,
          "voice": ["mpn","manufacturer part","manufacturer part number","part number"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["receipts of MPN STM32F407VGT6","TI parts","manufacturer part LM358N"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["item","sku"],
            "confusableWith": ["Items.ItemCode (core column)","partNumber (AUTOMOTIVE)"]
          },
          "semanticAction": null
        },
        {
          "key": "manufacturer", "label": "Manufacturer", "type": "text",
          "voice": ["manufacturer","mfr","brand"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["TI receipts","parts made by Murata"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "manufacturerLot", "label": "Mfr Lot", "type": "text", "required": true,
          "voice": ["manufacturer lot","mfr lot","factory lot"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["all receipts under mfr lot ABC123"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","traceability lot code"],
            "confusableWith": ["LotNumber (core column)","traceabilityLotCode (FOOD)"]
          },
          "semanticAction": null
        },
        {
          "key": "dateCode", "label": "Date Code", "type": "text", "required": true,
          "voice": ["date code","yyww","manufacture week"],
          "scope": ["ELECTRONICS","AUTOMOTIVE"],
          "exampleQueries": ["date code 2540","parts with date code from week 40"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "reelId", "label": "Reel ID", "type": "text",
          "voice": ["reel","tape","reel id"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["reel ID R-12345"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Moisture / MSL",
      "fields": [
        {
          "key": "mslLevel", "label": "MSL", "type": "number",
          "voice": ["msl","msl level","moisture level","moisture sensitivity"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["MSL 3","MSL 2a","all MSL 4 and above"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "bagOpenedAt", "label": "Bag Opened At", "type": "datetime",
          "voice": ["bag open","opened bag","time out of bag","tob"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["bag open more than 168 hours","tob > 168h","floor life exceeded"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "bagSealedAt", "label": "Bag Sealed At", "type": "datetime",
          "voice": ["bag sealed","resealed","baked"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["bags resealed yesterday"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Compliance",
      "fields": [
        {
          "key": "rohsCompliant", "label": "RoHS", "type": "boolean",
          "voice": ["rohs","rohs compliant","lead-free"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["RoHS-compliant receipts","non-RoHS parts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "reachSvhcDeclared", "label": "REACH SVHC", "type": "boolean",
          "voice": ["reach","svhc"],
          "scope": ["ELECTRONICS","CHEMICAL"],
          "exampleQueries": ["receipts with REACH SVHC declared"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "conflictMineralsRev", "label": "CMRT Rev", "type": "text",
          "voice": ["conflict minerals","cmrt","3tg"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["receipts on CMRT 6.31"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "esdClass", "label": "ESD Class", "type": "text",
          "voice": ["esd","esd class","static class"],
          "scope": ["ELECTRONICS"],
          "exampleQueries": ["class 1A ESD parts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in","coo"],
          "scope": ["ELECTRONICS","PHARMA","FOOD","APPAREL","CHEMICAL"],
          "exampleQueries": ["parts made in Taiwan","origin China"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[14]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 06. MEDICAL_DEVICE
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('MEDICAL_DEVICE',
     'Medical Devices (EU MDR / 21 CFR 820)',
     'Medical-device receipts under FDA 21 CFR 820 + EU MDR / EUDAMED. UDI-DI + Basic UDI-DI mandatory; lot OR serial mandatory depending on device class.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["basicUdiDi","udiDi"],
  "properties": {
    "basicUdiDi":         { "type": "string", "description": "EU MDR Basic UDI-DI (device-model identifier in EUDAMED)." },
    "udiDi":              { "type": "string", "description": "UDI-DI device identifier (per-SKU)." },
    "manufactureDate":    { "type": "string", "format": "date", "description": "Manufacture date (UDI-PI element)." },
    "expirationDate":     { "type": "string", "format": "date", "description": "Expiration date (UDI-PI element)." },
    "sterilizationBatch": { "type": "string", "description": "Sterilization batch / cycle identifier." },
    "softwareVersion":    { "type": "string", "description": "Embedded software / firmware version (SaMD)." },
    "deviceClass":        { "type": "string", "enum": ["I","Is","Im","IIa","IIb","III"], "description": "EU MDR device class." },
    "implantable":        { "type": "boolean", "description": "Implantable device flag." },
    "singleUse":          { "type": "boolean", "description": "Single-use device flag." },
    "countryOfOrigin":    { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." }
  }
}
$$::jsonb,
$$["udiDi","basicUdiDi","expirationDate","sterilizationBatch","deviceClass"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "UDI",
      "fields": [
        {
          "key": "basicUdiDi", "label": "Basic UDI-DI", "type": "text", "required": true,
          "voice": ["basic udi","basic udi-di","model identifier"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["receipts under basic UDI-DI 040000123456789"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "udiDi", "label": "UDI-DI", "type": "text", "required": true,
          "voice": ["udi","udi-di","udi di","device identifier"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["receipts of UDI 00840012345670","UDI-DI 00840012345670"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["ndc","gtin","mpn"],
            "confusableWith": ["ndc (PHARMA)","gtin (PHARMA/FOOD)","mpn (ELECTRONICS)"]
          },
          "semanticAction": null
        }
      ]
    },
    {
      "title": "Production / Sterilization",
      "fields": [
        {
          "key": "manufactureDate", "label": "Mfg Date", "type": "date",
          "voice": ["manufactured","mfg date","made"],
          "scope": ["MEDICAL_DEVICE","PHARMA","CHEMICAL","CONSTRUCTION"],
          "exampleQueries": ["devices manufactured in 2025"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "expirationDate", "label": "Expiration", "type": "date",
          "voice": ["expiration","expires","exp date","use by"],
          "scope": ["MEDICAL_DEVICE","PHARMA","FOOD","CHEMICAL"],
          "exampleQueries": ["devices expiring within 30 days","sterile stock expiring soon"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "sterilizationBatch", "label": "Sterilization Batch", "type": "text",
          "voice": ["sterilization batch","autoclave","sterile lot","sterilization cycle"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["receipts under sterilization batch S-2026-99"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "softwareVersion", "label": "Software Version", "type": "text",
          "voice": ["software version","firmware","fw version","samd version"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["receipts on software version 2.4.1"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Classification",
      "fields": [
        {
          "key": "deviceClass", "label": "Device Class", "type": "enum",
          "voice": ["device class","mdr class","class iii"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["class III devices","implantable class IIb receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "implantable", "label": "Implantable", "type": "boolean",
          "voice": ["implantable","implant"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["all implantable device receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "singleUse", "label": "Single Use", "type": "boolean",
          "voice": ["single use","single-use","disposable"],
          "scope": ["MEDICAL_DEVICE"],
          "exampleQueries": ["single-use device receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in"],
          "scope": ["MEDICAL_DEVICE","PHARMA","FOOD","ELECTRONICS","APPAREL","CHEMICAL"],
          "exampleQueries": ["devices made in Ireland"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[1, 10]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 07. AEROSPACE
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('AEROSPACE',
     'Aerospace (AS9100 / AMS / DFARS)',
     'Aerospace material receipts under AS9100 / AS9145 / DFARS specialty metals. Heat + mill cert + AMS spec mandatory; DFARS country-of-melt audit chain.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["heatNumber","millCertUrl","amsSpec"],
  "properties": {
    "heatNumber":        { "type": "string", "maxLength": 64,  "description": "Mill-issued heat / melt identifier." },
    "mill":              { "type": "string", "maxLength": 128, "description": "Producing mill." },
    "millCertUrl":       { "type": "string", "format": "uri",  "description": "URL to the Certified Mill Test Report PDF." },
    "amsSpec":           { "type": "string", "maxLength": 64,  "description": "AMS specification (e.g. AMS 5510, AMS 4911)." },
    "astmDesignation":   { "type": "string", "maxLength": 64,  "description": "ASTM designation when applicable." },
    "countryOfMelt":     { "type": "string", "maxLength": 2,   "description": "ISO 3166-1 alpha-2 country of melt (DFARS)." },
    "dfarsCompliant":    { "type": "boolean", "description": "DFARS 252.225-7008/-7009 specialty-metals compliant." },
    "pyrometryChartUrl": { "type": "string", "format": "uri",  "description": "URL to NADCAP AC7102 pyrometry chart." },
    "tensileLotResult":  { "type": "string", "description": "Tensile / mechanical lot test result reference." }
  }
}
$$::jsonb,
$$["heatNumber","amsSpec","countryOfMelt","dfarsCompliant"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Heat / Mill",
      "fields": [
        {
          "key": "heatNumber", "label": "Heat #", "type": "text", "required": true,
          "voice": ["heat","heat number","melt id","melt number"],
          "scope": ["AEROSPACE","STEEL","OIL_GAS"],
          "exampleQueries": ["aerospace heats from Carpenter","heat H-12345"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","batch","serial","tag"],
            "confusableWith": ["LotNumber (core column)","traceabilityLotCode (FOOD)","metrcTag (CANNABIS)"]
          },
          "semanticAction": null
        },
        {
          "key": "mill", "label": "Mill", "type": "text",
          "voice": ["mill","melt source","producer"],
          "scope": ["AEROSPACE","STEEL","OIL_GAS"],
          "exampleQueries": ["material from Carpenter Technology"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "millCertUrl", "label": "Mill Cert URL", "type": "url", "required": true,
          "voice": ["mill cert","mtr","cmtr","mill test report"],
          "scope": ["AEROSPACE","STEEL","OIL_GAS"],
          "exampleQueries": ["aerospace receipts missing mill cert"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Specifications",
      "fields": [
        {
          "key": "amsSpec", "label": "AMS Spec", "type": "text", "required": true,
          "voice": ["ams","ams spec","aerospace spec"],
          "scope": ["AEROSPACE","STEEL"],
          "exampleQueries": ["AMS 5510 receipts","material to AMS 4911"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["astm","api"],
            "confusableWith": ["astmDesignation (STEEL)","apiSpec (OIL_GAS)"]
          },
          "semanticAction": null
        },
        {
          "key": "astmDesignation", "label": "ASTM", "type": "text",
          "voice": ["astm","grade"],
          "scope": ["AEROSPACE","STEEL"],
          "exampleQueries": ["ASTM B265 receipts"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "DFARS / NADCAP",
      "fields": [
        {
          "key": "countryOfMelt", "label": "Country of Melt", "type": "iso2",
          "voice": ["country of melt","melt origin","dfars country"],
          "scope": ["AEROSPACE","STEEL","OIL_GAS"],
          "exampleQueries": ["DFARS-compliant melt","heats melted in US"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["country of origin"],
            "confusableWith": ["countryOfOrigin (PHARMA/FOOD/ELECTRONICS/APPAREL)"]
          },
          "semanticAction": null
        },
        {
          "key": "dfarsCompliant", "label": "DFARS", "type": "boolean",
          "voice": ["dfars","dfars compliant","specialty metals"],
          "scope": ["AEROSPACE"],
          "exampleQueries": ["DFARS-compliant receipts","non-DFARS heats"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "pyrometryChartUrl", "label": "Pyrometry Chart", "type": "url",
          "voice": ["pyrometry","heat treat chart","ac7102"],
          "scope": ["AEROSPACE"],
          "exampleQueries": ["heat-treat receipts missing pyrometry chart"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "tensileLotResult", "label": "Tensile Lot Result", "type": "text",
          "voice": ["tensile","mechanical test","lot test"],
          "scope": ["AEROSPACE"],
          "exampleQueries": ["receipts with tensile lot result"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[5, 6]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 08. CANNABIS
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('CANNABIS',
     'Cannabis (METRC seed-to-sale)',
     'State-managed cannabis seed-to-sale receipts. METRC 24-char tag + source license number mandatory; COA pass/fail required before transfer to retail.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["metrcTag","sourceLicenseNumber"],
  "properties": {
    "metrcTag":            { "type": "string", "pattern": "^[0-9A-Z]{24}$", "description": "METRC 24-character UID tag (state-issued)." },
    "sourceHarvestTag":    { "type": "string", "description": "Upstream harvest batch tag." },
    "harvestBatchNumber":  { "type": "string", "description": "Internal harvest batch identifier." },
    "strain":              { "type": "string", "description": "Strain name (e.g. Blue Dream)." },
    "cultivar":            { "type": "string", "description": "Cultivar / phenotype designator." },
    "sourceLicenseNumber": { "type": "string", "description": "Originating-licensee state license number." },
    "coaUrl":              { "type": "string", "format": "uri", "description": "Certificate of Analysis URL." },
    "coaPassed":           { "type": "boolean", "description": "COA pass / fail (potency + contaminants)." },
    "thcPercent":          { "type": "number", "description": "Total THC percent (decarb-adjusted)." },
    "cbdPercent":          { "type": "number", "description": "Total CBD percent." },
    "harvestDate":         { "type": "string", "format": "date", "description": "Field harvest date." },
    "cureDate":            { "type": "string", "format": "date", "description": "End-of-cure date." },
    "cultivationType":     { "type": "string", "enum": ["indoor","outdoor","mixed-light","greenhouse"], "description": "Cultivation method." }
  }
}
$$::jsonb,
$$["metrcTag","sourceLicenseNumber","strain","harvestBatchNumber","coaPassed"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "METRC Identity",
      "fields": [
        {
          "key": "metrcTag", "label": "METRC Tag", "type": "text", "required": true,
          "voice": ["metrc","metrc tag","state tag","seed-to-sale tag"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["METRC tag 1A4FF03000005EE000001234","find metrc 1A4FF03..."],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","heat","udi"],
            "confusableWith": ["LotNumber (core column)","heatNumber (STEEL)","udiDi (MEDICAL_DEVICE)"]
          },
          "semanticAction": null
        },
        {
          "key": "sourceHarvestTag", "label": "Source Harvest Tag", "type": "text",
          "voice": ["harvest tag","source harvest","upstream tag"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["receipts from harvest tag 1A4FF03..."],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "harvestBatchNumber", "label": "Harvest Batch", "type": "text",
          "voice": ["harvest batch","harvest number","batch"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["harvest batch HB-2026-99"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "sourceLicenseNumber", "label": "Source License #", "type": "text", "required": true,
          "voice": ["source license","license number","cultivator license"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["receipts from license CCL-1234"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Genetics",
      "fields": [
        {
          "key": "strain", "label": "Strain", "type": "text",
          "voice": ["strain","cultivar name","variety"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["Blue Dream receipts","Gelato strain in stock"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "cultivar", "label": "Cultivar", "type": "text",
          "voice": ["cultivar","phenotype","pheno"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["cultivar Pheno-3"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "cultivationType", "label": "Cultivation", "type": "enum",
          "voice": ["indoor","outdoor","greenhouse","mixed light","cultivation type"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["indoor receipts","outdoor harvest"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Lab / COA",
      "fields": [
        {
          "key": "coaUrl", "label": "COA", "type": "url",
          "voice": ["coa","certificate of analysis","lab cert"],
          "scope": ["CANNABIS","FOOD","CHEMICAL"],
          "exampleQueries": ["receipts missing COA"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "coaPassed", "label": "COA Passed", "type": "boolean",
          "voice": ["coa pass","passed lab","passed coa","lab pass"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["receipts that failed COA","lab-passed lots"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "thcPercent", "label": "THC %", "type": "decimal",
          "voice": ["thc","thc percent","total thc","potency"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["receipts above 25 percent THC","low-THC lots"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "cbdPercent", "label": "CBD %", "type": "decimal",
          "voice": ["cbd","cbd percent","total cbd"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["high-CBD receipts"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Dates",
      "fields": [
        {
          "key": "harvestDate", "label": "Harvest Date", "type": "date",
          "voice": ["harvest","harvested","cut date"],
          "scope": ["CANNABIS","FOOD"],
          "exampleQueries": ["harvested within last 30 days"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "cureDate", "label": "Cure Date", "type": "date",
          "voice": ["cure","cured","end of cure"],
          "scope": ["CANNABIS"],
          "exampleQueries": ["cured before October"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 09. AUTOMOTIVE
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('AUTOMOTIVE',
     'Automotive (IATF 16949 / PPAP)',
     'Tier-1 / tier-2 automotive part receipts under IATF 16949 + AIAG PPAP + IMDS. Supplier code + part # + date code + PPAP level mandatory; PSW status gates production release.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["supplierCode","partNumber","dateCode","ppapLevel"],
  "properties": {
    "supplierCode":           { "type": "string", "description": "OEM-assigned supplier code." },
    "supplierDuns":           { "type": "string", "pattern": "^[0-9]{9}$", "description": "D-U-N-S 9-digit identifier." },
    "partNumber":             { "type": "string", "description": "Engineering part number." },
    "changeLevel":            { "type": "string", "description": "Engineering change level / revision." },
    "dateCode":               { "type": "string", "description": "Date code stamped on part." },
    "ppapLevel":              { "type": "number", "enum": [1,2,3,4,5], "description": "AIAG PPAP submission level." },
    "pswStatus":              { "type": "string", "enum": ["full","interim","rejected"], "description": "Part Submission Warrant status." },
    "imdsId":                 { "type": "string", "description": "IMDS (International Material Data System) submission ID." },
    "asnReference":           { "type": "string", "description": "ASN / EDI 856 reference." },
    "criticalCharacteristic": { "type": "boolean", "description": "Critical-characteristic part flag (CC / SC)." },
    "countryOfOrigin":        { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." }
  }
}
$$::jsonb,
$$["supplierCode","partNumber","dateCode","ppapLevel","pswStatus"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Supplier / Part",
      "fields": [
        {
          "key": "supplierCode", "label": "Supplier Code", "type": "text", "required": true,
          "voice": ["supplier code","oem supplier code","supplier id"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["receipts from supplier code SUP-12345"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "supplierDuns", "label": "D-U-N-S", "type": "text",
          "voice": ["duns","d-u-n-s","duns number"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["receipts under DUNS 123456789"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "partNumber", "label": "Part #", "type": "text", "required": true,
          "voice": ["part number","part #","p/n","engineering part"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["part number 12345-AB receipts"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["mpn","sku","item"],
            "confusableWith": ["mpn (ELECTRONICS)","Items.ItemCode (core column)"]
          },
          "semanticAction": null
        },
        {
          "key": "changeLevel", "label": "Change Level", "type": "text",
          "voice": ["change level","revision","rev","engineering change"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["receipts on change level B","rev C parts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "dateCode", "label": "Date Code", "type": "text", "required": true,
          "voice": ["date code","stamped date"],
          "scope": ["AUTOMOTIVE","ELECTRONICS"],
          "exampleQueries": ["date code 2540 receipts"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "PPAP",
      "fields": [
        {
          "key": "ppapLevel", "label": "PPAP Level", "type": "number", "required": true,
          "voice": ["ppap","ppap level","production part approval"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["PPAP level 3 receipts","level 4 PPAP submissions"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "pswStatus", "label": "PSW Status", "type": "enum",
          "voice": ["psw","psw status","part submission warrant"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["interim PSW receipts","rejected PSW parts","full PSW approved"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "criticalCharacteristic", "label": "Critical Characteristic", "type": "boolean",
          "voice": ["critical characteristic","cc","sc","safety characteristic"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["critical-characteristic part receipts"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Material / Logistics",
      "fields": [
        {
          "key": "imdsId", "label": "IMDS ID", "type": "text",
          "voice": ["imds","imds id","material data"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["receipts missing IMDS"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "asnReference", "label": "ASN Reference", "type": "text",
          "voice": ["asn","asn reference","edi 856","advance ship notice"],
          "scope": ["AUTOMOTIVE"],
          "exampleQueries": ["receipts under ASN 12345"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in"],
          "scope": ["AUTOMOTIVE","PHARMA","FOOD","ELECTRONICS","APPAREL","CHEMICAL","MEDICAL_DEVICE"],
          "exampleQueries": ["parts made in Mexico"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[9]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 10. APPAREL
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('APPAREL',
     'Apparel / Textile',
     'Roll-and-dye-lot apparel and textile receipts. Roll/dye lot mandatory for shade-matching across cut tickets; supports GOTS / OEKO-TEX certifications and HTS classification.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["rollDyeLot"],
  "properties": {
    "rollDyeLot":        { "type": "string", "description": "Combined roll / dye lot identifier — shade-matching key." },
    "color":             { "type": "string", "description": "Color name / code." },
    "size":              { "type": "string", "description": "Size designation when applicable." },
    "fiberComposition":  { "type": "string", "description": "Fiber-composition statement (e.g. '100 percent cotton')." },
    "widthCm":           { "type": "number", "description": "Roll width in centimeters." },
    "rollLengthM":       { "type": "number", "description": "Roll length in meters." },
    "gsm":               { "type": "number", "description": "Grams per square meter." },
    "htsClassification": { "type": "string", "description": "Harmonized Tariff Schedule code." },
    "countryOfOrigin":   { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of origin." },
    "gotsCertNumber":    { "type": "string", "description": "GOTS (Global Organic Textile Standard) certificate number." },
    "oekoTexCertNumber": { "type": "string", "description": "OEKO-TEX Standard 100 certificate number." }
  }
}
$$::jsonb,
$$["rollDyeLot","color","fiberComposition","countryOfOrigin","htsClassification"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Roll / Shade",
      "fields": [
        {
          "key": "rollDyeLot", "label": "Roll / Dye Lot", "type": "text", "required": true,
          "voice": ["roll","dye lot","roll dye lot","shade lot"],
          "scope": ["APPAREL"],
          "exampleQueries": ["all rolls from dye lot DL-2026-99","shade lot 12345"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","batch"],
            "confusableWith": ["LotNumber (core column, all profiles)"]
          },
          "semanticAction": null
        },
        {
          "key": "color", "label": "Color", "type": "text",
          "voice": ["color","colour","shade"],
          "scope": ["APPAREL"],
          "exampleQueries": ["navy blue rolls","color 14-4318"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "size", "label": "Size", "type": "text",
          "voice": ["size"],
          "scope": ["APPAREL"],
          "exampleQueries": ["size XL receipts"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Fabric Spec",
      "fields": [
        {
          "key": "fiberComposition", "label": "Fiber Composition", "type": "text",
          "voice": ["fiber composition","fiber content","content","material"],
          "scope": ["APPAREL"],
          "exampleQueries": ["100 percent cotton rolls","poly-cotton blend receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "widthCm", "label": "Width (cm)", "type": "decimal",
          "voice": ["width","cm wide","fabric width"],
          "scope": ["APPAREL"],
          "exampleQueries": ["rolls wider than 150cm"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "rollLengthM", "label": "Roll Length (m)", "type": "decimal",
          "voice": ["roll length","meters","yardage"],
          "scope": ["APPAREL"],
          "exampleQueries": ["rolls longer than 100m"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "gsm", "label": "GSM", "type": "decimal",
          "voice": ["gsm","grams per square meter","weight"],
          "scope": ["APPAREL"],
          "exampleQueries": ["fabrics above 200 gsm"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Compliance",
      "fields": [
        {
          "key": "htsClassification", "label": "HTS", "type": "text",
          "voice": ["hts","tariff","hts code","harmonized tariff"],
          "scope": ["APPAREL"],
          "exampleQueries": ["receipts under HTS 6109.10"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfOrigin", "label": "Country of Origin", "type": "iso2",
          "voice": ["country of origin","origin","made in"],
          "scope": ["APPAREL","PHARMA","FOOD","ELECTRONICS","CHEMICAL","MEDICAL_DEVICE","AUTOMOTIVE"],
          "exampleQueries": ["rolls from Vietnam","made in Bangladesh"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "gotsCertNumber", "label": "GOTS Cert #", "type": "text",
          "voice": ["gots","gots cert","organic textile"],
          "scope": ["APPAREL"],
          "exampleQueries": ["GOTS-certified rolls"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "oekoTexCertNumber", "label": "OEKO-TEX Cert #", "type": "text",
          "voice": ["oeko-tex","oekotex","standard 100"],
          "scope": ["APPAREL"],
          "exampleQueries": ["OEKO-TEX certified material"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 11. CONSTRUCTION
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('CONSTRUCTION',
     'Construction / Ready-Mix / MRO',
     'Construction material receipts — ready-mix concrete, paint, sealants, MRO. Batch number + manufacture date mandatory; cure-by / shelf-life clock relevant for many SKUs.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["batchNumber","manufactureDate"],
  "properties": {
    "batchNumber":       { "type": "string", "description": "Producer-assigned batch number." },
    "plantCode":         { "type": "string", "description": "Producing plant code." },
    "manufactureDate":   { "type": "string", "format": "date", "description": "Date of manufacture / batch." },
    "cureByDate":        { "type": "string", "format": "date", "description": "Cure-by / use-by date." },
    "specCompliance":    { "type": "string", "description": "Spec compliance reference (ASTM C94, etc.)." },
    "mixDesignId":       { "type": "string", "description": "Concrete mix design identifier." },
    "vocContent":        { "type": "number", "description": "VOC content (g/L) for paints / sealants." },
    "slumpInches":       { "type": "number", "description": "Concrete slump test result (inches)." },
    "designStrengthPsi": { "type": "number", "description": "Concrete design strength (psi)." }
  }
}
$$::jsonb,
$$["batchNumber","plantCode","manufactureDate","mixDesignId"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Batch / Plant",
      "fields": [
        {
          "key": "batchNumber", "label": "Batch #", "type": "text", "required": true,
          "voice": ["batch","batch number","ready-mix batch"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["batch 12345 receipts","ready-mix batch B-9988"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","heat"],
            "confusableWith": ["LotNumber (core column)","heatNumber (STEEL)"]
          },
          "semanticAction": null
        },
        {
          "key": "plantCode", "label": "Plant Code", "type": "text",
          "voice": ["plant","plant code","ready-mix plant"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["receipts from plant 03"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Dates",
      "fields": [
        {
          "key": "manufactureDate", "label": "Mfg Date", "type": "date", "required": true,
          "voice": ["manufactured","mfg date","made","batched"],
          "scope": ["CONSTRUCTION","PHARMA","CHEMICAL","MEDICAL_DEVICE"],
          "exampleQueries": ["batched this morning","manufactured within last 24 hours"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "cureByDate", "label": "Cure By", "type": "date",
          "voice": ["cure by","use by","shelf life"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["receipts curing within 7 days"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["expiration","best by"],
            "confusableWith": ["expirationDate (PHARMA/MEDICAL_DEVICE)","bestByDate (FOOD)","shelfLifeDate (CHEMICAL)"]
          },
          "semanticAction": null
        }
      ]
    },
    {
      "title": "Spec / Performance",
      "fields": [
        {
          "key": "specCompliance", "label": "Spec Compliance", "type": "text",
          "voice": ["spec","spec compliance","astm c94"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["ASTM C94 compliant receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "mixDesignId", "label": "Mix Design ID", "type": "text",
          "voice": ["mix design","mix id","concrete mix"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["mix design MD-4000-PSI"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "vocContent", "label": "VOC (g/L)", "type": "decimal",
          "voice": ["voc","voc content","volatile organic"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["receipts above 50 g/L VOC","low-VOC paint"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "slumpInches", "label": "Slump (in)", "type": "decimal",
          "voice": ["slump","slump test"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["concrete with slump above 5 inches"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "designStrengthPsi", "label": "Design Strength (psi)", "type": "decimal",
          "voice": ["design strength","psi","strength"],
          "scope": ["CONSTRUCTION"],
          "exampleQueries": ["4000 psi concrete","design strength above 3000 psi"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- ---------------------------------------------------------------------
-- 12. OIL_GAS
-- ---------------------------------------------------------------------
INSERT INTO "ReceiptProfiles"
    ("Code", "Name", "Description",
     "JsonSchema", "PromotedFacets", "DefaultAttributes",
     "UiFormSpec", "RegulatoryProfileIds",
     "IsActive", "CreatedAt", "CreatedBy")
VALUES
    ('OIL_GAS',
     'Oil and Gas (API line pipe / OCTG)',
     'Oil-and-gas line pipe / OCTG receipts under API 5L, API 5CT, NACE MR0175 sour service. Heat + API spec + API grade + joint serial mandatory; hydrotest record required for line pipe.',
$$
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": ["heatNumber","apiSpec","apiGrade"],
  "properties": {
    "heatNumber":            { "type": "string", "description": "Mill-issued heat / melt identifier." },
    "mill":                  { "type": "string", "description": "Producing mill." },
    "millCertUrl":           { "type": "string", "format": "uri", "description": "URL to Certified Mill Test Report." },
    "apiSpec":               { "type": "string", "description": "API spec (5L line pipe, 5CT casing/tubing, 6A wellhead)." },
    "apiGrade":              { "type": "string", "description": "API grade (X42, X52, X65, X70, X80 / L80, P110, T95, etc.)." },
    "jointSerialNumber":     { "type": "string", "description": "Per-joint serial number." },
    "pressureRatingPsi":     { "type": "number", "description": "Pressure rating in psi." },
    "scheduleNumber":        { "type": "string", "description": "Pipe schedule (SCH 40, SCH 80, etc.)." },
    "wallThicknessIn":       { "type": "number", "description": "Wall thickness in inches." },
    "lengthFt":              { "type": "number", "description": "Joint length in feet." },
    "sourSvc":               { "type": "boolean", "description": "NACE MR0175 sour-service rated." },
    "hydrotestPressurePsi":  { "type": "number", "description": "Mill hydrotest pressure (psi)." },
    "countryOfMelt":         { "type": "string", "maxLength": 2, "description": "ISO 3166-1 alpha-2 country of melt." }
  }
}
$$::jsonb,
$$["heatNumber","apiSpec","apiGrade","jointSerialNumber","sourSvc"]$$::jsonb,
$${}$$::jsonb,
$$
{
  "groups": [
    {
      "title": "Heat / Mill",
      "fields": [
        {
          "key": "heatNumber", "label": "Heat #", "type": "text", "required": true,
          "voice": ["heat","heat number","melt id"],
          "scope": ["OIL_GAS","STEEL","AEROSPACE"],
          "exampleQueries": ["heat H-12345","oil-and-gas heats from Tenaris"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["lot","batch","serial","tag"],
            "confusableWith": ["LotNumber (core column)","jointSerialNumber (OIL_GAS)","traceabilityLotCode (FOOD)"]
          },
          "semanticAction": null
        },
        {
          "key": "mill", "label": "Mill", "type": "text",
          "voice": ["mill","producer","melt source"],
          "scope": ["OIL_GAS","STEEL","AEROSPACE"],
          "exampleQueries": ["material from Tenaris"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "millCertUrl", "label": "Mill Cert URL", "type": "url",
          "voice": ["mill cert","mtr","cmtr","mill test report"],
          "scope": ["OIL_GAS","STEEL","AEROSPACE"],
          "exampleQueries": ["receipts missing mill cert"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "API Spec",
      "fields": [
        {
          "key": "apiSpec", "label": "API Spec", "type": "text", "required": true,
          "voice": ["api","api spec","api 5l","api 5ct"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["API 5L line pipe","API 5CT casing receipts"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["astm","ams"],
            "confusableWith": ["astmDesignation (STEEL)","amsSpec (AEROSPACE)"]
          },
          "semanticAction": null
        },
        {
          "key": "apiGrade", "label": "API Grade", "type": "text", "required": true,
          "voice": ["api grade","grade","x65","l80","p110"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["X65 pipe receipts","L80 casing","P110 tubing"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Joint / Dimensions",
      "fields": [
        {
          "key": "jointSerialNumber", "label": "Joint Serial #", "type": "text",
          "voice": ["joint serial","joint number","per joint"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["joint serial J-12345","trace joint J-12345"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["heat","lot"],
            "confusableWith": ["SerialNumber (core column)","heatNumber (OIL_GAS/STEEL/AEROSPACE)"]
          },
          "semanticAction": "traceChainOfCustody"
        },
        {
          "key": "scheduleNumber", "label": "Schedule", "type": "text",
          "voice": ["schedule","sch","pipe schedule"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["SCH 40 pipe","schedule 80 receipts"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "wallThicknessIn", "label": "Wall Thickness (in)", "type": "decimal",
          "voice": ["wall thickness","wall","thickness"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["wall thickness over 0.5 inches"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "lengthFt", "label": "Joint Length (ft)", "type": "decimal",
          "voice": ["length","feet","joint length"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["joints longer than 40 feet"],
          "disambiguation": null, "semanticAction": null
        }
      ]
    },
    {
      "title": "Service / Test",
      "fields": [
        {
          "key": "pressureRatingPsi", "label": "Pressure Rating (psi)", "type": "decimal",
          "voice": ["pressure rating","working pressure","mawp"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["pressure above 5000 psi"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "sourSvc", "label": "Sour Service", "type": "boolean",
          "voice": ["sour","sour service","nace","mr0175","h2s service"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["sour-service receipts","NACE MR0175 rated stock"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "hydrotestPressurePsi", "label": "Hydrotest (psi)", "type": "decimal",
          "voice": ["hydrotest","hydrostatic test","mill test pressure"],
          "scope": ["OIL_GAS"],
          "exampleQueries": ["receipts hydrotested above 3000 psi"],
          "disambiguation": null, "semanticAction": null
        },
        {
          "key": "countryOfMelt", "label": "Country of Melt", "type": "iso2",
          "voice": ["country of melt","melt origin"],
          "scope": ["OIL_GAS","STEEL","AEROSPACE"],
          "exampleQueries": ["heats melted in US"],
          "disambiguation": {
            "phrasesThatAreNOTThisField": ["country of origin"],
            "confusableWith": ["countryOfOrigin (PHARMA/FOOD/ELECTRONICS/APPAREL)"]
          },
          "semanticAction": null
        }
      ]
    }
  ]
}
$$::jsonb,
$$[]$$::jsonb,
TRUE, now(), 'migration:ADR-015')
ON CONFLICT ("Code") DO NOTHING;

-- =====================================================================
-- End of ReceiptProfilesSeed.sql — 12 profiles seeded.
-- =====================================================================
