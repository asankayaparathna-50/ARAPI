# Estat.SdmxSource Integration Guide

## Overview

This guide explains how to use the **Estat.SdmxSource.SdmxObjects** library to convert your API data from JSON format to SDMX format (both JSON and XML/SDMX-ML).

## What is Estat.SdmxSource?

**Estat.SdmxSource** is the official SDMX library from Eurostat providing:
- Strongly-typed SDMX objects (DataMessage, DataSet, Series, Obs, etc.)
- Full SDMX 2.1 and 3.0 standard compliance
- Automatic serialization to SDMX-JSON and SDMX-ML (XML)
- Built-in dimension and attribute handling

## Architecture

```
Your Domain Entity (DataValue, PriceIndices, etc.)
    ↓
EstatSdmxMappingService (maps to Estat SDMX objects)
    ↓
Estat.Sdmx.Model.DataMessage
    ↓
SdmxConversionExtensions (serialization & content negotiation)
    ↓
JSON/XML Response
```

## Installation

The package has been added to your projects:
- `OpenAPI.API.csproj`: Added `Estat.SdmxSource` v2.0.1
- `OpenAPI.Application.csproj`: Added `Estat.SdmxSource` v2.0.1

## Key Services

### 1. **EstatSdmxMappingService** 
Location: `src/OpenAPI.Application/Services/EstatSdmxMappingService.cs`

**Key Methods:**
- `ConvertPriceIndicesToSdmxMessage()` - Converts PriceIndices to SDMX DataMessage
- `ConvertGenericDataToSdmxMessage()` - Converts any generic data list
- `SerializeToSdmxJson()` - Serializes to SDMX-JSON format
- `SerializeToSdmxXml()` - Serializes to SDMX-ML (XML) format

**Example Usage:**
```csharp
var sdmxMessage = _estatSdmxService.ConvertPriceIndicesToSdmxMessage(priceIndices, period);
var jsonOutput = _estatSdmxService.SerializeToSdmxJson(sdmxMessage);
```

### 2. **SdmxConversionExtensions**
Location: `src/OpenAPI.Application/Extensions/SdmxConversionExtensions.cs`

**Key Methods:**
- `ToSdmxResponse()` - Content negotiation and serialization in one call
- `GetSdmxContentType()` - Determines correct content-type header

## Implementation Steps

### Step 1: Update Your Controller

```csharp
[ApiController]
[Route("api/v1/statistics/weekly/economic-indicator")]
public class WeeklyEconomicIndicatorController : ControllerBase
{
    private readonly EstatSdmxMappingService _estatSdmxService;
    private readonly StatisticsServices _service;

    public WeeklyEconomicIndicatorController(
        EstatSdmxMappingService estatSdmxService,
        StatisticsServices service)
    {
        _estatSdmxService = estatSdmxService;
        _service = service;
    }

    [HttpGet("price-indices")]
    public async Task<IActionResult> GetPriceIndices(
        [FromQuery] string type,
        [FromQuery] string? period = null,
        [FromQuery] string? format = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _service.GetPriceIndices(type, period, cancellationToken);

            if (items?.priceIndices?.Count == 0)
                return NotFound("No data found");

            // NEW: Check if SDMX format requested
            if (format?.Equals("sdmx", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Convert to Estat SDMX objects
                var sdmxMessage = _estatSdmxService.ConvertPriceIndicesToSdmxMessage(items, period);
                
                // Get Accept header for content negotiation
                var acceptHeader = Request.Headers.Accept.ToString();
                var contentType = SdmxConversionExtensions.GetSdmxContentType(acceptHeader);
                
                // Serialize to JSON or XML
                var response = sdmxMessage.ToSdmxResponse(acceptHeader, _estatSdmxService);

                return new ContentResult
                {
                    Content = response as string ?? response.ToString(),
                    ContentType = contentType,
                    StatusCode = 200
                };
            }

            // Regular JSON response
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price indices");
            return StatusCode(500, "Error");
        }
    }
}
```

### Step 2: Configuration (appsettings.json)

Your SDMX settings are already configured under the `Sdmx` section:

```json
{
  "Sdmx": {
    "Agency": {
      "Id": "CBSL",
      "Name": "Central Bank of Sri Lanka",
      "ContactName": "Statistics Department",
      "Email": "statistics@cbsl.lk",
      "Website": "https://www.cbsl.gov.lk"
    },
    "DataStructures": {
      "PriceIndices": {
        "Id": "DSD_PRICE_INDICES",
        "Version": "1.0",
        "WeeklyFrequency": "W",
        "UnitMeasureIndex": "IDX"
      }
    },
    "Common": {
      "DimensionAtObservation": "TIME_PERIOD",
      "ReferenceArea": "LK",
      "Language": "en"
    }
  }
}
```

### Step 3: Test Your Endpoint

**Request SDMX-JSON:**
```bash
curl -X GET "http://localhost:7091/api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx" \
  -H "Accept: application/json"
```

**Request SDMX-ML (XML):**
```bash
curl -X GET "http://localhost:7091/api/v1/statistics/weekly/economic-indicator/price-indices?type=NCPI&format=sdmx" \
  -H "Accept: application/xml"
```

## SDMX Data Structure

### Dimensions (Key Concepts)
- **FREQ**: Frequency (W=Weekly, M=Monthly, D=Daily, etc.)
- **INDICATOR**: The specific indicator/variable
- **REF_AREA**: Geographic reference (e.g., LK for Sri Lanka)
- **TIME_PERIOD**: Time reference (YYYY-MM or YYYY-MM-DD format)

### Attributes (Metadata)
- **UNIT_MEASURE**: Unit of measurement (e.g., IDX for Index, LKR for currency)
- **OBS_STATUS**: Observation status (A=Available, P=Provisional, etc.)
- **DECIMALS**: Number of decimal places

### Example SDMX-JSON Output
```json
{
  "header": {
    "id": "PRICE_INDICES_20250115123456",
    "prepared": "2025-01-15T12:34:56Z",
    "sender": {
      "id": "CBSL"
    }
  },
  "dataSets": [
    {
      "structureRef": "CBSL:DSD_PRICE_INDICES(1.0)",
      "series": {
        "0:0:0": {
          "seriesKey": {
            "FREQ": ["W"],
            "INDICATOR": ["NCPI"],
            "REF_AREA": ["LK"]
          },
          "observations": {
            "2025-01": [105.2, {"UNIT_MEASURE": "IDX"}]
          }
        }
      }
    }
  ]
}
```

## Data Mapping Reference

### From Domain Entity to SDMX

| Domain Property | SDMX Component | Example |
|---|---|---|
| `DataCode.Code` | INDICATOR dimension | "NCPI", "CCPI" |
| `DataValue.PeriodID` | TIME_PERIOD dimension | "2025-01" |
| `DataValue.Value` | Observation value | 105.23 |
| `DataValue.Status` | OBS_STATUS attribute | "A" (Available) |
| `frequency` | FREQ dimension | "M", "W", "D" |

### Mapping Logic (EstatSdmxMappingService)

```csharp
// Series key mapping
var seriesKey = new SortedDictionary<string, string>
{
    { "FREQ", frequency },      // From DataValue frequency
    { "INDICATOR", indicator }, // From DataCode
    { "REF_AREA", "LK" }        // From config
};

// Observation mapping
var obs = new Obs
{
    ObsKey = new SortedDictionary<string, string>
    {
        { "TIME_PERIOD", periodId } // From DataValue.PeriodID
    },
    ObsValue = value,           // From DataValue.Value
    ObsAttributes = new SortedDictionary<string, string>
    {
        { "UNIT_MEASURE", "IDX" },
        { "OBS_STATUS", status }
    }
};
```

## Troubleshooting

### Issue: "Package not found"
**Solution:** Ensure packages are restored:
```bash
cd src/OpenAPI.API
dotnet restore
dotnet build
```

### Issue: Null reference in serialization
**Solution:** Check that DataMessage header and datasets are properly populated. Use the example controller for reference.

### Issue: Content-Type not negotiating correctly
**Solution:** Verify Accept header is being read:
```csharp
var acceptHeader = Request.Headers.Accept.ToString();
_logger.LogInformation("Accept header: {AcceptHeader}", acceptHeader);
```

## Best Practices

1. **Always use `format=sdmx` parameter** - Makes it explicit when SDMX is requested
2. **Content negotiate via Accept header** - Support both JSON and XML clients
3. **Log serialization errors** - SDMX structure mismatches can be hard to debug
4. **Validate data before mapping** - Ensure your domain data has required fields
5. **Version your SDMX structures** - Use semantic versioning in DSD settings

## Next Steps

1. ✅ Added Estat.SdmxSource package
2. ✅ Created EstatSdmxMappingService for data conversion
3. ✅ Created SdmxConversionExtensions for helper methods
4. ⏳ Update your existing endpoints (DailyEconomicIndicatorController, WeeklyEconomicIndicatorController)
5. ⏳ Test all endpoints with `format=sdmx` parameter
6. ⏳ Update API documentation in Swagger

## File References

- Service: [EstatSdmxMappingService.cs](../Services/EstatSdmxMappingService.cs)
- Extensions: [SdmxConversionExtensions.cs](../Extensions/SdmxConversionExtensions.cs)
- Example Controller: [SdmxConversionExampleController.cs](../../API/Controllers/Examples/SdmxConversionExampleController.cs)
- Configuration: `appsettings.json` → `Sdmx` section

## References

- [Estat.SdmxSource NuGet](https://www.nuget.org/packages/Estat.SdmxSource/)
- [SDMX 2.1 Standard](https://sdmx.org/)
- [SDMX-JSON Format](https://github.com/sdmx-twg/sdmx-json/)
