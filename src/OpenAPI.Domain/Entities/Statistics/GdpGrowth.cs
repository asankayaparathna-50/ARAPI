namespace OpenAPI.Domain.Entities.Statistics
{
    public class GdpItem
    {
        public string? Item { get; set; }
        public string? ItemName { get; set; }
        public string? PeriodId { get; set; }
        public decimal? CurrentValue { get; set; }
        public string Frequency { get; set; } = string.Empty;
    }  
}