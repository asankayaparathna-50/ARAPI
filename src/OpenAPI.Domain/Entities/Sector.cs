namespace OpenAPI.Domain.Entities
{
    public class Sector
    {
        public string SectorCode { get; set; } = string.Empty; // varchar(10)
        public string? Name { get; set; } // varchar(50)
    }
}
