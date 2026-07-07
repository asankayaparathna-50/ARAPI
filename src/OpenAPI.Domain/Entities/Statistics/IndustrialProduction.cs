namespace OpenAPI.Domain.Entities.Statistics
{
    public class IndustrialProductionItem
    {
        public string? ProductName { get; set; }
        public decimal? CurrentYear { get; set; }
        public decimal? PreviousYear { get; set; }
        public decimal? PercentageChange { get; set; }
    }

    public class IndustrialProduction
    {
        public string? Period { get; set; }
        public Dictionary<string, Dictionary<string, decimal?>>? Data { get; set; } = new Dictionary<string, Dictionary<string, decimal?>>();
        public List<ProductionItem> IndustrialProductionIndex { get; set; } = new List<ProductionItem>();
    }
}
