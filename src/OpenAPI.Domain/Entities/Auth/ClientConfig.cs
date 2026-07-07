namespace OpenAPI.Domain.Entities.Auth
{
    public record ClientConfig
    {
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string AllowedScopes { get; init; } = string.Empty;
        public string RefreshedToken { get; init; } = string.Empty;
        public DateTime LastAccessedOn { get; set; }
        public DateTime SecretExpiresOn { get; set; }
        public string[] ScopesArray =>
        string.IsNullOrWhiteSpace(AllowedScopes)
            ? Array.Empty<string>()
            : AllowedScopes.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim().ToLowerInvariant())
                           .ToArray();
    }
}
