namespace OpenAPI.Domain.Entities
{
    public class Frequency
    {
        public string FrequencyCode { get; set; } = string.Empty; // nchar(1)
        public string? Description { get; set; }                  // nchar(20)
        public int Sts1 { get; set; }
    }
}
