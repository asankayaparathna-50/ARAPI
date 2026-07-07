using Microsoft.AspNetCore.Mvc;
using OpenAPI.API.Constants;
using OpenAPI.API.Filters;
using OpenAPI.Application.Services;
using OpenAPI.Domain.Entities.Statistics;
using System.Globalization;

namespace OpenAPI.API.Controllers.Statistics.EconomicIndicators
{
    [ApiController]
    [Route("api/v1/statistics/weekly/economic-indicator")]
    [StatisticsQueryValidation]
    public class WeeklyEconomicIndicatorController : ControllerBase
    {
        private readonly StatisticsServices _service;
        private readonly SdmxTransformationService _sdmxService;
        private readonly EuristatSdmxTransformationService _euristatSdmxService;
        private readonly ILogger<WeeklyEconomicIndicatorController> _logger;

        public WeeklyEconomicIndicatorController(
            StatisticsServices service, 
            SdmxTransformationService sdmxService,
            EuristatSdmxTransformationService euristatSdmxService,
            ILogger<WeeklyEconomicIndicatorController> logger)
        {
            _service = service;
            _sdmxService = sdmxService;
            _euristatSdmxService = euristatSdmxService;
            _logger = logger;
        }

        //1.1
        [HttpGet("price-indices")]
        [Produces("application/json")]
        //[Authorize]
        public async Task<IActionResult> GetPriceIndices(string type, string? period = null,  string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var items = await _service.GetPriceIndices(type, period, cancellationToken);

                if (items == null || (items.priceIndices?.Count == 0))
                {
                    return NotFound($"No price indices data found for period '{period ?? "latest"}'");
                }

                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxPriceIndices(items, period);
                }

                var response = ShapePriceIndicesResponse(items, period, type);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetPriceIndices: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting price indices for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving price indices.");
            }
        }

        private ObjectResult FormatSdmxPriceIndices(PriceIndices items, string? period)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertPriceIndicesToSdmx(items, period);
            var acceptHeader = Request.Headers.Accept.ToString();
            
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            
            return new ObjectResult(sdmxData)
            {
                ContentTypes = { ContentType.ContentTypeApplicationJson }
            };
        }

        private object ShapePriceIndicesResponse(PriceIndices items, string? period, string type)
        {
            string? periodValue = GetPriceIndicesPeriodValue(period, items.priceIndices);
            var data = BuildPriceIndicesData(items.priceIndices);

            return new
            {
                Period = periodValue,
                Type = type,
                Data = data
            };
        }

        private static string? GetPriceIndicesPeriodValue(string? period, IEnumerable<IDictionary<string, object>>? priceIndices)
        {
            if (!string.IsNullOrWhiteSpace(period))
                return period;

            var firstEntry = priceIndices?.FirstOrDefault();
            if (firstEntry != null && firstEntry.TryGetValue("periodId", out var periodObj))
            {
                return periodObj?.ToString();
            }
            
            return null;
        }

        private static Dictionary<string, decimal?> BuildPriceIndicesData(IEnumerable<IDictionary<string, object>>? priceIndices)
        {
            var data = new Dictionary<string, decimal?>();
            
            if (priceIndices == null)
                return data;

            foreach (var entry in priceIndices)
            {
                if (!entry.TryGetValue("key", out var keyObj) || keyObj == null) 
                    continue;
                    
                var key = keyObj.ToString() ?? string.Empty;

                if (!entry.TryGetValue("value", out var valueObj) || valueObj == null) 
                    continue;

                if (decimal.TryParse(valueObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                {
                    data[key] = dec;
                }
            }

            return data;
        }

        //1.2
        [HttpGet("prices")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetPrices(string market, string? period = null, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(market))
                    return BadRequest("market is required");

                if (!string.IsNullOrWhiteSpace(period) && !DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetPrices(period ?? string.Empty, market, cancellationToken);

                if (items == null || (items.prices?.Count == 0))
                {
                    return NotFound($"No price data found for market '{market}' and period '{period ?? "latest"}'");
                } 

                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxMarketPrice(items, period);
                }

                string? periodValue = GetPeriodValue(period, items.prices);
                var data = GroupMarketPrices(items.prices);

                var response = new
                {
                    Period = periodValue,
                    Market = market,
                    Data = data
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetPrices: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting market prices for period: {Period}, market: {Market}", period, market);
                return StatusCode(500, "Internal server error occurred while retrieving market prices.");
            }
        }

        private IActionResult FormatSdmxMarketPrice(dynamic items, string? period)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertMarketPriceToSdmx(items, period);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private IActionResult FormatSdmxGdpGrowth(List<GdpItem> items, string? period, string? frequency)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertGdpGrowthToSdmx(items, period, frequency);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private IActionResult FormatSdmxAgriculturalProduction(List<ModalItem> items, string? period, string? frequency)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertAgriculturalProductionToSdmx(items, period, frequency);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private IActionResult FormatSdmxIndustrialProduction(List<ModalItem> items, string? period, string? frequency)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertIndustrialProductionToSdmx(items, period, frequency);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private IActionResult FormatSdmxGeneric(List<ModalItem> items, string indicatorName, string? frequency, string? period)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var dsdId = $"DSD_{indicatorName.Replace(" ", "_")}";
            var sdmxData = _euristatSdmxService.ConvertGenericModalItemsToSdmx(items, period, frequency, indicatorName, dsdId);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private IActionResult FormatSdmxGenericObject(object data, string indicatorName, string? frequency)
        {
            // Use new Eurostat SDMX API package for full SDMX 2.1 compliance
            var sdmxData = _euristatSdmxService.ConvertGenericObjectToSdmx(data, indicatorName, frequency);
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains(ContentType.ContentTypeApplicationXml) || acceptHeader.Contains(ContentType.ContentTypeTextXml))
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationXml }
                };
            }
            else
            {
                return new ObjectResult(sdmxData)
                {
                    ContentTypes = { ContentType.ContentTypeApplicationJson }
                };
            }
        }

        private static string? GetPeriodValue(string? period, IEnumerable<IDictionary<string, object>>? prices)
        {
            if (!string.IsNullOrWhiteSpace(period))
                return period;
            var firstEntry = prices?.FirstOrDefault();
            if (firstEntry != null)
            {
                // Try to get periodId from the entry
                if (firstEntry.TryGetValue("periodId", out var periodObj) && periodObj != null)
                {
                    return periodObj.ToString();
                }
                // If periodId not found, try "Period" as fallback
                if (firstEntry.TryGetValue("Period", out var periodObj2) && periodObj2 != null)
                {
                    return periodObj2.ToString();
                }
            }
            return null;
        }

        private static Dictionary<string, object> GroupMarketPrices(IEnumerable<IDictionary<string, object>>? prices)
        {
            var wholesaleData = new Dictionary<string, decimal?>();
            var retailData = new Dictionary<string, decimal?>();

            if (prices != null)
            {
                foreach (var entry in prices.Where(e => e != null))
                {
                    var (keyString, itemName, value) = ExtractMarketPriceKeyValue(entry);
                    if (keyString == null || value == null) continue;

                    AddToMarketCategory(keyString, itemName, value.Value, wholesaleData, retailData);
                }
            }

            return BuildMarketPriceData(wholesaleData, retailData);
        }

        private static (string? keyString, string itemName, decimal? value) ExtractMarketPriceKeyValue(IDictionary<string, object> entry)
        {
            if (!entry.TryGetValue("key", out var keyObj) || keyObj == null)
                return (null, string.Empty, null);

            var keyString = keyObj.ToString() ?? string.Empty;
            var parts = keyString.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var itemName = (parts.Length >= 2) ? parts[^1].Trim() : keyString;

            if (!entry.TryGetValue("value", out var valueObj) || valueObj == null)
                return (null, string.Empty, null);

            if (decimal.TryParse(valueObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                return (keyString, itemName, dec);

            return (null, string.Empty, null);
        }

        private static void AddToMarketCategory(string keyString, string itemName, decimal value, 
            Dictionary<string, decimal?> wholesaleData, Dictionary<string, decimal?> retailData)
        {
            if (keyString.StartsWith("Wholesale-", StringComparison.OrdinalIgnoreCase))
            {
                wholesaleData[itemName] = value;
            }
            else if (keyString.StartsWith("Retail-", StringComparison.OrdinalIgnoreCase))
            {
                retailData[itemName] = value;
            }
        }

        private static Dictionary<string, object> BuildMarketPriceData(Dictionary<string, decimal?> wholesaleData, Dictionary<string, decimal?> retailData)
        {
            var data = new Dictionary<string, object>();
            if (wholesaleData.Count > 0)
            {
                data["Wholesale"] = wholesaleData;
            }
            if (retailData.Count > 0)
            {
                data["Retail"] = retailData;
            }
            return data;
        }

        
        //1.3
        [HttpGet("gdp-growth")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetGdpGrowth(string frequency, string? period = null, string? year = null, string? quarter = null, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate period only if provided
                if (string.IsNullOrWhiteSpace(frequency))
                {
                    return BadRequest("frequency is required");
                }

                else if (!string.IsNullOrWhiteSpace(period)) 
                { 
                    if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    {
                        return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                    }

                }
                else if (string.IsNullOrWhiteSpace(period) && string.IsNullOrWhiteSpace(year))
                {
                    return BadRequest("Either period or year must be provided");
                }
                else if (!string.IsNullOrWhiteSpace(period) && !string.IsNullOrWhiteSpace(frequency))
                {
                    year = null; // Ignore year if period is provided
                    quarter = null; // Ignore quarter if period is provided
                }

                var items = await _service.GetGdpGrowth(frequency, period, year, quarter, cancellationToken);

                // Check if no data found
                if (items == null || items.Count == 0)
                {
                    return NotFound($"No GDP growth data found for period '{period ?? "latest"}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGdpGrowth(items, period, frequency);
                }

                // Return shaped response for JSON format
                var response = ShapeGdpGrowthResponse(items, period ?? year, frequency);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetGdpGrowth: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting GDP growth data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving GDP growth data.");
            }
        }

        private object ShapeGdpGrowthResponse(List<GdpItem> items, string? period, string? frequency)
        {
            if (items == null || items.Count == 0)
                return new { period = period ?? string.Empty, frequency = string.Empty, data = new Dictionary<string, decimal?>() };

            var firstItem = items[0];
            var resolvedPeriod = period ?? firstItem.PeriodId ?? string.Empty;

            var data = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Item))
                .ToDictionary(item => item.Item!, item => item.CurrentValue);

            return new
            {
                period = resolvedPeriod,
                frequency = frequency,
                data = data
            };
        }

        //1.4
        [HttpGet("agricultural-production")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetAgriculturalProduction(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                // Validate period format (expect yyyy-MM-dd format for daily data)
                if (!DateTime.TryParseExact(period, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest("period must be in yyyy-MM-dd format (e.g., 2024-09-01)");
                }

                var items = await _service.GetAgriculturalProduction(period, cancellationToken);

                // Check if no data found
                if (items == null || items.Count == 0)
                {
                    return NotFound($"No agricultural production data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxAgriculturalProduction(items, period, "M");
                }

                // Return shaped response for JSON format
                var response = new
                {
                    period = period,
                    frequency = "M",
                    data = BuildData(items)
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetAgriculturalProduction: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting agricultural production data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving agricultural production data.");
            }
        }

        //1.5
        [HttpGet("industrial-production")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetIndustrialProduction(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                var items = await _service.GetIndustrialProduction(period, cancellationToken);

                // Check if no data found
                if (items == null || items.Count == 0)
                {
                    return NotFound($"No industrial production data found for period '{period}'");
                }


                var productionData = new List<ModalItem>();
                var changeData = new List<ModalItem>();



                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Item))
                        continue;
                    
                    var itemName = item.Item!.Trim();
                    var isChangeItem = itemName.Contains("- Change", StringComparison.OrdinalIgnoreCase);
               

                    if (isChangeItem)
                    {
                        changeData.Add(item);
                    }
                    else
                    {
                        productionData.Add(item);
                    }
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxIndustrialProduction(items, period, "M");
                }

                var response = new
                {
                    period = period,
                    frequency = "M",
                    data = new Dictionary<string, object>
                    {
                        { "Production", BuildData(productionData) },
                        { "% Change", BuildData(changeData) }
                    }
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetIndustrialProduction: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting industrial production data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving industrial production data.");
            }
        }

        //1.6
        [HttpGet("pmi")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetPMI(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                var items = await _service.GetPMI(period, cancellationToken);

                // Check if no data found
                if (items == null || (items.Count == 0))
                {
                    return NotFound($"No PMI data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGeneric(items, "PMI", "M", period);
                }

                var response = new
                {
                    period = period,
                    frequency = "M",
                    data = BuildData(items)
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetPMI: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting Pmidata for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving Pmidata.");
            }
        }

        //1.7
        [HttpGet("employment")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetEmployment(string period, string frequency, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                // Validate period format (expect yyyy-MM-dd format)
                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest("period must be in yyyy-MM-dd format (e.g., 2023-06-30)");
                }
                if (string.IsNullOrWhiteSpace(frequency))
                {
                    return BadRequest("frequency is required");
                }

                var items = await _service.GetEmployment(period, frequency, cancellationToken);

                // Check if no data found
                if (items == null || !items.Any())
                {
                    return NotFound($"No employment data found for period '{period}' and frequency '{frequency}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGeneric(items, "EMPLOYMENT", frequency, period);
                }

                var response = new
                {
                    period = period,
                    frequency = frequency,
                    data = BuildData(items)
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetEmployment: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting employment data for period: {Period} and frequency: {Frequency}", period, frequency);
                return StatusCode(500, "Internal server error occurred while retrieving employment data.");
            }
        }

        //1.8 Wage Rate Indice
        [HttpGet("wage-rate-indices")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetWageRateIndices(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                // Validate period format (expect yyyy-MM-dd format for daily data)
                if (!DateTime.TryParseExact(period, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest("period must be in yyyy-MM-dd format (e.g., 2024-09-01)");
                }
                string frequency = "M"; 

                var response = await _service.GetWageRateIndices(period, frequency, cancellationToken);

                // Check if no data found
                if (response == null)
                {
                    return NotFound($"No wage rate indices data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(response, "WAGE_RATE_INDICES", "M");
                }

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetWageRateIndices: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting wage rate indices data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving wage rate indices data.");
            }
        }


        //1.9
        [HttpGet("crude-oil-prices")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetCrudeOilPrices(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                // Validate period format (expect yyyy-MM-dd format)
                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest("period must be in yyyy-MM-dd format (e.g., 2025-11-18)");
                }
                
                var items = await _service.GetCrudeOilPrices(period, cancellationToken);

                // Check if no data found
                if (items == null) { return NotFound($"No crude oil price data found for period '{period}'");}

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "CRUDE_OIL_PRICES", "D");
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetCrudeOilPrices: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting crude oil price data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving crude oil price data.");
            }
        }

        private static Dictionary<string, decimal?> BuildData(IEnumerable<ModalItem> items)
        {
            var data = new Dictionary<string, decimal?>();

            if (items == null)
                return data;

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ItemName))
                    continue;

                data[item.ItemName] = item.CurrentValue;
            }

            return data;
        }

        //1.10
        [HttpGet("daily-electricity-generation")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetDailyElectricityGeneration(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                // Validate period format (expect yyyy-MM-dd format)
                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest("period must be in yyyy-MM-dd format (e.g., 2025-11-18)");
                }

                var items = await _service.GetDailyElectricityGeneration(period, cancellationToken);

                // Check if no data found
                if (items == null) {
                    return NotFound($"No daily electricity generation data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "DAILY_ELECTRICITY_GENERATION", "D");
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetDailyElectricityGeneration: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting daily electricity generation data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving daily electricity generation data.");
            }
        }

        //2.1
        [HttpGet("interest-rate")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetPolicyInterestRate(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetInterestRates(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No interest rate data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "INTEREST_RATE", "D");
                }

                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetPolicyInterestRate: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting policy interest rate for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving policy interest rate.");
            }
        }

        //2.2
        [HttpGet("money-supply")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetMoneySupply(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetMoneySupply(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No money supply data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "MONEY_SUPPLY", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetMoneySupply: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting money supply for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving money supply.");
            }
        }

        //2.3
        [HttpGet("reserve-money")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetReserveMoney(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetReserveMoney(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No reserve money data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "RESERVE_MONEY", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetReserveMoney: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting reserve money for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving reserve money.");
            }
        }

         //2.4
        [HttpGet("money-market-activity")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetMoneyMarketActivity(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetMoneyMarketActivity(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No money market activity data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "MONEY_MARKET_ACTIVITY", "D");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetMoneyMarketActivity: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting money market activity for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving money market activity.");
            }
        }

        //2.5
        [HttpGet("cbsl-securities-portfolio")] 
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetCbslSecuritiesPortfolio(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetCbslSecuritiesPortfolio(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No CBSL securities portfolio data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "CBSL_SECURITIES_PORTFOLIO", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetCbslSecuritiesPortfolio: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting CBSL securities portfolio for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving CBSL securities portfolio.");
            }
        }

        //2.6

        //2.7.1
        [HttpGet("credit-cards")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetCreditCards(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetCreditCards(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No credit card data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "CREDIT_CARDS", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetCreditCards: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting credit card data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving credit card data.");
            }
        }

        //2.7.2
        [HttpGet("commercial-paper-issue")] 
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetCommercialPaperIssue(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetCommercialPaperIssue(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No commercial paper issue data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "COMMERCIAL_PAPER_ISSUE", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetCommercialPaperIssue: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting commercial paper issue data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving commercial paper issue data.");
            }
        }

        //2.8
        [HttpGet("share-market")] //Share Market
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetShareMarket(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetShareMarket(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No share market data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "SHARE_MARKET", "D");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetShareMarket: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting share market data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving share market data.");
            }
        }

        //3.1
        [HttpGet("government-finance")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetGovernmentFinance(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetGovernmentFinance(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No government finance data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "GOVERNMENT_FINANCE", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetGovernmentFinance: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting government finance data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving government finance data.");
            }
        }

        //3.2
        [HttpGet("outstanding-central-government-debt")] // Outstanding Central Government Debt
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetOutstandingCentralGovernmentDebt(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetOutstandingCentralGovernmentDebt(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No outstanding central government debt data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "OUTSTANDING_CENTRAL_GOVERNMENT_DEBT", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetOutstandingCentralGovernmentDebt: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting outstanding central government debt data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving outstanding central government debt data.");
            }
        }

        //3.3.1  - Primary and Secondary Market Yield Rates of Government Securities
        [HttpGet("primary-secondary-market-yield-rates-government-securities")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetGovernmentSecurities(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetGovernmentSecurities(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No government securities data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "GOVERNMENT_SECURITIES", "D");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetGovernmentSecurities: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting government securities data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving government securities data.");
            }
        }

        //3.3.2 International Sovereign Bonds
        [HttpGet("international-sovereign-bonds")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetInternationalSovereignBonds(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetInternationalSovereignBonds(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No international sovereign bonds data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "INTERNATIONAL_SOVEREIGN_BONDS", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetInternationalSovereignBonds: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting international sovereign bonds data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving international sovereign bonds data.");
            }
        }

        //3.4   - Weekly Summary of Primary and Secondary Market Transactions of Government Securities
        [HttpGet("primary-secondary-market-transactions-government-securities")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetPrimarySecondaryMarketTransactions(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetPrimarySecondaryMarketTransactions(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No government securities data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "PRIMARY_SECONDARY_MARKET_TRANSACTIONS", "W");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetGovernmentSecurities: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting government securities data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving government securities data.");
            }
        }

        //3.5 - Two Way Quotes (Treasury Bills)

        //3.6 - Two Way Quotes (Treasury Bonds)

        //3.7 Treasury Bonds issued pursuant to the Domestic Debt Optimisation & External Debt Restructuring Programme

        //4.1 - Exchange Rates
        [HttpGet("exchange-rates")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetExchangeRates(string period, string? type = null, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetExchangeRates(period, type, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No exchange rates data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "EXCHANGE_RATES", "D");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetExchangeRates: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting exchange rates data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving exchange rates data.");
            }
        }

        //4.2 - Tourism & Workers' Remittance
        [HttpGet("tourism-workers-remittance")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetTourismAndWorkersRemittance(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetTourismAndWorkersRemittance(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No tourism and workers' remittance data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "TOURISM_WORKERS_REMITTANCE", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetTourismAndWorkersRemittance: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting tourism and workers' remittance data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving tourism and workers' remittance data.");
            }
        }
        
        //4.3 Official Reserve Assets

        //4.4 International Reserves & Foreign Currency Liquidity

        //4.5 External Trade
        [HttpGet("external-trade")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetExternalTrade(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetExternalTrade(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No external trade data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "EXTERNAL_TRADE", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetExternalTrade: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting external trade data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving external trade data.");
            }
        }
        
        //4.6 Trade Indices (2010 = 100)
        [HttpGet("trade-indices")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetTradeIndices(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetTradeIndices(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No trade indices data found for period '{period}'");
                }

                // Check if SDMX format is requested
                if (format?.ToLower() == "sdmx")
                {
                    return FormatSdmxGenericObject(items, "TRADE_INDICES", "M");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetTradeIndices: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting trade indices data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving trade indices data.");
            }
        }
        
        //4.7 Commodity Price
        [HttpGet("commodity-price")]
        [Produces("application/json", "application/xml")]
        //[Authorize]
        public async Task<IActionResult> GetCommodityPrice(string period, string? format = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetCommodityPrice(period, cancellationToken);

                // Check if no data found
                if (items == null)
                {
                    return NotFound($"No commodity price data found for period '{period}'");
                }
                
                return Ok(items);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetCommodityPrice: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while getting commodity price data for period: {Period}", period);
                return StatusCode(500, "Internal server error occurred while retrieving commodity price data.");
            }
        }


    }
}

