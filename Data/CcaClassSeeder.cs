using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Data
{
    public static class CcaClassSeeder
    {
        public static async Task SeedCcaClassesAsync(AppDbContext db)
        {
            if (await db.CcaClasses.AnyAsync())
                return;

            var ccaClasses = new List<CcaClass>
            {
                new() { ClassNumber = 1, Rate = 0.04m, Description = "Buildings acquired after 1987 (brick, stone, cement)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 3, Rate = 0.05m, Description = "Buildings acquired before 1988 and improvements to Class 1/3 buildings", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 6, Rate = 0.10m, Description = "Frame, log, stucco on frame, galvanized or corrugated iron buildings, fences, greenhouses", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 7, Rate = 0.15m, Description = "Canoes, boats, most vessels, furniture and fittings", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 8, Rate = 0.20m, Description = "Property not included in another class (machinery, equipment, furniture)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 9, Rate = 0.25m, Description = "Aircraft, including furniture, fittings, parts", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 10, Rate = 0.30m, Description = "Automotive equipment, general-purpose electronic data processing equipment", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 12, Rate = 1.00m, Description = "China, cutlery, linen, uniforms, dies, jigs, patterns, moulds, tools (under $500)", IsDecliningBalance = true, HalfYearRuleApplies = false, Notes = "100% write-off in year of acquisition" },
                new() { ClassNumber = 13, Rate = 0m, Description = "Leasehold improvements (straight-line over term)", IsDecliningBalance = false, HalfYearRuleApplies = false, Notes = "Calculated over lease term" },
                new() { ClassNumber = 14, Rate = 0m, Description = "Patents, franchises, concessions, licenses (limited life)", IsDecliningBalance = false, HalfYearRuleApplies = false, Notes = "Straight-line over life" },
                new() { ClassNumber = 141, Rate = 0.05m, Description = "Goodwill and other eligible capital property", IsDecliningBalance = true, HalfYearRuleApplies = true, Notes = "Replaced Class 14 after 2016" },
                new() { ClassNumber = 16, Rate = 0.40m, Description = "Automobiles for lease/rent, taxicabs, coin-operated video games", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 17, Rate = 0.08m, Description = "Parking lots, sidewalks, roads, storage areas", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 29, Rate = 0.50m, Description = "Manufacturing and processing equipment (acquired before 2016)", IsDecliningBalance = false, HalfYearRuleApplies = true, Notes = "Straight-line 50% per year" },
                new() { ClassNumber = 38, Rate = 0.30m, Description = "Property used in a power plant (specified equipment)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 39, Rate = 0.25m, Description = "Machinery and equipment for manufacturing/processing (acquired 2016+)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 43, Rate = 0.30m, Description = "Clean energy generation equipment", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 44, Rate = 0.25m, Description = "Patents and licenses (no limited life)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 45, Rate = 0.45m, Description = "Computer equipment and systems software (acquired after March 22, 2004)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 46, Rate = 0.30m, Description = "Data network infrastructure equipment", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 50, Rate = 0.55m, Description = "General-purpose electronic data processing equipment (acquired after Jan 2011)", IsDecliningBalance = true, HalfYearRuleApplies = true },
                new() { ClassNumber = 53, Rate = 0.50m, Description = "Manufacturing and processing machinery and equipment (acquired after 2015)", IsDecliningBalance = true, HalfYearRuleApplies = true, IsAcceleratedInvestmentIncentive = true },
                new() { ClassNumber = 54, Rate = 0.30m, Description = "Zero-emission passenger vehicles (acquired after March 18, 2019)", IsDecliningBalance = true, HalfYearRuleApplies = true, IsAcceleratedInvestmentIncentive = true },
                new() { ClassNumber = 55, Rate = 0.40m, Description = "Zero-emission vehicles other than passenger vehicles", IsDecliningBalance = true, HalfYearRuleApplies = true, IsAcceleratedInvestmentIncentive = true },
                new() { ClassNumber = 56, Rate = 0.30m, Description = "Zero-emission automotive equipment (trucks, vans)", IsDecliningBalance = true, HalfYearRuleApplies = true, IsAcceleratedInvestmentIncentive = true }
            };

            db.CcaClasses.AddRange(ccaClasses);
            await db.SaveChangesAsync();
        }
    }
}
