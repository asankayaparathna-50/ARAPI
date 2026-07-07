using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenAPI.Application.Services;
using OpenAPI.Domain.Entities.Auth;

namespace OpenAPI.API.Controllers
{
    [ApiController]
    [Route("connect")]
    public class AuthController : ControllerBase
    {
        private readonly ClientServices _service;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ClientServices services, IOptions<JwtSettings> jwtOptions, ILogger<AuthController> logger)
        {
            _service = services;
            // Safely retrieve configuration via IOptions pattern
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromForm] TokenRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Grant Type Validation
                if (request.GrantType != "client_credentials")
                {
                    // Follows OAuth standard error format
                    return BadRequest(new { error = "unsupported_grant_type", error_description = "The requested grant type is not supported." });
                }

                // 2. Client Authentication
                var client = await _service.FindByClientIdAsync(request.ClientId);
                if (client is null) return Unauthorized(new { error = "invalid_client", error_description = "Client not found." });

                if (client.ClientSecret != request.ClientSecret) return Unauthorized(new { error = "invalid_client_credentials", error_description = "The client secret is invalid." });

                // 3. Configuration Validation (Crucial for secrets)
                if (string.IsNullOrEmpty(_jwtSettings.Key))
                {
                    // Log this serious configuration error internally
                    return StatusCode(500, new { error = "server_error", error_description = "JWT signing key is missing." });
                }

                // 4. Scope Validation
                var requestedScopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                var authorizedScopes = client.ScopesArray ?? Array.Empty<string>();

                // Determine the final set of scopes to grant (must be authorized AND requested)
                var grantedScopes = requestedScopes.Length > 0
                    ? requestedScopes.Intersect(authorizedScopes, StringComparer.OrdinalIgnoreCase).ToList()
                    : authorizedScopes.ToList(); // If no scope is requested, grant all authorized scopes (common default)

                // If the client requested scopes they aren't authorized for (and none were granted)
                if (requestedScopes.Length > 0 && grantedScopes.Count == 0)
                {
                    return BadRequest(new { error = "invalid_scope", error_description = "Requested scope is invalid or unauthorized for this client." });
                }

                // Define the Refresh Token Lifetime (e.g., 30 days)
                var refreshTokenLifetimeMinutes = _jwtSettings.RefreshTokenLifetimeMinutes;

                // Generate a unique, cryptographically secure string for the refresh token
                var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                var refreshTokenExpiry = DateTime.UtcNow.AddMinutes(refreshTokenLifetimeMinutes);

                await _service.PersistRefreshTokenAsync( client.ClientId, refreshToken, refreshTokenExpiry, cancellationToken );

                // 5. Build Claims
                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID claim for uniqueness/revocation
                    new Claim(JwtRegisteredClaimNames.Sub, client.ClientId), // Subject
                    new Claim("client_id", client.ClientId)
                };

                // Add granted scopes to claims
                foreach (var scope in grantedScopes)
                {
                    claims.Add(new Claim("scope", scope));
                }

                // 6. Token Generation
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var now = DateTime.UtcNow;
                var token = new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    notBefore: now,
                    expires: now.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes),
                    signingCredentials: creds
                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);
                var expiresInSeconds = (int)TimeSpan.FromMinutes(_jwtSettings.AccessTokenLifetimeMinutes).TotalSeconds;
                var refreshTokenExpiresInSeconds = (int)TimeSpan.FromMinutes(refreshTokenLifetimeMinutes).TotalSeconds;

                // 7. Success Response
                return Ok(new
                {
                    access_token = jwt,
                    token_type = "Bearer",
                    expires_in = expiresInSeconds,
                    scope = string.Join(' ', grantedScopes), // OAuth standard uses space-separated scopes
                    refresh_token = refreshToken,
                    refresh_token_expires_in = refreshTokenExpiresInSeconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting token.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("refresh/token")]
        public async Task<IActionResult> RefreshToken([FromForm] TokenRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Grant Type Validation
                if (request.GrantType != "refresh_token")
                {
                    // Follows OAuth standard error format
                    return BadRequest(new { error = "unsupported_grant_type", error_description = "The requested grant type is not supported." });
                }

                // 2. Client Authentication
                var client = await _service.RTokenFindByClientIdAsync(request.ClientId);
                if (client is null) return Unauthorized(new { error = "invalid_client", error_description = "Client not found." });

                if (client.TokenHash != request.RefreshToken) return Unauthorized(new { error = "invalid_refresh_token", error_description = "The refresh token is invalid." });

                var apiClient = await _service.FindByClientIdAsync(request.ClientId);
                if (apiClient is null) return Unauthorized(new { error = "invalid_client", error_description = "Client not found." });



                // 3. Configuration Validation (Crucial for secrets)
                if (string.IsNullOrEmpty(_jwtSettings.Key))
                {
                    // Log this serious configuration error internally
                    return StatusCode(500, new { error = "server_error", error_description = "JWT signing key is missing." });
                }

                // 4. Scope Validation
                var requestedScopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                var authorizedScopes = apiClient.ScopesArray ?? Array.Empty<string>();

                // Determine the final set of scopes to grant (must be authorized AND requested)
                var grantedScopes = requestedScopes.Length > 0
                    ? requestedScopes.Intersect(authorizedScopes, StringComparer.OrdinalIgnoreCase).ToList()
                    : authorizedScopes.ToList(); // If no scope is requested, grant all authorized scopes (common default)

                // If the client requested scopes they aren't authorized for (and none were granted)
                if (requestedScopes.Length > 0 && grantedScopes.Count == 0)
                {
                    return BadRequest(new { error = "invalid_scope", error_description = "Requested scope is invalid or unauthorized for this client." });
                }

                // 5. Build Claims
                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID claim for uniqueness/revocation
                    new Claim(JwtRegisteredClaimNames.Sub, client.ClientId), // Subject
                    new Claim("client_id", client.ClientId)
                };

                // Add granted scopes to claims
                foreach (var scope in grantedScopes)
                {
                    claims.Add(new Claim("scope", scope));
                }

                // 6. Token Generation
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var now = DateTime.UtcNow;
                var token = new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    notBefore: now,
                    expires: now.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes),
                    signingCredentials: creds
                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);
                var expiresInSeconds = (int)TimeSpan.FromMinutes(_jwtSettings.AccessTokenLifetimeMinutes).TotalSeconds;
                // 7. Success Response
                return Ok(new
                {
                    access_token = jwt,
                    token_type = "Bearer",
                    expires_in = expiresInSeconds,
                    scope = string.Join(' ', grantedScopes), // OAuth standard uses space-separated scopes
                    refresh_token = request.RefreshToken,
                    refresh_token_expires_in = client.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting token.");
                return StatusCode(500, "Internal server error.");
            }
        }


    }
}
