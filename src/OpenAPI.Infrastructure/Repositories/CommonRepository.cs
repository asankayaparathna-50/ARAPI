using System.Data;
using Dapper;
using OpenAPI.Domain.Entities;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Infrastructure.Repositories
{
    public class CommonRepository : ICommnRepository
    {
        private readonly ISqlQueryRepository _sqlRepo;

        public CommonRepository(ISqlQueryRepository sqlRepo)
        {
            _sqlRepo = sqlRepo ?? throw new ArgumentNullException(nameof(sqlRepo));
        }


        public async Task<ClientAppSetting?> GetAppSettingValue(string key, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetAppSettingValue";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { Key = key };

            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryFirstOrDefaultAsync<ClientAppSetting>(cmd);
            return result;
        }
    }
}
