namespace OpenAPI.Domain.Entities
{
    public class ExchangeRate
    {
        public DateOnly? Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string? Value { get; set; }
        //public decimal? BuyRate_LKR { get; set; }
        //public decimal? SellRate_LKR { get; set; }
    }

    public class ExchangeRateBuyingSelling
    {
        public DateOnly? Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal? BuyRate_LKR { get; set; }
        public decimal? SellRate_LKR { get; set; }
    }

}
