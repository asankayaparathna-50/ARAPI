# Update Your Existing Endpoints - Step by Step

This guide shows exactly how to update your existing `WeeklyEconomicIndicatorController` to support SDMX format.

## Current Code (Before)

Here's what your `price-indices` endpoint looks like now:

```csharp
[HttpGet("price-indices")]
[Produces("application/json")]
public async Task<IActionResult> GetPriceIndices(
    string type, 
    string? period = null,  
    string? format = null, 
    CancellationToken cancellationToken = default)
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
    var sdmxData = _sdmxService.ConvertPriceIndicesToSdmx(items, period);
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
```

## Updated Code (After)

### Step 1: Add EstatSdmxMappingService Injection

```csharp
[ApiController]
[Route("api/v1/statistics/weekly/economic-indicator")]
[StatisticsQueryValidation]
public class WeeklyEconomicIndicatorController : ControllerBase
{
    private readonly StatisticsServices _service;
    private readonly SdmxTransformationService _sdmxService;  // KEEP THIS (for structure definitions)
    private readonly EstatSdmxMappingService _estatSdmxService;  // ADD THIS (NEW)
    private readonly ILogger<WeeklyEconomicIndicatorController> _logger;

    public WeeklyEconomicIndicatorController(
        StatisticsServices service,
        SdmxTransformationService sdmxService,
        EstatSdmxMappingService estatSdmxService,  // ADD THIS
        ILogger<WeeklyEconomicIndicatorController> logger)
    {
        _service = service;
        _sdmxService = sdmxService;
        _estatSdmxService = estatSdmxService;  // ADD THIS
        _logger = logger;
    }
```

### Step 2: Update the price-indices Method

```csharp
[HttpGet("price-indices")]
[Produces("application/json", "application/xml")]  // UPDATE THIS to include XML
public async Task<IActionResult> GetPriceIndices(
    string type, 
    string? period = null,  
    string? format = null,  // Already there
    CancellationToken cancellationToken = default)
{
    try
    {
        var items = await _service.GetPriceIndices(type, period, cancellationToken);

        if (items == null || (items.priceIndices?.Count == 0))
        {
            return NotFound($"No price indices data found for period '{period ?? "latest"}'");
        }

        // NEW: Use new SDMX mapping service for format=sdmx
        if (SdmxConversionExtensions.IsSdmxFormatRequested(format))
        {
            try
            {
                // Convert to SDMX using the new service
                var sdmxMessage = _estatSdmxService.ConvertPriceIndicesToSdmxMessage(items, period);
                
                // Get Accept header for content negotiation
                var acceptHeader = Request.Headers.Accept.ToString();
                var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);
                
                // Serialize to JSON or XML
                var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

                return new ContentResult
                {
                    Content = response,
                    ContentType = contentType,
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting price indices to SDMX format");
                return StatusCode(500, "Error converting data to SDMX format");
            }
        }

        // Existing JSON response (unchanged)
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

// KEEP all existing helper methods unchanged:
private ObjectResult FormatSdmxPriceIndices(PriceIndices items, string? period)
{
    // ... existing code ...
}

private object ShapePriceIndicesResponse(PriceIndices items, string? period, string type)
{
    // ... existing code ...
}
```

### Step 3: Add Using Statement

Add this at the top of your file:

```csharp
using OpenAPI.Application.Extensions;  // ADD THIS for IsSdmxFormatRequested() and other extensions
```

## That's It!

Your endpoint now supports SDMX format. No breaking changes to existing code.

## Testing

**Before (still works):**
```bash
curl http://localhost:7091/api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI
→ Returns regular JSON
```

**After (new capability):**
```bash
curl http://localhost:7091/api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx \
  -H "Accept: application/json"
→ Returns SDMX-JSON

curl http://localhost:7091/api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx \
  -H "Accept: application/xml"
→ Returns SDMX-ML (XML)
```

## For Other Endpoints

Apply the same pattern to other endpoints:

### Daily Economic Indicators

```csharp
// In DailyEconomicIndicatorController

private readonly EstatSdmxMappingService _estatSdmxService;

public DailyEconomicIndicatorController(
    StatisticsServices service,
    EstatSdmxMappingService estatSdmxService,
    ILogger<DailyEconomicIndicatorController> logger)
{
    _service = service;
    _estatSdmxService = estatSdmxService;
    _logger = logger;
}

[HttpGet("real-gdp-growth")]
public async Task<IActionResult> GetRealGdpGrowth(
    [FromQuery] string period, 
    CancellationToken cancellationToken,
    [FromQuery] string? format = null)  // ADD THIS
{
    try
    {
        if (string.IsNullOrWhiteSpace(period))
            return BadRequest(ApiErrorMessages.PeriodRequired);

        var items = await _service.GetRealGdpGrowth(period, cancellationToken);
        if (items == null)
            return NotFound($"No real GDP growth data found for period '{period}'");

        // NEW: Add SDMX support
        if (SdmxConversionExtensions.IsSdmxFormatRequested(format))
        {
            try
            {
                // For single-value responses, wrap in list
                var dataList = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        { "periodId", period },
                        { "value", items },  // Your data value
                        { "frequency", "D" }  // Daily
                    }
                };

                var sdmxMessage = _estatSdmxService.ConvertGenericDataToSdmxMessage(
                    dataList,
                    "DSD_REAL_GDP",
                    "REAL_GDP_GROWTH",
                    period
                );

                var acceptHeader = Request.Headers.Accept.ToString();
                var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);
                var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

                return new ContentResult
                {
                    Content = response,
                    ContentType = contentType,
                    StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting to SDMX format");
                return StatusCode(500, "Error converting data to SDMX format");
            }
        }

        // Existing response (unchanged)
        return Ok(items);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching real GDP growth for period: {Period}", period);
        return StatusCode(500, ApiErrorMessages.ProcessingError);
    }
}
```

## Benefits of This Approach

✅ **Backward Compatible** - Existing code unchanged  
✅ **Optional** - Clients don't have to use SDMX  
✅ **Automatic Serialization** - JSON/XML handled automatically  
✅ **Standards Compliant** - Follows SDMX 2.1  
✅ **Easy Maintenance** - Service logic in one place  
✅ **Minimal Code Changes** - Just add a few lines per endpoint  

## Summary Checklist

For each endpoint you want to enable SDMX:

- [ ] Inject `EstatSdmxMappingService`
- [ ] Add `[FromQuery] string? format = null` parameter
- [ ] Add using statement for `SdmxConversionExtensions`
- [ ] Check `if (SdmxConversionExtensions.IsSdmxFormatRequested(format))`
- [ ] Call appropriate conversion method (`ConvertPriceIndicesToSdmxMessage` or `ConvertGenericDataToSdmxMessage`)
- [ ] Use `ToSdmxResponse()` for serialization
- [ ] Return `ContentResult` with appropriate content type
- [ ] Test with curl/Postman

That's it! Your endpoints now return SDMX-formatted data on demand.
