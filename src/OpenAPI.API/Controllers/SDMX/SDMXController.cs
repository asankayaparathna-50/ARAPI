using Microsoft.AspNetCore.Mvc;
using OpenAPI.Application.Services;

namespace OpenAPI.API.Controllers.SDMX
{
    [ApiController]
    [Route("api/v1/sdmx")]
    public class SdmxController : ControllerBase
    {
        private const string ApplicationXml = "application/xml";
        private readonly SdmxTransformationService _sdmxService;
        private readonly ILogger<SdmxController> _logger;

        public SdmxController(SdmxTransformationService sdmxService, ILogger<SdmxController> logger)
        {
            _sdmxService = sdmxService;
            _logger = logger;
        }

        [HttpGet("statistics/weekly/economic-indicator/price-indices/structure")]
        [Produces("application/json", "application/xml")]
        [ProducesResponseType(typeof(object), 200)]
        //[Authorize]
        public IActionResult GetPriceIndicesDataStructure([FromHeader(Name = "Accept")] string acceptHeader = "")
        {
            try
            {
                var dataStructure = _sdmxService.CreatePriceIndicesDataStructure();

                // Content negotiation based on Accept header
                if (acceptHeader.Contains(ApplicationXml) || acceptHeader.Contains("text/xml"))
                {
                    return new ObjectResult(dataStructure)
                    {
                        ContentTypes = { ApplicationXml }
                    };
                }

                return Ok(dataStructure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting price-indices data structure.");
                return StatusCode(500, "Internal server error. /n" + ex);
            }
        }

        [HttpGet("statistics/weekly/economic-indicator/prices/structure")]
        [Produces("application/json", "application/xml")]
        [ProducesResponseType(typeof(object), 200)]
        //[Authorize]
        public IActionResult GetMarketPricesDataStructure([FromHeader(Name = "Accept")] string acceptHeader = "")
        {
            try
            {
                var dataStructure = _sdmxService.CreateMarketPricesDataStructure();

                // Content negotiation based on Accept header
                if (acceptHeader.Contains("application/xml") || acceptHeader.Contains("text/xml"))
                {
                    return new ObjectResult(dataStructure)
                    {
                        ContentTypes = { "application/xml" }
                    };
                }

                return Ok(dataStructure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during getting market prices data structure.");
                return StatusCode(500, "Internal server error. /n" + ex);
            }
        }
    }
}
