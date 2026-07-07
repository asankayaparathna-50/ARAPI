To integrate SDMX.NET (Estat.SdmxSource), I would recommend extending the architecture like this:
                     
                     
                     HTTP Request
                           │
                           ▼
                StatisticsController
                           │
                           ▼
                 StatisticsService
                           │
                 GetPriceIndices()
                           │
                           ▼
                  PriceIndicesDto
                    /            \
                   /              \
          JSON Response      SDMX Response
                                  │
                                  ▼
                     ISdmxExportService
                                  │
          ┌───────────────────────┴────────────────────────┐
          │                                                │
     SdmxObjects                               SdmxMlWriter
          │                                                │
          └──────────── Estat.SdmxSource.SdmxAPI ──────────┘
                                  │
                                  ▼
                             SDMX XML


#### Suggested Project Structure

Since your README already contains services such as:

StatisticsServices
SdmxTransformationService
EstatSdmxMappingService
EuristatSdmxTransformationService

I would add a dedicated exporter.

OpenAPI.Application
│
├── Services
│     StatisticsService.cs
│
├── SDMX
│     ISdmxExportService.cs
│     SdmxExportService.cs
│     PriceIndicesSdmxMapper.cs
│
└── Extensions

This keeps all SDMX-specific code together instead of mixing it into the controller.

#### Controller

Your controller becomes very simple.

public async Task<IActionResult> GetPriceIndices(...)
{
    var items = await _service.GetPriceIndices(type, period, cancellationToken);

    if (items == null || items.priceIndices?.Count == 0)
        return NotFound();

    if (string.Equals(format, "sdmx", StringComparison.OrdinalIgnoreCase))
    {
        var xml = await _sdmxExportService.ExportPriceIndicesAsync(items);

        return Content(xml, "application/vnd.sdmx.data+xml");
    }

    return Ok(ShapePriceIndicesResponse(items, period, type));
}

The controller no longer knows anything about SDMX internals.

#### SDMX Export Service

Create an interface:

public interface ISdmxExportService
{
    Task<string> ExportPriceIndicesAsync(PriceIndicesResponse response);
}

Implementation:

public class SdmxExportService : ISdmxExportService
{
    public async Task<string> ExportPriceIndicesAsync(PriceIndicesResponse response)
    {
        // Map DTO to SDMX objects

        // IDataMessage
        // IDataSet
        // ISeries
        // IObservation

        // Serialize using SdmxMlWriter

        return xml;
    }
}

#### Mapper

Separate the mapping logic from the XML generation.

PriceIndicesResponse
        │
        ▼
PriceIndicesSdmxMapper
        │
        ▼
IDataMessage

Where Estat.SdmxSource.SdmxAPI is used

You normally won't instantiate classes from SdmxAPI directly. Instead, your exporter will work with interfaces such as:

IDataMessage
IDataSet
ISeries
IObservation
IDataflowObject
IDataStructureObject

These interfaces are defined in Estat.SdmxSource.SdmxAPI.

The concrete implementations come from Estat.SdmxSource.SdmxObjects, and Estat.SdmxSource.SdmxMlWriter serializes them to SDMX-ML XML.

#### Dependency Injection

Register the exporter in your dependency injection configuration:

services.AddScoped<ISdmxExportService, SdmxExportService>();