using OpenAPI.Domain.Entities.Statistics.Sdmx;
using OpenAPI.Application.Services;

namespace OpenAPI.Application.Extensions
{
    /// <summary>
    /// Extension methods for SDMX conversion and content negotiation
    /// </summary>
    public static class SdmxConversionExtensions
    {
        /// <summary>
        /// Converts SDMX DataMessage to response format based on Accept header
        /// Supports both SDMX-JSON and SDMX-ML (XML) formats
        /// </summary>
        public static string ToSdmxResponse(
            this SdmxDataMessage message,
            string acceptHeader,
            EstatSdmxMappingService mappingService)
        {
            if (string.IsNullOrEmpty(acceptHeader))
            {
                // Default to JSON
                return mappingService.SerializeToSdmxJson(message);
            }

            if (acceptHeader.Contains("application/xml") || acceptHeader.Contains("text/xml"))
            {
                return mappingService.SerializeToSdmxXml(message);
            }

            if (acceptHeader.Contains("application/vnd.sdmx") || acceptHeader.Contains("application/json"))
            {
                return mappingService.SerializeToSdmxJson(message);
            }

            // Default to JSON
            return mappingService.SerializeToSdmxJson(message);
        }

        /// <summary>
        /// Determines content type based on Accept header
        /// Returns standard MIME type for SDMX responses
        /// </summary>
        public static string GetSdmxContentType(string acceptHeader)
        {
            if (acceptHeader.Contains("application/xml") || acceptHeader.Contains("text/xml"))
            {
                return "application/xml; charset=utf-8";
            }

            if (acceptHeader.Contains("application/vnd.sdmx.json"))
            {
                return "application/vnd.sdmx.json; charset=utf-8";
            }

            // Default to JSON
            return "application/json; charset=utf-8";
        }

        /// <summary>
        /// Checks if the request explicitly asks for SDMX format
        /// </summary>
        public static bool IsSdmxFormatRequested(string? format)
        {
            return format?.Equals("sdmx", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Gets the preferred format from query parameter or Accept header
        /// </summary>
        public static string? GetPreferredSdmxFormat(string? formatParam, string? acceptHeader)
        {
            if (!string.IsNullOrEmpty(formatParam))
                return formatParam.ToLower();

            if (string.IsNullOrEmpty(acceptHeader))
                return "json";

            if (acceptHeader.Contains("xml"))
                return "xml";

            if (acceptHeader.Contains("json"))
                return "json";

            return "json";
        }
    }
}
