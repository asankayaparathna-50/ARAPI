namespace OpenAPI.Domain.Entities.Auth
{
    public class SdmxSettings
    {
        public AgencySettings Agency { get; set; } = new();
        public DataStructuresSettings DataStructures { get; set; } = new();
        public CommonSettings Common { get; set; } = new();
    }

    public class AgencySettings
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }

    public class DataStructuresSettings
    {
        public DataStructureSettings PriceIndices { get; set; } = new();
        public MarketPricesDataStructureSettings MarketPrices { get; set; } = new();
    }

    public class DataStructureSettings
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WeeklyFrequency { get; set; } = string.Empty;
        public string MonthlyFrequency { get; set; } = string.Empty;
        public string ObservationStatusAvailable { get; set; } = string.Empty;
        public string UnitMeasureIndex { get; set; } = string.Empty;
    }

    public class CommonSettings
    {
        public string DimensionAtObservation { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ReferenceArea { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }

    public class MarketPricesDataStructureSettings
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MessageIdPrefix { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string ObservationStatusAvailable { get; set; } = string.Empty;
        public string ReferenceAreaName { get; set; } = string.Empty;
        public MarketTypeSettings MarketTypes { get; set; } = new();
        public DimensionSettings Dimensions { get; set; } = new();
    }

    public class MarketTypeSettings
    {
        public string Wholesale { get; set; } = string.Empty;
        public string WholesaleName { get; set; } = string.Empty;
        public string Retail { get; set; } = string.Empty;
        public string RetailName { get; set; } = string.Empty;
    }

    public class DimensionSettings
    {
        public DimensionConfig Frequency { get; set; } = new();
        public DimensionConfig MarketType { get; set; } = new();
        public DimensionConfig Item { get; set; } = new();
        public DimensionConfig ReferenceArea { get; set; } = new();
        public DimensionConfig TimePeriod { get; set; } = new();
    }

    public class DimensionConfig
    {
        public string Id { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ConceptRef { get; set; } = string.Empty;
    }
}