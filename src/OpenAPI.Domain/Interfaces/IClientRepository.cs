using OpenAPI.Domain.Entities.Auth;

namespace OpenAPI.Domain.Interfaces
{
    public interface IClientRepository
    {
        Task<ClientConfig?> FindByIdAsync(string clientId, CancellationToken cancellationToken = default);
        Task PersistTokenAsync(RefreshToken token, CancellationToken cancellationToken);
        Task<RefreshToken?> RTokenFindByIdAsync(string clientId, CancellationToken cancellationToken = default);
    }
}
