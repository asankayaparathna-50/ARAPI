namespace OpenAPI.Domain.Entities
{
    public class DataValue
    {
        public int DataCodeID { get; set; }
        public string? PeriodID { get; set; }
        public string? Value { get; set; }
        public string? FootNote { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
        public string? FrequencyCode { get; set; }
        //public virtual DataCode DataCode { get; set; }
        //[ForeignKey("PeriodID")]
        //public virtual Period Period { get; set; }
    }
}
