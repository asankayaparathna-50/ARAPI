namespace OpenAPI.Domain.Entities.Statistics
{
    public class Employment
    {
        public List<EmploymentItem> EmploymentData { get; set; } = new List<EmploymentItem>();
        public List<EmployedPersonBySectorItem> EmpolyedPersonBySectorData { get; set; } = new List<EmployedPersonBySectorItem>();
    }

    public class EmploymentItem
    {
        public string? Item { get; set; }
        public string? ItemName { get; set; }
        public decimal? YearAgoValue { get; set; }
        public decimal? YearAgoWithQuarter { get; set; }
        public decimal? ThisYearWithQuarter { get; set; }
    }

    public class EmployedPersonBySectorItem
    {
        public string? Item { get; set; }
        public decimal? YearAgoValue { get; set; }
        public decimal? YearAgoWithQuarter { get; set; }
        public decimal? ThisYearWithQuarter { get; set; }
    }
}
