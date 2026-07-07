using OpenAPI.Domain.Entities;

namespace OpenAPI.Domain.Interfaces
{
    public interface ICommnRepository
    {
        Task<ClientAppSetting?> GetAppSettingValue(string key, CancellationToken cancellationToken = default);
    }
}
