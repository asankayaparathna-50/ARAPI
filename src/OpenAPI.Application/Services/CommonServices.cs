using OpenAPI.Domain.Entities;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Application.Services
{
    public class CommonServices
    {

        private readonly ICommnRepository _repo;

        public CommonServices(ICommnRepository repo)
        {
            _repo = repo;
        }

        public async Task<ClientAppSetting> GetAppSettingValue(string key, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetAppSettingValue(key,cancellationToken);

            if (result is null)
                throw new Exception("No appsetting values found.");

            return result;
        }

    }
}
