namespace OpenAPI.Domain.Entities.Statistics
{
    public class PriceIndices
    {
        public List<Dictionary<string, object>> priceIndices { get; set; } = new();
    }

    public class PriceIndicesDetails
    {
        public string? Key { get; set; }
        public string? Period { get; set; }        // Period identifier (e.g., 2025-01-00)
        public decimal? Value { get; set; }  // Current period value
    }

    // DTO for raw data from SP_GetPriceIndices_ByPeriod
    public class PriceIndexRawData
    {
        public string ItemName { get; set; } = string.Empty;
        public int DataCodeID { get; set; }
        public string? PeriodID { get; set; }        // Period identifier (e.g., 2025-01-00)
        public decimal? CurrentValue { get; set; }  // Current period value
    }
}
