using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OpenAPI.API.Constants;
using OpenAPI.API.Controllers.Statistics.EconomicIndicators;
using OpenAPI.API.Filters;
using OpenAPI.Application.Services;
using OpenAPI.Domain.Entities.Statistics;
using OpenAPI.Domain.Entities.Statistics.Sdmx;
using System.Globalization;

namespace OpenAPI.API.Controllers.Statistics.Economic_Indicators
{
    [ApiController]
    [Route("api/v1/statistics/daily/economic-indicator")]
    [StatisticsQueryValidation]
    //[Authorize(Policy = "statistics")]
    public class DailyEconomicIndicatorController : ControllerBase
    {
        private readonly StatisticsServices _service;
        private readonly ILogger<DailyEconomicIndicatorController> _logger;

        public DailyEconomicIndicatorController(StatisticsServices service, ILogger<DailyEconomicIndicatorController> logger)
        {
            _service = service;
            _logger = logger;
        }

        //d.1 Real GDP Growth
        [HttpGet("real-gdp-growth")]
        public async Task<IActionResult> GetRealGdpGrowth([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetRealGdpGrowth(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No real GDP growth data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Real GDP Growth", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching real GDP growth for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.2 YoY Growth
        [HttpGet("yoy-growth")]
        public async Task<IActionResult> GetYoyGrowth([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetYoyGrowth(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No NCPI YoY growth data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "YoY Growth", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCPI YoY growth for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }
        
        //d.3 TT Rate Buying USD ,Selling EUR
        [HttpGet("tt-rate")]
        public async Task<IActionResult> GetTtBuyingUsd([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetTTRate(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No TT Buying USD data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "TT Rate", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching TT Buying USD for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.4 Money Supply
        [HttpGet("money-supply")]
        public async Task<IActionResult> GetMoneySupply([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetMonySupply(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No money supply data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Money Supply", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Money Supply for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.5 USD Spot Rate
        [HttpGet("usd-spot-rate")]
        public async Task<IActionResult> GetUsdSpotRate([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetUsdSpotRate(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No USD Spot Rate data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "USD Spot Rate", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching USD Spot Rate for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.6 Policy Rates - Overnight Policy Rate (OPR)
        [HttpGet("policy-rates")]
        public async Task<IActionResult> GetPolicyRates([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetPolicyRates(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No Policy Rates data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Policy Rates", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Policy Rates for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.7 Average Weighted Prime Lending Rate (AWPR) - Weekly
        [HttpGet("awpr")]
        public async Task<IActionResult> GetAwpr([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetAwpr(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No AWPR data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "AWPR", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching AWPR for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }

        }

        //d.8 Overnight Liquidity (Injection (-) / Absorption (+))
        [HttpGet("overnight-liquidity")]
        public async Task<IActionResult> GetOvernightLiquidity([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetOvernightLiquidity(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No Overnight Liquidity data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Overnight Liquidity", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Overnight Liquidity for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.9 Treasury Bill Primary Market Auction Weighted Average Yield Rate -91 days
        [HttpGet("treasury-bill-yield")]
        public async Task<IActionResult> GetTreasuryBillYield([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetTreasuryBillYield(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No Treasury Bill Yield data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Treasury Bill Yield", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Treasury Bill Yield for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.10 EQUITY- All share price index
        [HttpGet("equity-all-share-price")]
        public async Task<IActionResult> GetAllSharePriceIndex([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetAllSharePriceIndex(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No All Share Price Index data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "All Share Price Index", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching All Share Price Index for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.11 Petroleum
        [HttpGet("petroleum-price")]
        public async Task<IActionResult> GetPetroleum ([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetPetroleum(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No petroleum data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Petroleum Price", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching petroleum data for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        //d.12 Electricity
        [HttpGet("electricity-generation")]
        public async Task<IActionResult> GetElectricity ([FromQuery] string period, CancellationToken cancellationToken, [FromQuery] string? format = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(period))
                    return BadRequest(ApiErrorMessages.PeriodRequired);

                if (!DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    return BadRequest(ApiErrorMessages.InvalidPeriodFormat);
                }

                var items = await _service.GetElectricity(period, cancellationToken);
                if (items == null)
                {
                    return NotFound($"No electricity generation data found for period '{period}'");
                }

                if (!string.IsNullOrEmpty(format) && format.Equals("sdmx", StringComparison.OrdinalIgnoreCase))
                    return Ok(TransformToSdmx(items, "Electricity Generation", period));

                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching electricity generation data for period: {Period}", period);
                return StatusCode(500, ApiErrorMessages.ProcessingError);
            }
        }

        private SdmxDataMessage TransformToSdmx(Modal items, string indicatorName, string period)
        {
            var resolvedPeriod = string.IsNullOrWhiteSpace(period) ? (items.Period ?? string.Empty) : period;
            var resolvedFrequency = string.IsNullOrWhiteSpace(items.Frequency) ? "D" : items.Frequency!;

            var message = new SdmxDataMessage
            {
                Header = new SdmxHeader
                {
                    Id = $"DAILY_ECONOMIC_INDICATOR_{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Prepared = DateTime.UtcNow,
                    Sender = new SdmxSender
                    {
                        Id = "CBSL",
                        Name = new SdmxLocalizedText { Text = "Central Bank of Sri Lanka" },
                        Contact = new SdmxContact
                        {
                            Name = new SdmxLocalizedText { Text = "OpenAPI" },
                            Email = string.Empty,
                            Uri = string.Empty
                        }
                    },
                    StructureRef = new SdmxStructureRef
                    {
                        StructureId = "DSD_DAILY_ECONOMIC_INDICATORS",
                        Namespace = "urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure=CBSL:DSD_DAILY_ECONOMIC_INDICATORS",
                        DimensionAtObservation = "TIME_PERIOD"
                    }
                },
                DataSet = new SdmxDataSet
                {
                    StructureRef = "CBSL:DSD_DAILY_ECONOMIC_INDICATORS",
                    Series = new List<SdmxSeries>()
                }
            };

            foreach (var (itemName, value) in ExtractDataPoints(items.Data))
            {
                var series = new SdmxSeries
                {
                    SeriesKey = new SdmxSeriesKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = "FREQ", Value = resolvedFrequency },
                            new() { Id = "INDICATOR", Value = itemName },
                            new() { Id = "REF_AREA", Value = "LKA" }
                        }
                    },
                    Attributes = new SdmxSeriesAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = "UNIT_MEASURE", Value = "INDEX" },
                            new() { Id = "DECIMALS", Value = "2" },
                            new() { Id = "TITLE", Value = indicatorName }
                        }
                    },
                    Observations = new List<SdmxObservation>
                    {
                        new()
                        {
                            ObsKey = new SdmxObsKey
                            {
                                Values = new List<SdmxKeyValue>
                                {
                                    new() { Id = "TIME_PERIOD", Value = resolvedPeriod }
                                }
                            },
                            ObsValue = new SdmxObsValue { Value = value },
                            Attributes = new SdmxObsAttributes
                            {
                                Values = new List<SdmxAttributeValue>
                                {
                                    new() { Id = "OBS_STATUS", Value = "A" }
                                }
                            }
                        }
                    }
                };

                message.DataSet.Series.Add(series);
            }

            return message;
        }

        private static IEnumerable<(string Name, decimal Value)> ExtractDataPoints(object? data)
        {
            if (data is IDictionary<string, decimal?> decimalData)
            {
                foreach (var entry in decimalData)
                {
                    if (entry.Value.HasValue)
                        yield return (entry.Key, entry.Value.Value);
                }

                yield break;
            }

            if (data is IDictionary<string, object> objectData)
            {
                foreach (var entry in objectData)
                {
                    if (entry.Value == null)
                        continue;

                    if (decimal.TryParse(entry.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        yield return (entry.Key, parsed);
                }
            }
        }
    
    }

}