namespace OpenAPI.Domain.Entities.Statistics
{
    public class Pmi
    {
        public List<PmiItem> PMIManufacturing { get; set; } = new List<PmiItem>();
        public List<PmiItem> PMIServices { get; set; } = new List<PmiItem>();
        public List<PmiItem> PMIConstruction { get; set; } = new List<PmiItem>();
    }

    public class PmiItem
    {
        public string? IndexName { get; set; }
        public decimal? CurrentMonth { get; set; }
        public decimal? PreviousMonth { get; set; }
        public decimal? YearAgo { get; set; }
        public decimal? CurrentYearDoubleMonthAgo { get; set; }
        public decimal? YearBackAndMonthAgo { get; set; }
        public decimal? YearBackAndDoubleMonthAgo { get; set; }
    }
}
