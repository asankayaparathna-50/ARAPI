namespace OpenAPI.Domain.Entities.Statistics
{
    public class AgriculturalProduction
    {
        public string? Period { get; set; }
        public Dictionary<string, Dictionary<string, decimal?>>? Data { get; set; } = new Dictionary<string, Dictionary<string, decimal?>>();
    }

    // Legacy class for backward compatibility (no longer used)
    public class ProductionItem
    {
        //public string? Product { get; set; }
        public string? ProductName { get; set; }
        public decimal? CurrentYear { get; set; }
        public decimal? PreviousYear { get; set; }
        public decimal? PercentageChange { get; set; }
    }
}
