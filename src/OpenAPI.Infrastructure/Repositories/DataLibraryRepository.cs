using System.Data;
using Dapper;
using OpenAPI.Domain.Entities;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Infrastructure.Repositories
{
    public class DataLibraryRepository : IDataLibraryRepository
    {
        private readonly ISqlQueryRepository _sqlRepo;

        public DataLibraryRepository(ISqlQueryRepository sqlRepo)
        {
            _sqlRepo = sqlRepo ?? throw new ArgumentNullException(nameof(sqlRepo));
        }

        public async Task<IEnumerable<Frequency>> GetFrequencysAsync(CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetFrequencies";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var cmd = new CommandDefinition(
                spName,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryAsync<Frequency>(cmd);
            return result;
        }

        public async Task<IEnumerable<Sector>> GetSectorsAsync(CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetSectors";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var cmd = new CommandDefinition(
                spName,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryAsync<Sector>(cmd);
            return result;
        }

        public async Task<IEnumerable<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetSubjects";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var cmd = new CommandDefinition(
                spName,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryAsync<Subject>(cmd);
            return result;
        }

        public async Task<IEnumerable<Subject>> GetSubjectsBySectorAsync(string sectorCode, CancellationToken cancellationToken = default)
        {
            if (sectorCode == null) throw new ArgumentNullException(nameof(sectorCode));

            const string spName = "SP_GetSubjectsBySector";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var cmd = new CommandDefinition(
                spName,
                new { SectorCode = sectorCode },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryAsync<Subject>(cmd);
            return result;
        }

        public async Task<IEnumerable<DataCode>> GetDataCodeListAsync(int? dataCodeId, int? subId, string? sectorCode, string? frequencyCode, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetDataCodeList";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            var parameters = new { DataCodeID = dataCodeId, SubId = subId, SectorCode = sectorCode, FrequencyCode = frequencyCode };

            var cmd = new CommandDefinition(
                spName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken
            );

            var result = await conn.QueryAsync<DataCode>(cmd);
            return result;
        }

        // Implementation of IDataLibraryRepository.GetDataValueAsync
        public async Task<IEnumerable<DataValue>> GetDataValueAsync(string dataCodeId, string fromDate, string? toDate = null, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetDataValueByRange";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            // Convert string to string format required by the stored procedure (e.g., "yyyy-MM-dd")
            var parameters = new
            {
                DataCodeID = dataCodeId,
                FromPeriodID = fromDate,//.ToString("yyyy-MM-dd"),
                ToPeriodID = toDate,//?.ToString("yyyy-MM-dd")
            };

            var cmd = new CommandDefinition(spName, parameters: parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

            var result = await conn.QueryAsync<DataValue>(cmd);
            return result;
        }

        // Implementation of IDataLibraryRepository.GetDataValuesAsync
        public async Task<IEnumerable<DataValues>> GetDataValuesAsync(string dataCodeId, string fromDate, string? toDate = null, string? frequencyCode = null, CancellationToken cancellationToken = default)
        {
            const string spName = "SP_GetDataValuesByRange";
            await using var conn = await _sqlRepo.GetOpenConnectionAsync(cancellationToken);

            // Convert string to string format required by the stored procedure (e.g., "yyyy-MM-dd")
            var parameters = new
            {
                DataCodeID = dataCodeId,
                FromPeriodID = fromDate,//.ToString("yyyy-MM-dd"),
                ToPeriodID = toDate,//?.ToString("yyyy-MM-dd")
            };

            var cmd = new CommandDefinition(spName, parameters: parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

            var result = await conn.QueryAsync<DataValues>(cmd);
            return result;
        }

        // Implementation of IDataLibraryRepository.GetExchangeRatesByDateRangeAsync
        public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesByDateRangeAsync(string currencyCode, string typeLike, string fromDate, string? toDate = null, CancellationToken cancellationToken = default)
        {
            // 1. App Setting Lookup - Ensure cancellation token is passed
            string appsettingKey = typeLike.ToLower() + "-" + currencyCode.ToLower() + "-DataCodeId";
            var clientAppValue = await GetAppSettingValue(appsettingKey, cancellationToken);

            if (clientAppValue == null)
            {
                return Enumerable.Empty<ExchangeRate>();
            }

            string dataCodeId = clientAppValue.Value ?? string.Empty;
            string type = clientAppValue.Description ?? string.Empty;

            if (string.IsNullOrEmpty(dataCodeId)) { return Enumerable.Empty<ExchangeRate>(); }

            // 2. Data Fetching - Pass string objects to GetDataValueAsync (which now accepts them)
            var datavalue = await GetDataValueAsync(dataCodeId, fromDate, toDate, cancellationToken);

            // 3. Entity Mapping
            var result = new List<ExchangeRate>(datavalue.Count());

            foreach (var dv in datavalue)
            {
                var exchange = new ExchangeRate
                {
                    // The underlying data value retrieval still expects PeriodID strings to be parsable to string
                    Date = DateOnly.Parse(dv.PeriodID ?? string.Empty),
                    Type = type,
                    CurrencyCode = currencyCode.ToUpper(),
                    Value = dv.Value
                };

                result.Add(exchange);
            }

            return result;
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
