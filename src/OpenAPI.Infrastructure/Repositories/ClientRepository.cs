using Dapper;
using System.Data;
using OpenAPI.Domain.Entities.Auth;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Infrastructure.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly ISqlQueryRepository _sqlRepo;

        public ClientRepository(ISqlQueryRepository sqlRepo)
        {
            // Defensive check: Ensure the dependency is not null.
            _sqlRepo = sqlRepo ?? throw new ArgumentNullException(nameof(sqlRepo));
        }

        public async Task<ClientConfig?> FindByIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            // 1. Define the stored procedure name and safely manage the connection.
            const string spName = "SP_FindClientById";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            // 2. Parameterized Query: Prevents SQL Injection.
            var parameters = new { ClientId = clientId };

            // 3. Command Definition: Passes parameters, command type, and the CancellationToken.
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            // 4. Execute the query and return the first matching client (or null).
            var client = await conn.QueryFirstOrDefaultAsync<ClientConfig>(cmd);
            return client;
        }

        public async Task<RefreshToken?> RTokenFindByIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            // 1. Define the stored procedure name and safely manage the connection.
            const string spName = "SP_RToenFindClientById";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            // 2. Parameterized Query: Prevents SQL Injection.
            var parameters = new { ClientId = clientId };

            // 3. Command Definition: Passes parameters, command type, and the CancellationToken.
            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            // 4. Execute the query and return the first matching client (or null).
            var client = await conn.QueryFirstOrDefaultAsync<RefreshToken>(cmd);
            return client;
        }

        public async Task PersistTokenAsync(RefreshToken token, CancellationToken cancellationToken)
        {
            const string spName = "SP_UpsertRefreshToken";

            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            // Note: The 'TokenHash' holds the token value for persistence.
            var parameters = new
            {
                ClientId = token.ClientId,
                TokenValue = token.TokenHash,
                ExpiresAt = token.ExpiresAt
            };

            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            // ExecuteAsync is used for non-query operations (Insert/Update)
            await conn.ExecuteAsync(cmd);
        }
    }

}
