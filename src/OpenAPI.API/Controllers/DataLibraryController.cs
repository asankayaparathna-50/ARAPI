using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAPI.Application.Services;

namespace OpenAPI.API.Controllers
{
    [ApiController]
    [Route("api/v1/datalibrary")]
    public class DataLibraryController : ControllerBase
    {
        private const string InternalServerErrorMessage = "Internal server error.";
        private readonly DataLibraryServices _service;
        private readonly CommonServices _commonServices;
        private readonly ILogger<DataLibraryController> _logger;

        public DataLibraryController(DataLibraryServices service,  ILogger<DataLibraryController> logger, CommonServices commonServices)
        {
            _service = service;
            _logger = logger;
            _commonServices = commonServices;
        }

        /// <summary>
        /// Get all frequencies
        /// GET /api/v1/datalibrary/frequencies
        /// </summary>
        [HttpGet("frequencies")]
        [Produces("application/json")]
        [Authorize]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetFrequencyAsync(cancellationToken);
                 return Ok(items);
            }
            catch (Exception ex)
            {
                // Log the full exception details
                _logger.LogError(ex, "An unhandled exception occurred during getting frequencies.");
                // Return generic 500 error to prevent information disclosure
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get all sectors
        /// GET /api/v1/datalibrary/sectors
        /// </summary>
        [HttpGet("sectors")]
        [Produces("application/json")]
        //[AllowAnonymous]
        [Authorize]
        public async Task<IActionResult> GetSectors(CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetSectorsAsync(cancellationToken);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting sectors.");
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get all subjects
        /// GET /api/v1/datalibrary/subjects
        /// </summary>
        [HttpGet("subjects")]
        [Produces("application/json")]
        [Authorize]
        public async Task<IActionResult> GetSubjects(CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetSubjectsAsync(cancellationToken);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting subjects.");
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get subjects by sector
        /// GET /api/v1/datalibrary/subjects/sector/{sectorCode}
        /// </summary>
        [HttpGet("subjects/sector/{sectorCode}")]
        [Produces("application/json")]
        [Authorize(Policy = "client1")]
        public async Task<IActionResult> GetSubjectsBySector([FromRoute] string sectorCode, CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetSubjectsBySectorAsync(sectorCode, cancellationToken);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting subjects by sector {SectorCode}.", sectorCode);
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get data code list grouped as SubjectName - TopicName
        /// GET /api/v1/datalibrary/datacodelist?subId=123&sectorCode=ABC&frequencyCode=M
        /// </summary>
        [HttpGet("datacodelist")]
        [Produces("application/json")]
        [Authorize(Policy = "account")]
        public async Task<IActionResult> GetDataCodeList([FromQuery] int? subId, [FromQuery] string? sectorCode, [FromQuery] string? frequencyCode, CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetDataCodeListAsync(subId, sectorCode, frequencyCode, cancellationToken);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting data code list for subId: {SubId}, sectorCode: {SectorCode}, frequencyCode: {FrequencyCode}.", subId, sectorCode, frequencyCode);
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get data code list grouped as SubjectName - TopicName
        /// GET /api/v1/datalibrary/datavalues?subId=123&sectorCode=ABC&frequencyCode=M
        /// </summary>
        [HttpGet("datavalues")]
        [Produces("application/json")]
        [Authorize(Policy = "account")]
        public async Task<IActionResult> GetDataValues([FromQuery] List<int> dataCodeList, [FromQuery] bool notes, [FromQuery] string from, [FromQuery] string to, [FromQuery] string frequencyCode, CancellationToken cancellationToken)
        {
            try
            {
                var items = await _service.GetDataValuesAsync(dataCodeList, notes, from, to, frequencyCode, cancellationToken);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting data values for codes: {DataCodeList}, from: {FromDate}, to: {ToDate}.", string.Join(",", dataCodeList), from, to);
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        /// <summary>
        /// Get exchange rates between dates (inclusive). 
        /// If toDate is omitted, fromDate will be used as single date.
        /// </summary>
        [HttpGet("exchangerates")]
        [Produces("application/json")]
        [Authorize(Policy = "statistics")]
        public async Task<IActionResult> GetBuyingAndSellingExchangeRates([FromQuery] string currencyCode, [FromQuery] string typeLike, [FromQuery] string fromDate, [FromQuery] string? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var validationResult = ValidateExchangeRateInputs(currencyCode, fromDate, toDate, out _, out var normalizedCurrencyCode, out var errorResult);
                if (!validationResult)
                    return errorResult!;

                // App Setting Lookup - Ensure cancellation token is passed
                string appsettingKey = typeLike.ToLower() + "-" + normalizedCurrencyCode.ToLower() + "-DataCodeId";
                var clientAppSetting = await _commonServices.GetAppSettingValue(appsettingKey, cancellationToken);

                // --- Business Logic / Service Call ---
                if (typeLike.Equals("ex-bs", StringComparison.OrdinalIgnoreCase))
                {
                    var exrateBS = await _service.GetTransformedExchangeRatesAsync(normalizedCurrencyCode, typeLike, fromDate, toDate, clientAppSetting, cancellationToken);

                    if (exrateBS == null || !exrateBS.Any())
                        return NotFound("No transformed exchange rate records found for the given criteria.");

                    return Ok(exrateBS);
                }

                // Default: Call the service for raw data
                var results = await _service.GetExchangeRatesByDateRangeAsync(normalizedCurrencyCode, typeLike, fromDate, clientAppSetting, toDate, cancellationToken);

                if (results == null || !results.Any())
                    return NotFound("No exchange rate records found for the given criteria.");

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting exchange rates for currency: {CurrencyCode}, from: {FromDate}, to: {ToDate}.", currencyCode, fromDate, toDate);
                return StatusCode(500, InternalServerErrorMessage);
            }
        }

        private bool ValidateExchangeRateInputs(string currencyCode, string fromDate, string? toDate, out DateTime? toDt, out string normalizedCurrencyCode, out IActionResult? errorResult)
        {
            toDt = null;
            normalizedCurrencyCode = currencyCode;
            errorResult = null;

            var dateFormat = "yyyy-MM-dd";
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (!DateTime.TryParseExact(fromDate, dateFormat, culture, System.Globalization.DateTimeStyles.None, out var fromDt))
            {
                errorResult = BadRequest("Invalid 'fromDate' format. Expected format: yyyy-MM-dd");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(toDate))
            {
                if (!DateTime.TryParseExact(toDate, dateFormat, culture, System.Globalization.DateTimeStyles.None, out var parsedToDate))
                {
                    errorResult = BadRequest("Invalid 'toDate' format. Expected format: yyyy-MM-dd");
                    return false;
                }

                toDt = parsedToDate;

                if (fromDt > parsedToDate)
                {
                    errorResult = BadRequest("'fromDate' cannot be greater than 'toDate'.");
                    return false;
                }
            }

            // Normalize currency code
            if (!string.IsNullOrWhiteSpace(currencyCode))
            {
                normalizedCurrencyCode = currencyCode.Trim().ToLower();
                if (normalizedCurrencyCode.Length > 3)
                    normalizedCurrencyCode = normalizedCurrencyCode.Substring(0, 3);
            }

            return true;
        }

    }
}
