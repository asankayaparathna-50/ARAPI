namespace OpenAPI.Domain.Entities
{
    public class ClientAppSetting
    {
        public required string Key { get; set; }
        public required string Value { get; set; }
        public string Description { get; set; } = string.Empty;

    }
}
