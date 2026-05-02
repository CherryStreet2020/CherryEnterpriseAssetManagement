using System;
using System.Collections.Generic;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services
{
    /// <summary>
    /// Maps an Asset (primarily its <see cref="Asset.AssetType"/> string and InServiceDate) to a
    /// recommended Canadian CCA class number per CRA Schedule II.
    ///
    /// This is a *starting point* — accountants will refine per-asset overrides during the
    /// CcaBackfill run. The suggester intentionally errs on the side of mainstream classes
    /// (8, 10, 50, 53) and never returns special/limited-life classes (13/14/141/16) implicitly,
    /// since those require human judgement.
    /// </summary>
    public static class CcaClassSuggester
    {
        // Common CRA class numbers — keep in sync with Data/CcaClassSeeder.cs.
        public const int Class1_BuildingsPost1987 = 1;
        public const int Class8_GeneralMachinery = 8;          // 20% DB — default fallback
        public const int Class10_AutomotiveAndOldComputers = 10;
        public const int Class12_FullWriteoff = 12;            // 100% — software, tools <$500, dies, jigs, patterns
        public const int Class17_LandImprovements = 17;        // 8% DB — parking lots, sidewalks, roads
        public const int Class29_PreManufacturing2016 = 29;    // 50% SL — pre-2016 manufacturing
        public const int Class39_PostManufacturing2016 = 39;   // 25% DB — post-2015 generic M&P
        public const int Class50_GeneralComputers = 50;        // 55% DB — post-Jan 2011 computers + systems software
        public const int Class53_ManufacturingPost2015 = 53;   // 50% DB + AII — post-2015 manufacturing M&E

        // Threshold below which "tools" qualify for Class 12 (100% writeoff) under CRA rules.
        public const decimal ToolsClass12CostThreshold = 500m;

        // Year boundary for Class 53 eligibility (acquired after 2015).
        public const int Class53MinYear = 2016;

        /// <summary>
        /// Returns the recommended CRA CCA class number for the given asset.
        /// Pure function — safe to call from any context.
        /// </summary>
        public static int Suggest(Asset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            var type = (asset.AssetType ?? string.Empty).Trim().ToUpperInvariant();
            var year = asset.InServiceDate.Year;

            // Buildings — Class 1 (post-1987 buildings, 4% DB).
            if (ContainsAny(type, "BUILDING", "FACILITY", "WAREHOUSE", "PLANT STRUCTURE"))
                return Class1_BuildingsPost1987;

            // Land improvements — Class 17 (parking lots, sidewalks, roads, fences in some cases).
            if (ContainsAny(type, "PARKING", "SIDEWALK", "ROAD", "PAVEMENT", "LAND IMPROVEMENT"))
                return Class17_LandImprovements;

            // Office furniture & fittings — Class 8.
            if (ContainsAny(type, "FURNITURE", "DESK", "CHAIR", "OFFICE EQUIPMENT", "CABINET"))
                return Class8_GeneralMachinery;

            // Computer software — Class 12 (100% writeoff).
            if (ContainsAny(type, "SOFTWARE", "LICENSE"))
                return Class12_FullWriteoff;

            // Computer hardware — Class 50 (post-Jan 2011, 55% DB) is the modern default;
            // anything older falls into Class 10 (30% DB) but we don't try to guess.
            if (ContainsAny(type, "COMPUTER", "LAPTOP", "SERVER", "WORKSTATION", "NETWORK", "ROUTER", "SWITCH"))
                return Class50_GeneralComputers;

            // Vehicles & general automotive equipment — Class 10 (30% DB).
            // Note: passenger vehicles >$36,000 (2024 limit) are Class 10.1 — out of scope here.
            // Forklifts are commonly Class 10 when used in general material handling.
            if (ContainsAny(type, "VEHICLE", "AUTOMOBILE", "TRUCK", "CAR ", "FORKLIFT", "VAN", "TRAILER"))
                return Class10_AutomotiveAndOldComputers;

            // Tools / dies / jigs / moulds / patterns — Class 12 if individually under threshold,
            // otherwise general Class 8. We can't always tell at the type level, so use cost.
            if (ContainsAny(type, "DIE", "JIG", "MOULD", "MOLD", "PATTERN", "FIXTURE", "TOOL"))
                return asset.AcquisitionCost > 0 && asset.AcquisitionCost < ToolsClass12CostThreshold
                    ? Class12_FullWriteoff
                    : Class8_GeneralMachinery;

            // Manufacturing / processing equipment — Class 53 (post-2015, 50% DB + AII)
            // or Class 29 (pre-2016, 50% SL). Covers CNC, lathes, mills, presses, robots,
            // welders, conveyors, compressors, pumps, motors, transformers, cranes, HVAC, etc.
            if (IsManufacturingEquipment(type))
            {
                return year >= Class53MinYear
                    ? Class53_ManufacturingPost2015
                    : Class29_PreManufacturing2016;
            }

            // Default: Class 8 (general 20% DB, "property not included in another class").
            return Class8_GeneralMachinery;
        }

        /// <summary>
        /// Resolves the suggested <see cref="CcaClass.Id"/> using the supplied lookup map.
        /// Returns null if no class with that number is registered.
        /// </summary>
        public static int? SuggestCcaClassId(Asset asset, IReadOnlyDictionary<int, int> classIdByClassNumber)
        {
            var classNumber = Suggest(asset);
            if (classIdByClassNumber.TryGetValue(classNumber, out var id))
                return id;

            // Fallback: try Class 8 (general fallback) if the suggested class isn't seeded.
            if (classNumber != Class8_GeneralMachinery
                && classIdByClassNumber.TryGetValue(Class8_GeneralMachinery, out var fallbackId))
                return fallbackId;

            return null;
        }

        private static bool IsManufacturingEquipment(string type)
        {
            return ContainsAny(type,
                "CNC", "MILL", "LATHE", "PRESS", "GRINDER", "MACHINE",
                "CONVEYOR", "ROBOT", "WELDER", "COMPRESSOR", "PUMP",
                "HVAC", "TRANSFORMER", "MOTOR", "CRANE", "MIXER",
                "EXTRUDER", "INJECTION", "STAMPING", "MANUFACTURING",
                "PROCESSING", "PRODUCTION", "ASSEMBLY");
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            foreach (var n in needles)
            {
                if (haystack.Contains(n, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
