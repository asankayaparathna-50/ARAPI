using Microsoft.Data.SqlClient;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Infrastructure.Repositories.Data
{
    public class SqlQueryRepository : ISqlQueryRepository
    {
        private readonly string _conn;

        public SqlQueryRepository(IConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            // Connection string is read from environment variable: ConnectionStrings__DefaultConnection
            // or from appsettings.json under ConnectionStrings:DefaultConnection
            _conn = config.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found. " +
                    "Please set the environment variable 'ConnectionStrings__DefaultConnection' or " +
                    "configure 'ConnectionStrings:DefaultConnection' in appsettings.json.");

            if (string.IsNullOrWhiteSpace(_conn))
            {
                throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is empty. " +
                    "Please set the environment variable 'ConnectionStrings__DefaultConnection' with a valid SQL Server connection string.");
            }
        }


        public Task<string> GetConnectionStringAsync()
        {
            return Task.FromResult(_conn);
        }

        public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new SqlConnection(_conn);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }

    }
}
