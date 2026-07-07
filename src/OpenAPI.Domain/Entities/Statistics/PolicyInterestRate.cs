namespace OpenAPI.Domain.Entities.Statistics
{
    public class PolicyInterestRate
    {
        public List<Dictionary<string, object>> OPR { get; set; } = new();
    }

    public class PolicyInterestRateDetails
    {
        public string? ItemName { get; set; }               // Item Name from DataCode
        
        // numeric values returned by SP
        public decimal? YearAgoValue { get; set; }          // value for period.AddYears(-1)
        public decimal? WeekAgoValue { get; set; }          // value for period.AddDays(-7)
        public decimal? ThisWeek { get; set; }              // value for period (current)
    }

    public class InterestRateItem
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal? YearAgoValue { get; set; }
        public decimal? WeekAgoValue { get; set; }
        public decimal? ThisWeek { get; set; }
    }

    public class InterestRatesResponse
    {
        public InterestRateItem? PolicyInterestRate { get; set; }
        public List<InterestRateItem> StandingFacilityRate { get; set; } = new();
        public InterestRateItem? CallMoneyMarket { get; set; }
        public List<InterestRateItem> TreasuryBillYields { get; set; } = new();
        public InterestRateItem? LicensedCommercialBank { get; set; }
    }
}
