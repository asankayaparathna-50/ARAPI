using OpenAPI.Domain.Entities.Auth;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Application.Services
{
    public class ClientServices
    {
        // Inject the new repository
        private readonly IClientRepository _clientRepo;

        public ClientServices(IClientRepository clientRepo)
        {
            _clientRepo = clientRepo;
        }

        public async Task<ClientConfig?> FindByClientIdAsync(string clientId)
        {
            return await _clientRepo.FindByIdAsync(clientId);
        }

        public async Task<RefreshToken?> RTokenFindByClientIdAsync(string clientId)
        {
            return await _clientRepo.RTokenFindByIdAsync(clientId);
        }

        public async Task PersistRefreshTokenAsync(string clientId, string refreshToken, DateTime expiration, CancellationToken cancellationToken)
        {
            // NOTE ON SECURITY: For maximum protection, you should HASH the refresh token 
            // (like you would a password) before storing it, and store the plaintext 
            // token only in the response. We are using the token value directly for now.

            var tokenRecord = new RefreshToken
            {
                ClientId = clientId,
                TokenHash = refreshToken, // Used to store the token value
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = expiration,
                IsRevoked = false
            };

            // Delegate persistence to the dedicated repository
            await _clientRepo.PersistTokenAsync(tokenRecord, cancellationToken);
        }

    }
}