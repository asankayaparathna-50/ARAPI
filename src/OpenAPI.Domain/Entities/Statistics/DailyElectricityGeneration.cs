namespace OpenAPI.Domain.Entities.Statistics
{
    public class DailyElectricityGeneration
    {
        public Dictionary<string, string?> PeakDemandMW { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> TotalEnergyGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> HydroGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> ThermalCoalGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> ThermalOilGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> WindGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> SolarGWh { get; set; } = new Dictionary<string, string?>();
        public Dictionary<string, string?> BiomassGWh { get; set; } = new Dictionary<string, string?>();
    }

    // Helper class for Dapper mapping
    public class DailyElectricityGenerationItem
    {
        public string DayLabel { get; set; } = string.Empty;
        public string? PeakDemandMW { get; set; }
        public string? TotalEnergyGWh { get; set; }
        public string? HydroGWh { get; set; }
        public string? ThermalCoalGWh { get; set; }
        public string? ThermalOilGWh { get; set; }
        public string? WindGWh { get; set; }
        public string? SolarGWh { get; set; }
        public string? BiomassGWh { get; set; }
    }
}
