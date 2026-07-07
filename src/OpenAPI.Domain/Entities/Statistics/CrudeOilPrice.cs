namespace OpenAPI.Domain.Entities.Statistics
{
    public class CrudeOilPrice
    {
        public List<CrudeOilPriceMonthlyItem> MonthlyPrices { get; set; } = new List<CrudeOilPriceMonthlyItem>();
        public List<CrudeOilPriceDailyItem> DailyPrices { get; set; } = new List<CrudeOilPriceDailyItem>();
    }

    public class CrudeOilPriceMonthlyItem
    {
        public string? MonthName { get; set; }
        public string? Year { get; set; }
        public string? CrudeOilFuturesPricesBrent { get; set; }
        public string? CrudeOilFuturesPricesWTI { get; set; }
        public string? CPCImportPrices { get; set; }
    }

    public class CrudeOilPriceDailyItem
    {
        public string? DayLabel { get; set; }
        public int? Year { get; set; }
        public string? CrudeOilFuturesPricesBrent { get; set; }
        public string? CrudeOilFuturesPricesWTI { get; set; }
    }

}
