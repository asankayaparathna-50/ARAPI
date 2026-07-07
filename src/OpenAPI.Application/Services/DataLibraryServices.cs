using OpenAPI.Domain.Entities;
using OpenAPI.Domain.Interfaces;

namespace OpenAPI.Application.Services
{
    public class DataLibraryServices
    {

        private readonly IDataLibraryRepository _repo;

        public DataLibraryServices(IDataLibraryRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<Frequency>> GetFrequencyAsync(CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetFrequencysAsync(cancellationToken);

            if (!result.Any())
                throw new Exception("No frequency found.");

            return result;
        }

        public async Task<IEnumerable<Sector>> GetSectorsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetSectorsAsync(cancellationToken);

            if (!result.Any())
                throw new Exception("No sectors found.");

            return result;
        }

        public async Task<IEnumerable<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetSubjectsAsync(cancellationToken);

            if (!result.Any())
                throw new Exception("No subjects found.");

            return result;
        }

        public async Task<IEnumerable<Subject>> GetSubjectsBySectorAsync(string sectorCode, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetSubjectsBySectorAsync(sectorCode, cancellationToken);

            if (!result.Any())
                throw new Exception("No subjects found for sector.");

            return result;
        }

        public async Task<IEnumerable<DataCode>> GetDataCodeListAsync(int? subId, string? sectorCode, string? frequencyCode, CancellationToken cancellationToken = default)
        {
            var result = await _repo.GetDataCodeListAsync(null, subId, sectorCode, frequencyCode, cancellationToken);
            return result;
        }

        public async Task<DataValueReturn> GetDataValuesAsync(List<int> dataCodeList, bool notes, string from, string to, string frequencyCode, CancellationToken cancellationToken = default)
        {
            var result = new DataValueReturn
            {
                DataValueMain = new List<DataValueMain>(),
                DataValueNotes = new List<DataValueNotes>()
            };

            if (dataCodeList == null || dataCodeList.Count == 0)
                return result;

            foreach (var dataCodeId in dataCodeList)
            {
                // get metadata for this data code
                var dataCode = (await _repo.GetDataCodeListAsync(dataCodeId, null, null, frequencyCode, cancellationToken))
                                    .FirstOrDefault();

                // get data values 
                var datavalue = (await _repo.GetDataValuesAsync(dataCodeId.ToString(), from, to, frequencyCode, cancellationToken))?.ToList()
                                ?? new List<DataValues>(); // ensure non-null list

                // build list of DataValues DTOs
                var dvList = new List<DataValues>();
                foreach (var value in datavalue)
                {
                    // defensive mapping: make sure fields exist on 'value'
                    var tempValue = new DataValues
                    {
                        SubjectName = value.SubjectName ?? string.Empty,
                        TopicName = value.TopicName ?? string.Empty,
                        ItemName = value.ItemName ?? string.Empty,
                        Unit = value.Unit ?? string.Empty,
                        Scale = value.Scale ?? string.Empty,
                        PeriodID = value.PeriodID ?? string.Empty,
                        Value = value.Value ?? string.Empty
                    };
                    dvList.Add(tempValue);
                }

                // create DataValueMain and safely set ItemName using first row if present
                var dvMain = new DataValueMain
                {
                    ItemName = dvList.Count > 0
                                ? (dvList[0].SubjectName + " - " + dvList[0].TopicName).Trim()
                                : (dataCode?.ItemName ?? string.Empty),

                    DataValues = dvList
                };

                // Add the main section for this data code to the result list
                result.DataValueMain.Add(dvMain);

                // Add notes if requested and metadata exists
                if (notes && dataCode != null)
                {
                    var dvNote = new DataValueNotes
                    {
                        ItemName = dataCode.ItemName ?? string.Empty,
                        Sector = dataCode.SectorCode ?? string.Empty,
                        Source = dataCode.Source ?? string.Empty,
                        GeoArea = dataCode.GeoAreaCode ?? string.Empty,
                        Note = dataCode.Notes ?? string.Empty,
                        DataLastUpdated = (dataCode.ModifiedOn ?? dataCode.CreatedOn)?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                        UpdateFrequency = dataCode.FrequencyCode ?? string.Empty
                    };

                    result.DataValueNotes.Add(dvNote);
                }
            }

            return result;
        }

        public async Task<IEnumerable<ExchangeRateBuyingSelling>> GetTransformedExchangeRatesAsync(string currencyCode, string typeLike, string fromDate, string? toDate, ClientAppSetting clientAppSetting, CancellationToken cancellationToken)
        {

            // 1. Get the raw data from the repository (passing cancellation token)
            var results = await GetExchangeRatesByDateRangeAsync(currencyCode, typeLike, fromDate, clientAppSetting, toDate, cancellationToken);

            // 2. Perform the complex transformation here (SRP)
            var exrateBS = new List<ExchangeRateBuyingSelling>();

            foreach (var item in results)
            {
                decimal? buy = null, sell = null;

                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    var parts = item.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(p => p.Trim())
                                          .ToArray();

                    if (parts.Length > 0 && decimal.TryParse(parts[0], out var buyRate))
                        buy = buyRate;

                    if (parts.Length > 1 && decimal.TryParse(parts[1], out var sellRate))
                        sell = sellRate;
                }

                exrateBS.Add(new ExchangeRateBuyingSelling
                {
                    // Convert internal DateOnly back to string format for the DTO
                    Date = item.Date, //.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Type = item.Type,
                    CurrencyCode = item.CurrencyCode,
                    BuyRate_LKR = buy,
                    SellRate_LKR = sell
                });
            }

            return exrateBS;
        }

        // Existing method signatures
        public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesByDateRangeAsync(string currencyCode, string typeLike, string fromDate, ClientAppSetting clientAppSetting, string? toDate = null, CancellationToken cancellationToken = default)
        {
            if (clientAppSetting == null)
            {
                return Enumerable.Empty<ExchangeRate>();
            }

            string dataCodeId = clientAppSetting != null ? clientAppSetting.Value : string.Empty;
            string type = clientAppSetting != null ? clientAppSetting.Description : string.Empty;

            if (dataCodeId == null) { return Enumerable.Empty<ExchangeRate>(); }

            var datavalue = await _repo.GetDataValueAsync(dataCodeId, fromDate, toDate, cancellationToken);

            var result = new List<ExchangeRate>(datavalue.Count());

            foreach (var dv in datavalue)
            {
                var exchange = new ExchangeRate
                {
                    Date = DateOnly.Parse(dv.PeriodID ?? ""),
                    Type = type,
                    CurrencyCode = currencyCode.ToUpper(),
                    Value = dv.Value
                };

                result.Add(exchange);
            }

            return result;
        }
    
    }
}
