namespace OpenAPI.Domain.Entities
{
    public class DataCode
    {
        public int DataCodeID { get; set; }
        public string ItemName { get; set; } = string.Empty; // [Item Name]
        public string ShortName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Scale { get; set; } = string.Empty;
        public string SectorCode { get; set; } = string.Empty;
        public string GeoAreaCode { get; set; } = string.Empty;
        public string FrequencyCode { get; set; } = string.Empty;
        public int? SecurityLevel { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int? DepartmentCode { get; set; }
        public int DivisionId { get; set; }
        public string? KeyWords { get; set; }
        public int Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public int? IsQuarterStartApril { get; set; }
        public int? TopicId { get; set; }
        public string? DelayInWords { get; set; }
    }
}
