using Microsoft.AspNetCore.Mvc;
using OpenAPI.API.Constants;
using OpenAPI.Application.Services;
using OpenAPI.Application.Extensions;
using OpenAPI.Domain.Entities.Statistics;

namespace OpenAPI.API.Controllers.Examples
{
    /// <summary>
    /// Example showing how to use EstatSdmxMappingService for SDMX format conversion
    /// Update your existing controllers to follow this pattern
    /// </summary>
    [ApiController]
    [Route("api/v1/examples")]
    public class SdmxConversionExampleController : ControllerBase
    {
        private readonly EstatSdmxMappingService _estatSdmxService;
        private readonly StatisticsServices _statisticsService;
        private readonly ILogger<SdmxConversionExampleController> _logger;

        public SdmxConversionExampleController(
            EstatSdmxMappingService estatSdmxService,
            StatisticsServices statisticsService,
            ILogger<SdmxConversionExampleController> logger)
        {
            _estatSdmxService = estatSdmxService;
            _statisticsService = statisticsService;
            _logger = logger;
        }

        /// <summary>
        /// EXAMPLE 1: Using EstatSdmxMappingService for Price Indices
        /// 
        /// Usage:
        /// GET /api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx
        /// Accept: application/json  (or application/xml for SDMX-ML)
        /// </summary>
        [HttpGet("price-indices-sdmx")]
        [Produces("application/json", "application/xml")]
        public async Task<IActionResult> GetPriceIndicesSdmx(
            [FromQuery] string type,
            [FromQuery] string? period = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. Get your data from service (regular JSON format)
                var priceIndicesData = await _statisticsService.GetPriceIndices(type, period, cancellationToken);

                if (priceIndicesData?.priceIndices == null || priceIndicesData.priceIndices.Count == 0)
                {
                    return NotFound($"No price indices data found");
                }

                // 2. Convert to Estat SDMX DataMessage
                var sdmxMessage = _estatSdmxService.ConvertPriceIndicesToSdmxMessage(priceIndicesData, period);

                // 3. Handle content negotiation
                var acceptHeader = Request.Headers.Accept.ToString();
                var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);

                // 4. Serialize to appropriate format
                var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

                return new ContentResult
                {
                    Content = response as string ?? response.ToString(),
                    ContentType = contentType,
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting price indices to SDMX");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// EXAMPLE 2: Using EstatSdmxMappingService for Generic Data
        /// 
        /// Usage for any generic data structure:
        /// GET /api/v1/statistics/daily/economic-indicator/real-gdp-growth?period=2025-01-15&format=sdmx
        /// Accept: application/vnd.sdmx.json  (or application/xml)
        /// </summary>
        [HttpGet("generic-sdmx")]
        [Produces("application/json", "application/xml")]
        public async Task<IActionResult> GetGenericDataSdmx(
            [FromQuery] string period,
            [FromQuery] string? format = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (format?.Equals("sdmx", StringComparison.OrdinalIgnoreCase) != true)
                {
                    return BadRequest("Use format=sdmx parameter for SDMX conversion");
                }

                // 1. Get your data from service
                var genericData = await _statisticsService.GetRealGdpGrowth(period, cancellationToken);

                if (genericData == null)
                {
                    return NotFound($"No data found for period '{period}'");
                }

                // 2. Convert to SDMX using EstatSdmxMappingService
                var sdmxMessage = _estatSdmxService.ConvertGenericDataToSdmxMessage(
                    new List<Dictionary<string, object>> { new Dictionary<string, object> { { "value", genericData } } },
                    "DSD_REAL_GDP",
                    "REAL_GDP_GROWTH",
                    period
                );

                // 3. Return with appropriate content type
                var acceptHeader = Request.Headers.Accept.ToString();
                var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);

                var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

                return new ContentResult
                {
                    Content = response as string ?? response.ToString(),
                    ContentType = contentType,
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting generic data to SDMX");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// EXAMPLE 3: Updating existing endpoint with format=sdmx parameter
        /// 
        /// Pattern to apply to your WeeklyEconomicIndicatorController and DailyEconomicIndicatorController:
        /// 
        /// [HttpGet("your-endpoint")]
        /// public async Task<IActionResult> YourEndpoint(
        ///     [FromQuery] string? format = null,
        ///     CancellationToken cancellationToken = default)
        /// {
        ///     try
        ///     {
        ///         var data = await _service.GetYourData(cancellationToken);
        ///         
        ///         // Check if SDMX format is requested
        ///         if (format?.Equals("sdmx", StringComparison.OrdinalIgnoreCase) == true)
        ///         {
        ///             // Convert to SDMX
        ///             var sdmxMessage = _estatSdmxService.ConvertXxxToSdmxMessage(data, ...);
        ///             var acceptHeader = Request.Headers.Accept.ToString();
        ///             var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);
        ///             return new ContentResult 
        ///             { 
        ///                 Content = response as string,
        ///                 ContentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader),
        ///                 StatusCode = 200
        ///             };
        ///         }
        ///         
        ///         // Return regular JSON
        ///         return Ok(data);
        ///     }
        ///     catch (Exception ex)
        ///     {
        ///         _logger.LogError(ex, "Error");
        ///         return StatusCode(500, "Error");
        ///     }
        /// }
        /// </summary>
        [HttpGet("integration-pattern")]
        public IActionResult GetIntegrationPattern()
        {
            var exampleMarkdown = @"
# EstatSdmxMappingService Integration Pattern

## Step 1: Inject the service
```csharp
private readonly EstatSdmxMappingService _estatSdmxService;

public YourController(EstatSdmxMappingService estatSdmxService, ...)
{
    _estatSdmxService = estatSdmxService;
}
```

## Step 2: Check if SDMX format is requested
```csharp
if (format?.Equals(""sdmx"", StringComparison.OrdinalIgnoreCase) == true)
{
    // Convert to SDMX format
}
```

## Step 3: Convert data to SDMX
```csharp
// For price indices
var sdmxMessage = _estatSdmxService.ConvertPriceIndicesToSdmxMessage(data, period);

// For generic data
var sdmxMessage = _estatSdmxService.ConvertGenericDataToSdmxMessage(
    dataList, 
    dataStructureId, 
    indicatorValue, 
    period
);
```

## Step 4: Handle content negotiation and serialize
```csharp
var acceptHeader = Request.Headers.Accept.ToString();
var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);
var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

return new ContentResult
{
    Content = response as string,
    ContentType = contentType,
    StatusCode = 200
};
```

## Usage Examples

### Get Price Indices in SDMX-JSON format
```
GET /api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx
Accept: application/json
```

### Get Price Indices in SDMX-ML (XML) format
```
GET /api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx
Accept: application/xml
```

## Key Features
- Automatic SDMX dimension/attribute mapping from domain data
- Content negotiation: JSON, XML, SDMX-JSON support
- Uses Estat.SdmxSource library for standards compliance
- Configurable via appsettings.json SDMX section
- Logging for troubleshooting
";
            return Ok(new { pattern = exampleMarkdown });
        }
    }
}
