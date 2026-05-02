namespace Abs.FixedAssets.Models
{
    public enum DepreciationMethod
    {
        StraightLine = 0,
        DoubleDecliningBalance = 1,
        SumOfYearsDigits = 2,
        UnitsOfProduction = 3,
        DecliningBalance150 = 4,
        MACRS = 5,
        CustomSchedule = 6,
        CCA = 7,  // Canadian Capital Cost Allowance (class-based)
        MACRS3Year = 8,
        MACRS5Year = 9,
        MACRS7Year = 10,
        MACRS10Year = 11,
        MACRS15Year = 12,
        MACRS20Year = 13,
        MACRS27_5Year = 14,  // Residential rental
        MACRS39Year = 15,    // Nonresidential real property
        ADS = 16,            // Alternative Depreciation System
        GroupComposite = 17, // Group/Composite depreciation for similar assets
        Component = 18,      // Component depreciation (IFRS)
        Amortization = 19,   // Intangible asset amortization
        NoDepreciation = 20, // Land, artwork, non-depreciable assets
        IFRSRevaluation = 21 // IFRS revaluation model
    }

    public enum AccountingPeriodType
    {
        Standard12Month = 0,   // Standard monthly periods (Jan-Dec or fiscal)
        Period13 = 1,          // 13-period calendar (4 weeks each)
        Week544 = 2,           // 5-4-4 retail calendar
        Week454 = 3,           // 4-5-4 retail calendar
        Week445 = 4            // 4-4-5 retail calendar
    }

    public enum TaxJurisdiction
    {
        Canada = 0,
        USA = 1,
        Both = 2
    }

    public enum DepreciationConvention
    {
        FullMonth = 0,
        HalfYear = 1,
        MidMonth = 2,
        HalfMonth = 3,
        ActualDays = 4,
        ModifiedHalfYear = 5,
        FullYear = 6,
        MidQuarter = 7,
        NextMonth = 8,
        NoProrate = 9,
        FirstDayOfMonth = 10,
        LastDayOfMonth = 11
    }

    public enum AssetStatus
    {
        Active = 0,
        FullyDepreciated = 1,
        Disposed = 2,
        Transferred = 3,
        WrittenOff = 4,
        Impaired = 5,
        Held = 6
    }

    public enum BookType
    {
        Financial = 0,  // GAAP/IFRS/ASPE
        Tax = 1,        // CCA for Canada
        Management = 2  // Custom/internal
    }

    public enum PeriodStatus
    {
        Open = 0,
        Closed = 1,
        Locked = 2
    }

    public enum DepreciationRunStatus
    {
        Draft = 0,
        Posted = 1,
        Reversed = 2
    }
}
