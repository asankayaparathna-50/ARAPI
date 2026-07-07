using Microsoft.AspNetCore.Mvc;

namespace OpenAPI.Domain.Entities.Auth
{
    /// <summary>
    /// Represents the request body for the OAuth 2.0 Token endpoint (Client Credentials Flow).
    /// </summary>
    public class TokenRequest
    {
        // [FromForm] attributes map keys from the HTTP request body to the properties.
        [FromForm(Name = "grant_type")]
        public string GrantType { get; set; } = string.Empty;

        [FromForm(Name = "client_id")]
        public string ClientId { get; set; } = string.Empty;

        [FromForm(Name = "client_secret")]
        public string ClientSecret { get; set; } = string.Empty;

        // Used for 'refresh_token' flow
        [FromForm(Name = "refresh_token")]
        public string? RefreshToken { get; set; }


        /// <summary>
        /// Optional: A space-separated string of requested scopes.
        /// </summary>
        [FromForm(Name = "scope")]
        public string? Scope { get; set; }
    }

}
