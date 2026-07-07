using Microsoft.AspNetCore.Mvc;

namespace OpenAPI.Domain.Entities.Auth
{
    public class RefreshToken
    {
        public int Id { get; set; }

        [FromForm(Name = "client_id")]
        public string ClientId { get; set; } = string.Empty;

        [FromForm(Name = "refresh_token")]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
    }
}
