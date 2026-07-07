using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OpenAPI.Domain.Entities.Statistics;
using OpenAPI.Domain.Entities.Statistics.Sdmx;
using OpenAPI.Domain.Entities.Auth;
using System.Text.Json;
using System.Globalization;

namespace OpenAPI.Application.Services
{
    /// <summary>
    /// Service for converting domain entities to SDMX-compliant format
    /// Maps database entities to SDMX DTOs following SDMX 2.1 standard
    /// Supports both SDMX-JSON and SDMX-ML (XML) serialization
    /// </summary>
    public class EstatSdmxMappingService
    {
        private readonly SdmxSettings _sdmxSettings;
        private readonly ILogger<EstatSdmxMappingService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string FrequencyDimension = "FREQ";
        private const string TimePeriodDimension = "TIME_PERIOD";
        private const string RefAreaDimension = "REF_AREA";
        private const string IndicatorDimension = "INDICATOR";
        private const string ObsStatusAttribute = "OBS_STATUS";
        private const string UnitMeasureAttribute = "UNIT_MEASURE";
        private const string DecimalsAttribute = "DECIMALS";

        public EstatSdmxMappingService(IOptions<SdmxSettings> sdmxSettings, ILogger<EstatSdmxMappingService> logger)
        {
            _sdmxSettings = sdmxSettings.Value;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Converts PriceIndices data to SDMX DataMessage format
        /// </summary>
        public SdmxDataMessage ConvertPriceIndicesToSdmxMessage(PriceIndices priceIndices, string? period)
        {
            try
            {
                var message = new SdmxDataMessage
                {
                    Header = CreateHeader("PRICE_INDICES"),
                    DataSet = CreatePriceIndicesDataSet(priceIndices, period)
                };

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PriceIndices to SDMX message");
                throw;
            }
        }

        /// <summary>
        /// Converts generic data dictionary to SDMX DataMessage
        /// </summary>
        public SdmxDataMessage ConvertGenericDataToSdmxMessage(
            List<Dictionary<string, object>> data,
            string dataStructureId,
            string indicatorDimensionValue,
            string? period = null)
        {
            try
            {
                var message = new SdmxDataMessage
                {
                    Header = CreateHeader(dataStructureId),
                    DataSet = CreateGenericDataSet(data, dataStructureId, indicatorDimensionValue, period)
                };

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting generic data to SDMX message for structure: {DataStructureId}", dataStructureId);
                throw;
            }
        }

        /// <summary>
        /// Creates SDMX Header with agency and structural metadata
        /// </summary>
        private SdmxHeader CreateHeader(string messageId)
        {
            var header = new SdmxHeader
            {
                Id = $"{messageId}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Prepared = DateTime.UtcNow,
                Sender = new SdmxSender 
                { 
                    Id = _sdmxSettings.Agency.Id,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.Agency.Name },
                    Contact = new SdmxContact
                    {
                        Name = new SdmxLocalizedText { Text = _sdmxSettings.Agency.ContactName },
                        Email = _sdmxSettings.Agency.Email,
                        Uri = _sdmxSettings.Agency.Website
                    }
                },
                StructureRef = new SdmxStructureRef
                {
                    StructureId = _sdmxSettings.DataStructures.PriceIndices.Id,
                    Namespace = _sdmxSettings.Common.Namespace,
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };

            return header;
        }

        /// <summary>
        /// Creates DataSet for Price Indices data
        /// </summary>
        private SdmxDataSet CreatePriceIndicesDataSet(PriceIndices priceIndices, string? period)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = BuildStructureRef(_sdmxSettings.DataStructures.PriceIndices.Id)
            };

            if (priceIndices.priceIndices?.Any() == true)
            {
                foreach (var item in priceIndices.priceIndices)
                {
                    var series = MapPriceIndexToSeries(item);
                    if (series != null)
                    {
                        dataSet.Series.Add(series);
                    }
                }
            }

            return dataSet;
        }

        /// <summary>
        /// Creates generic DataSet for any data structure
        /// </summary>
        private SdmxDataSet CreateGenericDataSet(
            List<Dictionary<string, object>> data,
            string dataStructureId,
            string indicatorValue,
            string? period)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = BuildStructureRef(dataStructureId)
            };

            if (data?.Any() == true)
            {
                var frequencyCode = GetFrequencyCode(data);
                var refArea = _sdmxSettings.Common.ReferenceArea;

                foreach (var item in data)
                {
                    var seriesKey = CreateSeriesKey(frequencyCode, indicatorValue, refArea);
                    var series = new SdmxSeries { SeriesKey = seriesKey };

                    if (item.TryGetValue("periodId", out var periodObj) && item.TryGetValue("value", out var valueObj))
                    {
                        var obs = new SdmxObservation
                        {
                            ObsKey = new SdmxObsKey
                            {
                                Values = new List<SdmxKeyValue>
                                {
                                    new SdmxKeyValue { Id = TimePeriodDimension, Value = periodObj?.ToString() ?? period ?? DateTime.Now.ToString("yyyy-MM") }
                                }
                            },
                            ObsValue = new SdmxObsValue
                            {
                                Value = decimal.TryParse(valueObj?.ToString(), out var decVal) ? decVal : 0
                            },
                            Attributes = new SdmxObsAttributes
                            {
                                Values = new List<SdmxAttributeValue>
                                {
                                    new SdmxAttributeValue { Id = UnitMeasureAttribute, Value = GetUnitMeasure(dataStructureId) },
                                    new SdmxAttributeValue { Id = DecimalsAttribute, Value = "2" }
                                }
                            }
                        };

                        if (item.TryGetValue("status", out var statusObj))
                        {
                            obs.Attributes.Values.Add(new SdmxAttributeValue 
                            { 
                                Id = ObsStatusAttribute, 
                                Value = statusObj?.ToString() ?? "A" 
                            });
                        }

                        series.Observations.Add(obs);
                    }

                    dataSet.Series.Add(series);
                }
            }

            return dataSet;
        }

        /// <summary>
        /// Maps a price index item to SDMX Series
        /// </summary>
        private SdmxSeries? MapPriceIndexToSeries(Dictionary<string, object> item)
        {
            if (item == null)
                return null;

            var key = item.TryGetValue("key", out var keyObj) ? keyObj?.ToString() : null;
            var periodId = item.TryGetValue("periodId", out var periodObj) ? periodObj?.ToString() : null;
            var value = item.TryGetValue("value", out var valueObj) ? valueObj?.ToString() : null;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(periodId))
                return null;

            var seriesKey = CreateSeriesKey(
                _sdmxSettings.DataStructures.PriceIndices.WeeklyFrequency,
                key,
                _sdmxSettings.Common.ReferenceArea
            );

            var series = new SdmxSeries { SeriesKey = seriesKey };
            var obsValue = decimal.TryParse(value, out var decVal) ? decVal : 0;

            var obs = new SdmxObservation
            {
                ObsKey = new SdmxObsKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new SdmxKeyValue { Id = TimePeriodDimension, Value = periodId }
                    }
                },
                ObsValue = new SdmxObsValue { Value = obsValue },
                Attributes = new SdmxObsAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new SdmxAttributeValue { Id = UnitMeasureAttribute, Value = _sdmxSettings.DataStructures.PriceIndices.UnitMeasureIndex },
                        new SdmxAttributeValue { Id = DecimalsAttribute, Value = "2" },
                        new SdmxAttributeValue { Id = ObsStatusAttribute, Value = item.TryGetValue("status", out var statusObj) ? statusObj?.ToString() ?? "A" : "A" }
                    }
                }
            };

            series.Observations.Add(obs);
            return series;
        }

        /// <summary>
        /// Creates a series key with SDMX dimensions
        /// </summary>
        private SdmxSeriesKey CreateSeriesKey(string frequency, string indicator, string refArea)
        {
            return new SdmxSeriesKey
            {
                Values = new List<SdmxKeyValue>
                {
                    new SdmxKeyValue { Id = FrequencyDimension, Value = frequency },
                    new SdmxKeyValue { Id = IndicatorDimension, Value = indicator },
                    new SdmxKeyValue { Id = RefAreaDimension, Value = refArea }
                }
            };
        }

        /// <summary>
        /// Builds a proper SDMX structure reference
        /// </summary>
        private string BuildStructureRef(string dataStructureId)
        {
            return $"{_sdmxSettings.Agency.Id}:{dataStructureId}({_sdmxSettings.DataStructures.PriceIndices.Version})";
        }

        /// <summary>
        /// Gets frequency code from data (defaults to "M" for monthly)
        /// </summary>
        private string GetFrequencyCode(List<Dictionary<string, object>> data)
        {
            if (data?.Any() == true && data.First().TryGetValue("frequency", out var freqObj))
            {
                return freqObj?.ToString() ?? "M";
            }
            return "M";
        }

        /// <summary>
        /// Gets unit measure for a data structure
        /// </summary>
        private string GetUnitMeasure(string dataStructureId)
        {
            return dataStructureId.Contains("PRICE")
                ? _sdmxSettings.DataStructures.PriceIndices.UnitMeasureIndex
                : "UNIT";
        }

        /// <summary>
        /// Serializes SDMX message to JSON format
        /// </summary>
        public string SerializeToSdmxJson(SdmxDataMessage message)
        {
            try
            {
                return JsonSerializer.Serialize(message, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing SDMX message to JSON");
                throw;
            }
        }

        /// <summary>
        /// Serializes SDMX message to XML format (SDMX-ML)
        /// </summary>
        public string SerializeToSdmxXml(SdmxDataMessage message)
        {
            try
            {
                var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(SdmxDataMessage));
                using (var stringWriter = new System.IO.StringWriter())
                {
                    xmlSerializer.Serialize(stringWriter, message);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing SDMX message to XML");
                throw;
            }
        }
    }
}
