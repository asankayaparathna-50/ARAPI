namespace OpenAPI.Domain.Interfaces
{
    using System.Data.Common;

    public interface ISqlQueryRepository
    {
        Task<string> GetConnectionStringAsync();
        Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
    }
}
