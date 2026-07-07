using OpenAPI.Domain.Entities;

namespace OpenAPI.Domain.Interfaces
{
    public interface IDataLibraryRepository
    {
        Task<IEnumerable<Frequency>> GetFrequencysAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Sector>> GetSectorsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Subject>> GetSubjectsBySectorAsync(string sectorCode, CancellationToken cancellationToken = default);
        Task<IEnumerable<DataCode>> GetDataCodeListAsync(int? dataCodeId, int? subId, string? sectorCode, string? frequencyCode, CancellationToken cancellationToken = default);
        Task<IEnumerable<ExchangeRate>> GetExchangeRatesByDateRangeAsync(string currencyCode, string typeLike, string fromDate, string? toDate = null,CancellationToken cancellationToken = default);  
        Task<IEnumerable<DataValue>> GetDataValueAsync(string dataCodeId, string fromDate, string? toDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<DataValues>> GetDataValuesAsync(string dataCodeId, string fromDate, string? toDate = null, string? frequencyCode = null, CancellationToken cancellationToken = default);
        
    }
}
