namespace OpenAPI.Domain.Entities.Auth
{
    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenLifetimeMinutes { get; set; } = 60;
        public int RefreshTokenLifetimeMinutes { get; set; } = 60*24*30;
    }
}
