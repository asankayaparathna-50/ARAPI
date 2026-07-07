namespace OpenAPI.Domain.Entities
{
    public class DataValueReturn
    {
        public List<DataValueMain>? DataValueMain { get; set; }
        public List<DataValueNotes>? DataValueNotes { get; set; }

    }

    public class DataValueMain
    {
        public string? ItemName { get; set; }
        public List<DataValues>? DataValues { get; set; }
    }

    public class DataValues
    {
        public string ItemName { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Scale { get; set; } = string.Empty;
        public string PeriodID { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class DataValueNotes
    {
        public string ItemName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string GeoArea { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string DataLastUpdated { get; set; } = string.Empty;
        public string UpdateFrequency { get; set; } = string.Empty;
    }
}
