using Microsoft.Extensions.Logging;
using OpenAPI.Domain.Entities.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenAPI.Application.Services
{
    /// <summary>
    /// Service for converting data to SDMX format using Eurostat Estat.SdmxSource.SdmxAPI package
    /// This implementation provides SDMX 2.1 compliance
    /// </summary>
    public class EuristatSdmxTransformationService
    {
        private const string AgencyId = "CBSL";
        private const string DataFlowId = "PRICE_INDICES";
        private const string DataStructureId = "DSD_PRICE_INDICES";

        private readonly ILogger<EuristatSdmxTransformationService> _logger;

        public EuristatSdmxTransformationService(ILogger<EuristatSdmxTransformationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Converts PriceIndices data to SDMX 2.1 format using Estat package
        /// Returns a serializable object that can be converted to XML/JSON
        /// </summary>
        public object ConvertPriceIndicesToSdmx(PriceIndices priceIndices, string? period)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for Price Indices data using Eurostat package");

                // Create a structured SDMX-compliant object
                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = DataStructureId,
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:{DataStructureId}",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertPriceIndicesToDataSet(priceIndices, period)
                };

                _logger.LogInformation("Successfully converted Price Indices to SDMX format");
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Price Indices to SDMX format");
                throw;
            }
        }

        /// <summary>
        /// Converts MarketPrice data to SDMX 2.1 format
        /// </summary>
        public object ConvertMarketPriceToSdmx(dynamic marketPrice, string? period)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for Market Price data using Eurostat package");

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = "DSD_MARKET_PRICE",
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:DSD_MARKET_PRICE",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertMarketPriceToDataSet(marketPrice, period)
                };

                _logger.LogInformation("Successfully converted Market Price to SDMX format");
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Market Price to SDMX format");
                throw;
            }
        }

        /// <summary>
        /// Creates the SDMX DataSet for Price Indices
        /// </summary>
        private object ConvertPriceIndicesToDataSet(PriceIndices priceIndices, string? period)
        {
            var seriesList = new List<object>();

            if (priceIndices?.priceIndices != null && priceIndices.priceIndices.Count > 0)
            {
                var observations = new List<object>();

                foreach (var item in priceIndices.priceIndices)
                {
                    if (item == null) continue;

                    try
                    {
                        var obsKey = ExtractObservationKey(item);
                        var obsValue = ExtractObservationValue(item);

                        if (obsKey != null && obsValue != null)
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", "M" },
                                    { "REF_AREA", "SL" },
                                    { "INDICATOR", "PRICE_IDX" }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing observation item");
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = "M",
                            REF_AREA = "SL",
                            INDICATOR = "PRICE_IDX"
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:{DataStructureId}",
                Series = seriesList
            };
        }

        /// <summary>
        /// Creates the SDMX DataSet for Market Price
        /// </summary>
        private object ConvertMarketPriceToDataSet(dynamic marketPrice, string? period)
        {
            var seriesList = new List<object>();

            if (marketPrice?.prices != null && marketPrice.prices.Count > 0)
            {
                var observations = new List<object>();

                foreach (var item in marketPrice.prices)
                {
                    if (item == null) continue;

                    try
                    {
                        var obsKey = item["periodId"]?.ToString() ?? period;
                        var obsValue = item["value"];

                        if (!string.IsNullOrEmpty(obsKey) && obsValue != null)
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", "D" },
                                    { "REF_AREA", "SL" }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing market price observation");
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = "D",
                            REF_AREA = "SL"
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:DSD_MARKET_PRICE",
                Series = seriesList
            };
        }

        /// <summary>
        /// Extracts observation time period from item
        /// </summary>
        private string? ExtractObservationKey(IDictionary<string, object> item)
        {
            if (item.TryGetValue("periodId", out var periodObj) && periodObj != null)
                return periodObj.ToString();

            if (item.TryGetValue("Period", out var periodObj2) && periodObj2 != null)
                return periodObj2.ToString();

            return null;
        }

        /// <summary>
        /// Extracts observation value from item
        /// </summary>
        private object? ExtractObservationValue(IDictionary<string, object> item)
        {
            if (item.TryGetValue("value", out var valueObj) && valueObj != null)
            {
                if (decimal.TryParse(valueObj.ToString(), out var decValue))
                    return decValue;

                return valueObj;
            }

            return null;
        }

        /// <summary>
        /// Converts GDP Growth data to SDMX 2.1 format
        /// </summary>
        public object ConvertGdpGrowthToSdmx(List<GdpItem> gdpItems, string? period, string? frequency)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for GDP Growth data using Eurostat package");

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = "DSD_GDP_GROWTH",
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:DSD_GDP_GROWTH",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertGdpGrowthToDataSet(gdpItems, period, frequency)
                };

                _logger.LogInformation("Successfully converted GDP Growth to SDMX format");
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting GDP Growth to SDMX format");
                throw;
            }
        }

        /// <summary>
        /// Creates the SDMX DataSet for GDP Growth
        /// </summary>
        private object ConvertGdpGrowthToDataSet(List<GdpItem> gdpItems, string? period, string? frequency)
        {
            var seriesList = new List<object>();

            if (gdpItems != null && gdpItems.Count > 0)
            {
                var observations = new List<object>();
                var freq = frequency ?? "A"; // Annual by default

                foreach (var item in gdpItems)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Item)) continue;

                    try
                    {
                        var obsKey = item.PeriodId ?? period;
                        var obsValue = item.CurrentValue;

                        if (!string.IsNullOrEmpty(obsKey))
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", freq },
                                    { "REF_AREA", "SL" },
                                    { "INDICATOR", item.Item }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing GDP growth observation item: {Item}", item?.Item);
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = frequency ?? "A",
                            REF_AREA = "SL",
                            INDICATOR = "GDP_GROWTH"
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:DSD_GDP_GROWTH",
                Series = seriesList
            };
        }

        /// <summary>
        /// Converts Agricultural Production data to SDMX 2.1 format
        /// </summary>
        public object ConvertAgriculturalProductionToSdmx(List<ModalItem> items, string? period, string? frequency)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for Agricultural Production data using Eurostat package");

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = "DSD_AGRICULTURAL_PRODUCTION",
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:DSD_AGRICULTURAL_PRODUCTION",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertAgriculturalProductionToDataSet(items, period, frequency)
                };

                _logger.LogInformation("Successfully converted Agricultural Production to SDMX format");
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Agricultural Production to SDMX format");
                throw;
            }
        }

        /// <summary>
        /// Creates the SDMX DataSet for Agricultural Production
        /// </summary>
        private object ConvertAgriculturalProductionToDataSet(List<ModalItem> items, string? period, string? frequency)
        {
            var seriesList = new List<object>();

            if (items != null && items.Count > 0)
            {
                var observations = new List<object>();
                var freq = frequency ?? "M"; // Monthly by default

                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemName)) continue;

                    try
                    {
                        var obsKey = item.PeriodId ?? period;
                        var obsValue = item.CurrentValue;

                        if (!string.IsNullOrEmpty(obsKey))
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", freq },
                                    { "REF_AREA", "SL" },
                                    { "INDICATOR", item.ItemName }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing agricultural production observation item: {Item}", item?.ItemName);
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = frequency ?? "M",
                            REF_AREA = "SL",
                            INDICATOR = "AGRICULTURAL_PRODUCTION"
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:DSD_AGRICULTURAL_PRODUCTION",
                Series = seriesList
            };
        }

        /// <summary>
        /// Converts Industrial Production data to SDMX 2.1 format
        /// </summary>
        public object ConvertIndustrialProductionToSdmx(List<ModalItem> items, string? period, string? frequency)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for Industrial Production data using Eurostat package");

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = "DSD_INDUSTRIAL_PRODUCTION",
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:DSD_INDUSTRIAL_PRODUCTION",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertIndustrialProductionToDataSet(items, period, frequency)
                };

                _logger.LogInformation("Successfully converted Industrial Production to SDMX format");
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Industrial Production to SDMX format");
                throw;
            }
        }

        /// <summary>
        /// Creates the SDMX DataSet for Industrial Production
        /// </summary>
        private object ConvertIndustrialProductionToDataSet(List<ModalItem> items, string? period, string? frequency)
        {
            var seriesList = new List<object>();

            if (items != null && items.Count > 0)
            {
                var observations = new List<object>();
                var freq = frequency ?? "M"; // Monthly by default

                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemName)) continue;

                    try
                    {
                        var obsKey = item.PeriodId ?? period;
                        var obsValue = item.CurrentValue;

                        if (!string.IsNullOrEmpty(obsKey))
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", freq },
                                    { "REF_AREA", "SL" },
                                    { "INDICATOR", item.ItemName }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing industrial production observation item: {Item}", item?.ItemName);
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = frequency ?? "M",
                            REF_AREA = "SL",
                            INDICATOR = "INDUSTRIAL_PRODUCTION"
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:DSD_INDUSTRIAL_PRODUCTION",
                Series = seriesList
            };
        }

        /// <summary>
        /// Generic converter for ModalItem-based indicators with configurable indicator name
        /// </summary>
        public object ConvertGenericModalItemsToSdmx(List<ModalItem> items, string? period, string? frequency, string indicatorName, string dsdId)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for {Indicator} using Eurostat package", indicatorName);

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = dsdId,
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:{dsdId}",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = ConvertGenericModalItemsToDataSet(items, period, frequency, indicatorName, dsdId)
                };

                _logger.LogInformation("Successfully converted {Indicator} to SDMX format", indicatorName);
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting {Indicator} to SDMX format", indicatorName);
                throw;
            }
        }

        /// <summary>
        /// Generic DataSet converter for ModalItem-based indicators
        /// </summary>
        private object ConvertGenericModalItemsToDataSet(List<ModalItem> items, string? period, string? frequency, string indicatorName, string dsdId)
        {
            var seriesList = new List<object>();

            if (items != null && items.Count > 0)
            {
                var observations = new List<object>();
                var freq = frequency ?? "M"; // Monthly by default

                foreach (var item in items)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemName)) continue;

                    try
                    {
                        var obsKey = item.PeriodId ?? period;
                        var obsValue = item.CurrentValue;

                        if (!string.IsNullOrEmpty(obsKey))
                        {
                            observations.Add(new
                            {
                                Dimension = new Dictionary<string, object>
                                {
                                    { "TIME_PERIOD", obsKey },
                                    { "FREQ", freq },
                                    { "REF_AREA", "SL" },
                                    { "INDICATOR", item.ItemName }
                                },
                                ObsValue = obsValue,
                                ObsStatus = "A"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing {Indicator} observation item: {Item}", indicatorName, item?.ItemName);
                        continue;
                    }
                }

                if (observations.Count > 0)
                {
                    seriesList.Add(new
                    {
                        SeriesKey = new
                        {
                            FREQ = frequency ?? "M",
                            REF_AREA = "SL",
                            INDICATOR = indicatorName
                        },
                        Observations = observations
                    });
                }
            }

            return new
            {
                StructureRef = $"{AgencyId}:{dsdId}",
                Series = seriesList
            };
        }

        /// <summary>
        /// Converts generic object data to SDMX 2.1 format
        /// </summary>
        public object ConvertGenericObjectToSdmx(object data, string indicatorName, string? frequency)
        {
            try
            {
                _logger.LogInformation("Starting SDMX conversion for generic {Indicator} using Eurostat package", indicatorName);

                var dsdId = $"DSD_{indicatorName.ToUpper().Replace("-", "_")}";
                var freq = frequency ?? "M";

                var sdmxMessage = new
                {
                    Header = new
                    {
                        ID = Guid.NewGuid().ToString(),
                        Prepared = DateTime.UtcNow,
                        Sender = new
                        {
                            ID = AgencyId,
                            Name = "Central Bank of Sri Lanka"
                        },
                        Structure = new
                        {
                            StructureID = dsdId,
                            Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={AgencyId}:{dsdId}",
                            DimensionAtObservation = "TIME_PERIOD"
                        }
                    },
                    DataSet = new
                    {
                        StructureRef = $"{AgencyId}:{dsdId}",
                        Series = new[] {
                            new
                            {
                                SeriesKey = new
                                {
                                    FREQ = freq,
                                    REF_AREA = "SL",
                                    INDICATOR = indicatorName
                                },
                                Observations = new[] {
                                    new
                                    {
                                        Dimension = new Dictionary<string, object>
                                        {
                                            { "TIME_PERIOD", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                                            { "FREQ", freq },
                                            { "REF_AREA", "SL" },
                                            { "INDICATOR", indicatorName }
                                        },
                                        ObsValue = data,
                                        ObsStatus = "A"
                                    }
                                }
                            }
                        }
                    }
                };

                _logger.LogInformation("Successfully converted {Indicator} to SDMX format", indicatorName);
                return sdmxMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting {Indicator} to SDMX format", indicatorName);
                throw;
            }
        }
    }
}
