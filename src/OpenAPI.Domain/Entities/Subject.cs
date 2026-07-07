namespace OpenAPI.Domain.Entities
{
    public class Subject
    {
        public int Id { get; set; }
        public string? Name { get; set; } // nchar(50)
        public string? SectorCode { get; set; } // varchar(10)
        public int Sts2 { get; set; }
    }
}
