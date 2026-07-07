using Microsoft.Extensions.Options;
using OpenAPI.Domain.Entities.Statistics;
using OpenAPI.Domain.Entities.Statistics.Sdmx;
using OpenAPI.Domain.Entities.Auth;
using System.Globalization;

namespace OpenAPI.Application.Services
{
    /// <summary>
    /// Service for converting data to SDMX format
    /// </summary>
    public class SdmxTransformationService
    {
        private readonly SdmxSettings _sdmxSettings;
        private const string SdmxMonthFormat = "yyyy-MM";
        private const string IndicatorKey = "INDICATOR";
        private const string TimePeriodKey = "TIME_PERIOD";
        private const string ObsStatusKey = "OBS_STATUS";
        private const string ReferenceAreaKey = "Reference Area";
        private const string RefAreaKey = "REF_AREA";
        private const string UnitMeasureKey = "UNIT_MEASURE";
        private const string DecimalsKey = "DECIMALS";
        private const string TitleKey = "TITLE";
        private const string FrequencyText = "Frequency";
        private const string MonthlyText = "Monthly";
        private const string SriLankaText = "Sri Lanka";
        private const string TimePeriodText = "Time Period";
        private const string ServicesText = "Services";

        public SdmxTransformationService(IOptions<SdmxSettings> sdmxSettings)
        {
            _sdmxSettings = sdmxSettings.Value;
        }
        /// <summary>
        /// Converts PriceIndices data to SDMX format
        /// </summary>
        /// <param name="priceIndices">The price indices data to convert</param>
        /// <param name="period">The period for which data is being requested</param>
        /// <returns>SDMX formatted data message</returns>
        public SdmxDataMessage ConvertPriceIndicesToSdmx(PriceIndices priceIndices, string? period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateHeader(),
                DataSet = CreateDataSet(priceIndices)
            };

            return dataMessage;
        }

        /// <summary>
        /// Creates the Data Structure Definition for Price Indices
        /// </summary>
        /// <returns>SDMX Data Structure Definition</returns>
        public SdmxDataStructure CreatePriceIndicesDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = _sdmxSettings.DataStructures.PriceIndices.Id,
                Version = _sdmxSettings.DataStructures.PriceIndices.Version,
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.PriceIndices.Name },
                Description = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.PriceIndices.Description },
                Dimensions = CreateDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private SdmxHeader CreateHeader()
        {
            return new SdmxHeader
            {
                Id = $"PRICE_INDICES_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
        }

        private SdmxDataSet CreateDataSet(PriceIndices priceIndices)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:{_sdmxSettings.DataStructures.PriceIndices.Id}",
                Series = new List<SdmxSeries>()
            };

            if (priceIndices.priceIndices != null && priceIndices.priceIndices.Any())
            {
                foreach (var item in priceIndices.priceIndices)
                {
                    var series = CreateSeriesFromPriceIndexDictionary(item);
                    dataSet.Series.Add(series);
                }
            }

            return dataSet;
        }

        private const string DescriptionKey = "description";
        private const string ProductKey = "PRODUCT";
        private const string RubberProductCode = "RUBBER";
        private const string IndexKey = "INDEX";

        private SdmxSeries CreateSeriesFromPriceIndexDictionary(Dictionary<string, object> data)
        {
            var indicator = data.TryGetValue("key", out var keyObj) ? (keyObj?.ToString() ?? "PRICE_INDEX") : "PRICE_INDEX";
            var series = CreatePriceIndexSeriesStructure(indicator);

            AddDescriptionAttributeIfAvailable(data, series);
            AddPriceIndexObservationIfAvailable(data, series);

            return series;
        }

        private SdmxSeries CreatePriceIndexSeriesStructure(string indicator)
        {
            return new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = _sdmxSettings.DataStructures.PriceIndices.WeeklyFrequency },
                        new() { Id = IndicatorKey, Value = indicator },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = _sdmxSettings.DataStructures.PriceIndices.UnitMeasureIndex },
                        new() { Id = DecimalsKey, Value = "2" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };
        }

        private static void AddDescriptionAttributeIfAvailable(Dictionary<string, object> data, SdmxSeries series)
        {
            if (data.ContainsKey(DescriptionKey) && data[DescriptionKey] != null)
            {
                series.Attributes.Values.Add(new SdmxAttributeValue 
                { 
                    Id = TitleKey, 
                    Value = data[DescriptionKey].ToString() ?? string.Empty 
                });
            }
        }

        private void AddPriceIndexObservationIfAvailable(Dictionary<string, object> data, SdmxSeries series)
        {
            if (!data.TryGetValue("periodId", out var periodObj) || !data.TryGetValue("value", out var valueObj))
                return;

            if (!decimal.TryParse(valueObj?.ToString(), out var value))
                return;

            var periodId = periodObj?.ToString();
            var timePeriodValue = NormalizePeriodToSdmxFormat(periodId);
            var observation = CreatePriceIndexObservation(timePeriodValue, value);

            series.Observations.Add(observation);
        }

        private static string? NormalizePeriodToSdmxFormat(string? periodId)
        {
            if (string.IsNullOrWhiteSpace(periodId))
                return periodId;

            var normalized = periodId.EndsWith("-00", StringComparison.Ordinal)
                ? periodId[..^3] + "-01"
                : periodId;

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate.ToString("yyyy-MMM", CultureInfo.InvariantCulture);
            }

            return periodId;
        }

        private SdmxObservation CreatePriceIndexObservation(string? timePeriodValue, decimal value)
        {
            return new SdmxObservation
            {
                ObsKey = new SdmxObsKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = TimePeriodKey, Value = timePeriodValue ?? string.Empty }
                    }
                },
                ObsValue = new SdmxObsValue { Value = value },
                Attributes = new SdmxObsAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = ObsStatusKey, Value = _sdmxSettings.DataStructures.PriceIndices.ObservationStatusAvailable }
                    }
                }
            };
        }

        private List<SdmxDimension> CreateDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = "FREQ",
                    Position = 1,
                    Name = new SdmxLocalizedText { Text = FrequencyText },
                    ConceptRef = "FREQ",
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.DataStructures.PriceIndices.WeeklyFrequency, Name = new SdmxLocalizedText { Text = "Weekly" } },
                        new() { Value = _sdmxSettings.DataStructures.PriceIndices.MonthlyFrequency, Name = new SdmxLocalizedText { Text = MonthlyText } }
                    }
                },
                new()
                {
                    Id = IndicatorKey,
                    Position = 2,
                    Name = new SdmxLocalizedText { Text = "Price Index Indicator" },
                    ConceptRef = IndicatorKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "NCPI", Name = new SdmxLocalizedText { Text = "National Consumer Price Index" } },
                        new() { Value = "CCPI", Name = new SdmxLocalizedText { Text = "Colombo Consumer Price Index" } }
                    }
                },
                new()
                {
                    Id = RefAreaKey,
                    Position = 3,
                    Name = new SdmxLocalizedText { Text = ReferenceAreaKey },
                    ConceptRef = RefAreaKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = SriLankaText } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = 4,
                    Name = new SdmxLocalizedText { Text = TimePeriodText },
                    ConceptRef = TimePeriodKey
                }
            };
        }

        private List<SdmxAttribute> CreateAttributes()
        {
            return new List<SdmxAttribute>
            {
                new()
                {
                    Id = UnitMeasureKey,
                    Name = new SdmxLocalizedText { Text = "Unit of Measure" },
                    AssignmentStatus = "Mandatory"
                },
                new()
                {
                    Id = DecimalsKey,
                    Name = new SdmxLocalizedText { Text = "Decimals" },
                    AssignmentStatus = "Optional"
                },
                new()
                {
                    Id = TitleKey,
                    Name = new SdmxLocalizedText { Text = "Series Title" },
                    AssignmentStatus = "Optional"
                },
                new()
                {
                    Id = ObsStatusKey,
                    Name = new SdmxLocalizedText { Text = "Observation Status" },
                    AssignmentStatus = "Optional"
                }
            };
        }

        /// <summary>
        /// Converts MarketPrice data to SDMX format
        /// </summary>
        /// <param name="marketPrice">The market price data to convert</param>
        /// <param name="period">The period for which data is being requested</param>
        /// <returns>SDMX formatted data message</returns>
        public SdmxDataMessage ConvertMarketPriceToSdmx(MarketPrice marketPrice, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreatePricesHeader(),
                DataSet = CreatePricesDataSet(marketPrice)
            };

            return dataMessage;
        }

        /// <summary>
        /// Creates the Data Structure Definition for Market Prices
        /// </summary>
        /// <returns>SDMX Data Structure Definition</returns>
        public SdmxDataStructure CreateMarketPricesDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = _sdmxSettings.DataStructures.MarketPrices.Id,
                Version = _sdmxSettings.DataStructures.MarketPrices.Version,
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Name },
                Description = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Description },
                Dimensions = CreatePricesDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private SdmxHeader CreatePricesHeader()
        {
            return new SdmxHeader
            {
                Id = $"{_sdmxSettings.DataStructures.MarketPrices.MessageIdPrefix}_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = _sdmxSettings.DataStructures.MarketPrices.Id,
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:{_sdmxSettings.DataStructures.MarketPrices.Id}",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreatePricesDataSet(MarketPrice marketPrice)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:{_sdmxSettings.DataStructures.MarketPrices.Id}",
                Series = new List<SdmxSeries>()
            };

            // Process prices from the new structure
            if (marketPrice.prices != null && marketPrice.prices.Any())
            {
                foreach (var priceEntry in marketPrice.prices)
                {
                    var series = CreateSeriesFromPriceEntry(priceEntry);
                    if (series != null)
                    {
                        dataSet.Series.Add(series);
                    }
                }
            }

            return dataSet;
        }

        private SdmxSeries? CreateSeriesFromPriceEntry(Dictionary<string, object> priceEntry)
        {
            if (!priceEntry.TryGetValue("key", out var keyObj) || keyObj == null) return null;
            if (!priceEntry.TryGetValue("periodId", out var periodObj) || periodObj == null) return null;
            if (!priceEntry.TryGetValue("value", out var valueObj) || valueObj == null) return null;

            var itemName = keyObj.ToString() ?? "";
            var periodId = periodObj.ToString() ?? "";
            
            if (!decimal.TryParse(valueObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return null;

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Frequency.Id, Value = _sdmxSettings.DataStructures.MarketPrices.Frequency },
                        new() { Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Item.Id, Value = CleanItemName(itemName) },
                        new() { Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.ReferenceArea.Id, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = _sdmxSettings.DataStructures.MarketPrices.Currency },
                        new() { Id = DecimalsKey, Value = "2" },
                        new() { Id = TitleKey, Value = itemName }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add single observation for current period
            if (DateTime.TryParse(periodId, CultureInfo.InvariantCulture, DateTimeStyles.None, out var periodDate))
            {
                series.Observations.Add(CreateObservation(periodDate, value));
            }

            return series;
        }

        private SdmxObservation CreateObservation(DateTime date, decimal value)
        {
            return new SdmxObservation
            {
                ObsKey = new SdmxObsKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.TimePeriod.Id, Value = date.ToString("yyyy-MM-dd") }
                    }
                },
                ObsValue = new SdmxObsValue { Value = value },
                Attributes = new SdmxObsAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = ObsStatusKey, Value = _sdmxSettings.DataStructures.MarketPrices.ObservationStatusAvailable }
                    }
                }
            };
        }

        private List<SdmxDimension> CreatePricesDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Frequency.Id,
                    Position = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Frequency.Position,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Frequency.Name },
                    ConceptRef = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Frequency.ConceptRef,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.DataStructures.MarketPrices.Frequency, Name = new SdmxLocalizedText { Text = "Daily" } }
                    }
                },
                new()
                {
                    Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.MarketType.Id,
                    Position = _sdmxSettings.DataStructures.MarketPrices.Dimensions.MarketType.Position,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Dimensions.MarketType.Name },
                    ConceptRef = _sdmxSettings.DataStructures.MarketPrices.Dimensions.MarketType.ConceptRef,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.DataStructures.MarketPrices.MarketTypes.Wholesale, Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.MarketTypes.WholesaleName } },
                        new() { Value = _sdmxSettings.DataStructures.MarketPrices.MarketTypes.Retail, Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.MarketTypes.RetailName } }
                    }
                },
                new()
                {
                    Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Item.Id,
                    Position = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Item.Position,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Item.Name },
                    ConceptRef = _sdmxSettings.DataStructures.MarketPrices.Dimensions.Item.ConceptRef
                },
                new()
                {
                    Id = _sdmxSettings.DataStructures.MarketPrices.Dimensions.ReferenceArea.Id,
                    Position = _sdmxSettings.DataStructures.MarketPrices.Dimensions.ReferenceArea.Position,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Dimensions.ReferenceArea.Name },
                    ConceptRef = _sdmxSettings.DataStructures.MarketPrices.Dimensions.ReferenceArea.ConceptRef,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.ReferenceAreaName } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = _sdmxSettings.DataStructures.MarketPrices.Dimensions.TimePeriod.Position,
                    Name = new SdmxLocalizedText { Text = _sdmxSettings.DataStructures.MarketPrices.Dimensions.TimePeriod.Name },
                    ConceptRef = TimePeriodKey
                }
            };
        }

     

        /// <summary>
        /// Create data structure definition for GDP Growth data
        /// </summary>
        public SdmxDataStructure CreateGdpGrowthDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = "DSD_GDP_GROWTH",
                Version = "1.0",
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = "GDP Growth Data Structure Definition" },
                Description = new SdmxLocalizedText { Text = "Data structure for Sri Lankan GDP growth by industrial origin at constant 2015 prices" },
                Dimensions = CreateGdpGrowthDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private List<SdmxDimension> CreateGdpGrowthDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = "FREQ",
                    Position = 1,
                    Name = new SdmxLocalizedText { Text = FrequencyText },
                    ConceptRef = "FREQ",
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "A", Name = new SdmxLocalizedText { Text = "Annual" } },
                        new() { Value = "Q", Name = new SdmxLocalizedText { Text = "Quarterly" } }
                    }
                },
                new()
                {
                    Id = IndicatorKey,
                    Position = 2,
                    Name = new SdmxLocalizedText { Text = "GDP Component" },
                    ConceptRef = IndicatorKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "GDP_AGRI", Name = new SdmxLocalizedText { Text = "Agriculture, Forestry and Fishing" } },
                        new() { Value = "GDP_IND", Name = new SdmxLocalizedText { Text = "Industries" } },
                        new() { Value = "GDP_SERV", Name = new SdmxLocalizedText { Text = ServicesText } },
                        new() { Value = "GDP_TAX", Name = new SdmxLocalizedText { Text = "Taxes less Subsidies on products" } },
                        new() { Value = "GDP_TOTAL", Name = new SdmxLocalizedText { Text = "Gross Domestic Product" } }
                    }
                },
                new()
                {
                    Id = RefAreaKey,
                    Position = 3,
                    Name = new SdmxLocalizedText { Text = ReferenceAreaKey },
                    ConceptRef = RefAreaKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = SriLankaText } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = 4,
                    Name = new SdmxLocalizedText { Text = TimePeriodText },
                    ConceptRef = TimePeriodKey
                }
            };
        }
        private static string CleanItemName(string itemName)
        {
            // Remove special characters and replace spaces with underscores for SDMX compatibility
            return itemName.Replace(" ", "_")
                          .Replace("(", "")
                          .Replace(")", "")
                          .Replace("-", "_")
                          .ToUpperInvariant();
        }

        /// <summary>
        /// Converts Agricultural Production data to SDMX format
        /// </summary>
        /// <param name="agriculturalProduction">The agricultural production data to convert</param>
        /// <param name="period">The period for which data is being requested</param>
        /// <returns>SDMX formatted data message</returns>
        public SdmxDataMessage ConvertAgriculturalProductionToSdmx(AgriculturalProduction agriculturalProduction, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateAgriProductionHeader(),
                DataSet = CreateAgriProductionDataSet(agriculturalProduction, period)
            };

            return dataMessage;
        }

        /// <summary>
        /// Create data structure definition for Agricultural Production data
        /// </summary>
        public SdmxDataStructure CreateAgriculturalProductionDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = "DSD_AGRI_PROD",
                Version = "1.0",
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = "Agricultural Production Data Structure Definition" },
                Description = new SdmxLocalizedText { Text = "Data structure for Sri Lankan agricultural production statistics" },
                Dimensions = CreateAgriProductionDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private SdmxHeader CreateAgriProductionHeader()
        {
            return new SdmxHeader
            {
                Id = $"AGRI_PROD_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_AGRI_PROD",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_AGRI_PROD",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreateAgriProductionDataSet(AgriculturalProduction agriculturalProduction, string period)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_AGRI_PROD",
                Series = new List<SdmxSeries>()
            };

            // Add agricultural production series
            //if (agriculturalProduction.AgriProduction != null && agriculturalProduction.AgriProduction.Any())
            //{
            //    foreach (var item in agriculturalProduction.AgriProduction)
            //    {
            //        var series = CreateAgriProductionSeries(item, period);
            //        if (series != null)
            //            dataSet.Series.Add(series);
            //    }
            //}

            return dataSet;
        }

        private SdmxSeries? CreateAgriProductionSeries(ProductionItem agriItem, string period)
        {
            if (!agriItem.CurrentYear.HasValue && !agriItem.PreviousYear.HasValue)
                return null;

            var productCode = MapAgriProduct(agriItem.ProductName ?? "");

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "M" }, // Monthly frequency
                        new() { Id = ProductKey, Value = productCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = GetUnitMeasure(agriItem.ProductName ?? "") },
                        new() { Id = DecimalsKey, Value = "1" },
                        new() { Id = TitleKey, Value = agriItem.ProductName ?? "" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Parse period (yyyy-MM format)
            if (DateTime.TryParseExact(period, SdmxMonthFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var currentDate))
            {
                // Add current year observation
                if (agriItem.CurrentYear.HasValue)
                {
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = currentDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = agriItem.CurrentYear.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add previous year observation
                if (agriItem.PreviousYear.HasValue)
                {
                    var previousYearDate = currentDate.AddYears(-1);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = previousYearDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = agriItem.PreviousYear.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add percentage change as a separate observation attribute if available
                if (agriItem.PercentageChange.HasValue)
                {
                    series.Attributes.Values.Add(new SdmxAttributeValue
                    {
                        Id = "YOY_GROWTH",
                        Value = agriItem.PercentageChange.Value.ToString("F1")
                    });
                }
            }

            return series;
        }

        private List<SdmxDimension> CreateAgriProductionDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = "FREQ",
                    Position = 1,
                    Name = new SdmxLocalizedText { Text = FrequencyText },
                    ConceptRef = "FREQ",
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "M", Name = new SdmxLocalizedText { Text = MonthlyText } }
                    }
                },
                new()
                {
                    Id = ProductKey,
                    Position = 2,
                    Name = new SdmxLocalizedText { Text = "Agricultural Product" },
                    ConceptRef = ProductKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "TEA", Name = new SdmxLocalizedText { Text = "Tea" } },
                        new() { Value = RubberProductCode, Name = new SdmxLocalizedText { Text = "Rubber" } },
                        new() { Value = "COCONUT", Name = new SdmxLocalizedText { Text = "Coconut" } }
                    }
                },
                new()
                {
                    Id = RefAreaKey,
                    Position = 3,
                    Name = new SdmxLocalizedText { Text = ReferenceAreaKey },
                    ConceptRef = RefAreaKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = SriLankaText } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = 4,
                    Name = new SdmxLocalizedText { Text = TimePeriodText },
                    ConceptRef = TimePeriodKey
                }
            };
        }

        private static string MapAgriProduct(string product)
        {
            return product switch
            {
                var s when s.Contains("Tea") => "TEA",
                var s when s.Contains("Rubber") => RubberProductCode,
                var s when s.Contains("Coconut") => "COCONUT",
                _ => CleanItemName(product)
            };
        }

        private static string GetUnitMeasure(string product)
        {
            return product switch
            {
                var s when s.Contains("Tea") || s.Contains("Rubber") => "MM_KG", // Million kg
                var s when s.Contains("Coconut") => "MM_NUTS", // Million nuts
                _ => "UNITS"
            };
        }

        /// <summary>
        /// Converts Industrial Production data to SDMX format
        /// </summary>
        /// <param name="industrialProduction">The industrial production data to convert</param>
        /// <param name="period">The period for which data is being requested</param>
        /// <returns>SDMX formatted data message</returns>
        public SdmxDataMessage ConvertIndustrialProductionToSdmx(IndustrialProduction industrialProduction, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateIndustrialProductionHeader(),
                DataSet = CreateIndustrialProductionDataSet(industrialProduction, period)
            };

            return dataMessage;
        }

        /// <summary>
        /// Create data structure definition for Industrial Production data
        /// </summary>
        public SdmxDataStructure CreateIndustrialProductionDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = "DSD_IND_PROD",
                Version = "1.0",
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = "Industrial Production Index Data Structure Definition" },
                Description = new SdmxLocalizedText { Text = "Data structure for Sri Lankan Industrial Production Index statistics" },
                Dimensions = CreateIndustrialProductionDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private SdmxHeader CreateIndustrialProductionHeader()
        {
            return new SdmxHeader
            {
                Id = $"IND_PROD_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_IND_PROD",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_IND_PROD",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreateIndustrialProductionDataSet(IndustrialProduction industrialProduction, string period)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_IND_PROD",
                Series = new List<SdmxSeries>()
            };

            // Add industrial production series
            if (industrialProduction.IndustrialProductionIndex != null && industrialProduction.IndustrialProductionIndex.Any())
            {
                foreach (var item in industrialProduction.IndustrialProductionIndex)
                {
                    var series = CreateIndustrialProductionSeries(item, period);
                    if (series != null)
                        dataSet.Series.Add(series);
                }
            }

            return dataSet;
        }

        private SdmxSeries? CreateIndustrialProductionSeries(ProductionItem indItem, string period)
        {
            if (!indItem.CurrentYear.HasValue && !indItem.PreviousYear.HasValue)
                return null;

            var productCode = MapIndustrialProduct(indItem.ProductName ?? "");

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "M" }, // Monthly frequency
                        new() { Id = "PRODUCT", Value = productCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = IndexKey }, // Index measure
                        new() { Id = DecimalsKey, Value = "1" },
                        new() { Id = TitleKey, Value = indItem.ProductName ?? "" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Parse period (yyyy-MM format)
            if (DateTime.TryParseExact(period, SdmxMonthFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var currentDate))
            {
                // Add current year observation
                if (indItem.CurrentYear.HasValue)
                {
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = currentDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = indItem.CurrentYear.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add previous year observation
                if (indItem.PreviousYear.HasValue)
                {
                    var previousYearDate = currentDate.AddYears(-1);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = previousYearDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = indItem.PreviousYear.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add percentage change as a separate observation attribute if available
                if (indItem.PercentageChange.HasValue)
                {
                    series.Attributes.Values.Add(new SdmxAttributeValue
                    {
                        Id = "YOY_GROWTH",
                        Value = indItem.PercentageChange.Value.ToString("F1")
                    });
                }
            }

            return series;
        }

        private List<SdmxDimension> CreateIndustrialProductionDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = "FREQ",
                    Position = 1,
                    Name = new SdmxLocalizedText { Text = FrequencyText },
                    ConceptRef = "FREQ",
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "M", Name = new SdmxLocalizedText { Text = "Monthly" } }
                    }
                },
                new()
                {
                    Id = ProductKey,
                    Position = 2,
                    Name = new SdmxLocalizedText { Text = "Industrial Product" },
                    ConceptRef = ProductKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "IIP", Name = new SdmxLocalizedText { Text = "Index of Industrial Production" } },
                        new() { Value = "FOOD", Name = new SdmxLocalizedText { Text = "Food products" } },
                        new() { Value = "APPAREL", Name = new SdmxLocalizedText { Text = "Wearing apparel" } },
                        new() { Value = "MINERAL", Name = new SdmxLocalizedText { Text = "Other non-metallic mineral products" } },
                        new() { Value = "PETROLEUM", Name = new SdmxLocalizedText { Text = "Coke and refined petroleum products" } },
                        new() { Value = RubberProductCode, Name = new SdmxLocalizedText { Text = "Rubber and plastic products" } },
                        new() { Value = "CHEMICAL", Name = new SdmxLocalizedText { Text = "Chemicals and chemical products" } },
                        new() { Value = "BEVERAGE", Name = new SdmxLocalizedText { Text = "Beverages" } }
                    }
                },
                new()
                {
                    Id = RefAreaKey,
                    Position = 3,
                    Name = new SdmxLocalizedText { Text = ReferenceAreaKey },
                    ConceptRef = RefAreaKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = SriLankaText } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = 4,
                    Name = new SdmxLocalizedText { Text = TimePeriodText },
                    ConceptRef = TimePeriodKey
                }
            };
        }

        private static string MapIndustrialProduct(string product)
        {
            return product switch
            {
                var s when s.Contains("Index of Industrial Production") => "IIP",
                var s when s.Contains("Food products") => "FOOD",
                var s when s.Contains("Wearing apparel") => "APPAREL",
                var s when s.Contains("Other non-metallic mineral products") => "MINERAL",
                var s when s.Contains("Coke and refined petroleum products") => "PETROLEUM",
                var s when s.Contains("Rubber and plastic products") => RubberProductCode,
                var s when s.Contains("Chemicals and chemical products") => "CHEMICAL",
                var s when s.Contains("Beverages") => "BEVERAGE",
                _ => CleanItemName(product)
            };
        }

        /// <summary>
        /// Converts Pmidata to SDMX format
        /// </summary>
        /// <param name="pmi">The Pmidata to convert</param>
        /// <param name="period">The period for which data is being requested</param>
        /// <returns>SDMX formatted data message</returns>
        public SdmxDataMessage ConvertPMIToSdmx(Pmi pmi, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreatePMIHeader(),
                DataSet = CreatePMIDataSet(pmi, period)
            };

            return dataMessage;
        }

        /// <summary>
        /// Create data structure definition for Pmidata
        /// </summary>
        public SdmxDataStructure CreatePMIDataStructure()
        {
            return new SdmxDataStructure
            {
                Id = "DSD_PMI",
                Version = "1.0",
                AgencyId = _sdmxSettings.Agency.Id,
                Name = new SdmxLocalizedText { Text = "Purchasing Managers' Index Data Structure Definition" },
                Description = new SdmxLocalizedText { Text = "Data structure for Sri Lankan Purchasing Managers' Index (PMI) statistics" },
                Dimensions = CreatePMIDimensions(),
                Attributes = CreateAttributes()
            };
        }

        private SdmxHeader CreatePMIHeader()
        {
            return new SdmxHeader
            {
                Id = $"PMI_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_PMI",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_PMI",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreatePMIDataSet(Pmi pmi, string period)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_PMI",
                Series = new List<SdmxSeries>()
            };

            AddPmiSeriesToDataSet(pmi.PMIManufacturing, period, "PMI_MFG", dataSet);
            AddPmiSeriesToDataSet(pmi.PMIServices, period, "PMI_SVC", dataSet);
            AddPmiSeriesToDataSet(pmi.PMIConstruction, period, "PMI_CON", dataSet);

            return dataSet;
        }

        private void AddPmiSeriesToDataSet(List<PmiItem>? pmiItems, string period, string indexCode, SdmxDataSet dataSet)
        {
            if (pmiItems == null || !pmiItems.Any())
                return;

            foreach (var item in pmiItems)
            {
                var series = CreatePMISeries(item, period, indexCode);
                if (series != null)
                    dataSet.Series.Add(series);
            }
        }

        private SdmxSeries? CreatePMISeries(PmiItem PmiItem, string period, string indexCode)
        {
            if (!PmiItem.CurrentMonth.HasValue && !PmiItem.PreviousMonth.HasValue && !PmiItem.YearAgo.HasValue 
                && !PmiItem.CurrentYearDoubleMonthAgo.HasValue && !PmiItem.YearBackAndMonthAgo.HasValue 
                && !PmiItem.YearBackAndDoubleMonthAgo.HasValue)
                return null;

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "M" }, // Monthly frequency
                        new() { Id = IndexKey, Value = indexCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = "INDEX" }, // Index measure
                        new() { Id = DecimalsKey, Value = "1" },
                        new() { Id = TitleKey, Value = PmiItem.IndexName ?? "" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Parse period (yyyy-MM format)
            if (DateTime.TryParseExact(period, SdmxMonthFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var currentDate))
            {
                // Add current month observation
                if (PmiItem.CurrentMonth.HasValue)
                {
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = currentDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.CurrentMonth.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add previous month observation (1 month ago)
                if (PmiItem.PreviousMonth.HasValue)
                {
                    var previousMonthDate = currentDate.AddMonths(-1);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = previousMonthDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.PreviousMonth.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add double month ago observation (2 months ago)
                if (PmiItem.CurrentYearDoubleMonthAgo.HasValue)
                {
                    var doubleMonthAgoDate = currentDate.AddMonths(-2);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = doubleMonthAgoDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.CurrentYearDoubleMonthAgo.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add year ago observation (12 months ago)
                if (PmiItem.YearAgo.HasValue)
                {
                    var yearAgoDate = currentDate.AddYears(-1);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = yearAgoDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.YearAgo.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add year back and month ago observation (13 months ago)
                if (PmiItem.YearBackAndMonthAgo.HasValue)
                {
                    var yearBackMonthAgoDate = currentDate.AddMonths(-13);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = yearBackMonthAgoDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.YearBackAndMonthAgo.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }

                // Add year back and double month ago observation (14 months ago)
                if (PmiItem.YearBackAndDoubleMonthAgo.HasValue)
                {
                    var yearBackDoubleMonthAgoDate = currentDate.AddMonths(-14);
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = yearBackDoubleMonthAgoDate.ToString(SdmxMonthFormat) }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = PmiItem.YearBackAndDoubleMonthAgo.Value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }
            }

            return series;
        }

        private List<SdmxDimension> CreatePMIDimensions()
        {
            return new List<SdmxDimension>
            {
                new()
                {
                    Id = "FREQ",
                    Position = 1,
                    Name = new SdmxLocalizedText { Text = FrequencyText },
                    ConceptRef = "FREQ",
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "M", Name = new SdmxLocalizedText { Text = "Monthly" } }
                    }
                },
                new()
                {
                    Id = IndexKey,
                    Position = 2,
                    Name = new SdmxLocalizedText { Text = "PmiIndex Type" },
                    ConceptRef = IndexKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = "PMI_MFG", Name = new SdmxLocalizedText { Text = "PmiManufacturing" } },
                        new() { Value = "PMI_SVC", Name = new SdmxLocalizedText { Text = "PmiServices" } },
                        new() { Value = "PMI_CON", Name = new SdmxLocalizedText { Text = "PmiConstruction" } }
                    }
                },
                new()
                {
                    Id = RefAreaKey,
                    Position = 3,
                    Name = new SdmxLocalizedText { Text = ReferenceAreaKey },
                    ConceptRef = RefAreaKey,
                    CodeList = new List<SdmxCode>
                    {
                        new() { Value = _sdmxSettings.Common.ReferenceArea, Name = new SdmxLocalizedText { Text = SriLankaText } }
                    }
                },
                new()
                {
                    Id = TimePeriodKey,
                    Position = 4,
                    Name = new SdmxLocalizedText { Text = TimePeriodText },
                    ConceptRef = TimePeriodKey
                }
            };
        }

        // Employment Data SDMX Transformation
        public SdmxDataMessage ConvertEmploymentToSdmx(Employment employment, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateEmploymentHeader(),
                DataSet = CreateEmploymentDataSet(employment)
            };

            return dataMessage;
        }

        private SdmxHeader CreateEmploymentHeader()
        {
            return new SdmxHeader
            {
                Id = $"EMPLOYMENT_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_EMPLOYMENT",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_EMPLOYMENT",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreateEmploymentDataSet(Employment employment)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_EMPLOYMENT",
                Series = new List<SdmxSeries>()
            };

            // Add employment indicators series (Labour Force Participation Rate, Unemployment Rate)
            if (employment.EmploymentData != null && employment.EmploymentData.Any())
            {
                foreach (var item in employment.EmploymentData)
                {
                    var series = CreateEmploymentSeries(item);
                    if (series != null)
                        dataSet.Series.Add(series);
                }
            }

            // Add employed person by sector series (Agriculture, Industry, Services)
            if (employment.EmpolyedPersonBySectorData != null && employment.EmpolyedPersonBySectorData.Any())
            {
                foreach (var item in employment.EmpolyedPersonBySectorData)
                {
                    var series = CreateEmployedPersonBySectorSeries(item);
                    if (series != null)
                        dataSet.Series.Add(series);
                }
            }

            return dataSet;
        }

        private SdmxSeries? CreateEmploymentSeries(EmploymentItem employmentItem)
        {
            if (!employmentItem.YearAgoValue.HasValue && !employmentItem.YearAgoWithQuarter.HasValue && !employmentItem.ThisYearWithQuarter.HasValue)
                return null;

            var indicatorCode = MapEmploymentIndicator(employmentItem.Item ?? "");

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "Q" }, // Quarterly
                        new() { Id = IndicatorKey, Value = indicatorCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = "PERCENT" },
                        new() { Id = DecimalsKey, Value = "1" },
                        new() { Id = TitleKey, Value = employmentItem.Item ?? "" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add year-ago annual observation
            if (employmentItem.YearAgoValue.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "YEAR_AGO_ANNUAL" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = employmentItem.YearAgoValue.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            // Add year-ago quarterly observation
            if (employmentItem.YearAgoWithQuarter.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "YEAR_AGO_QUARTER" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = employmentItem.YearAgoWithQuarter.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            // Add current year quarterly observation
            if (employmentItem.ThisYearWithQuarter.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "THIS_YEAR_QUARTER" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = employmentItem.ThisYearWithQuarter.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            return series;
        }

        private static string MapEmploymentIndicator(string item)
        {
            return item switch
            {
                var s when s.Contains("Labour Force Participation Rate") => "LFPR",
                var s when s.Contains("Unemployment Rate") => "UNEMP_RATE",
                _ => CleanItemName(item)
            };
        }

        private SdmxSeries? CreateEmployedPersonBySectorSeries(EmployedPersonBySectorItem sectorItem)
        {
            if (!sectorItem.YearAgoValue.HasValue && !sectorItem.YearAgoWithQuarter.HasValue && !sectorItem.ThisYearWithQuarter.HasValue)
                return null;

            var sectorCode = MapSectorCode(sectorItem.Item ?? "");

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "Q" }, // Quarterly
                        new() { Id = "SECTOR", Value = sectorCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = "PERCENT" },
                        new() { Id = DecimalsKey, Value = "1" },
                        new() { Id = TitleKey, Value = $"Employment in {sectorItem.Item}" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add year-ago annual observation
            if (sectorItem.YearAgoValue.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "YEAR_AGO_ANNUAL" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = sectorItem.YearAgoValue.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            // Add year-ago quarterly observation
            if (sectorItem.YearAgoWithQuarter.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "YEAR_AGO_QUARTER" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = sectorItem.YearAgoWithQuarter.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            // Add current year quarterly observation
            if (sectorItem.ThisYearWithQuarter.HasValue)
            {
                series.Observations.Add(new SdmxObservation
                {
                    ObsKey = new SdmxObsKey
                    {
                        Values = new List<SdmxKeyValue>
                        {
                            new() { Id = TimePeriodKey, Value = "THIS_YEAR_QUARTER" }
                        }
                    },
                    ObsValue = new SdmxObsValue { Value = sectorItem.ThisYearWithQuarter.Value },
                    Attributes = new SdmxObsAttributes
                    {
                        Values = new List<SdmxAttributeValue>
                        {
                            new() { Id = ObsStatusKey, Value = "A" }
                        }
                    }
                });
            }

            return series;
        }

        private static string MapSectorCode(string sector)
        {
            return sector switch
            {
                "Agriculture" => "AGRI",
                "Industry" => "IND",
                ServicesText => "SVC",
                _ => CleanItemName(sector)
            };
        }

        // Crude Oil Prices SDMX Transformation
        public SdmxDataMessage ConvertCrudeOilPricesToSdmx(CrudeOilPrice crudeOilPrice, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateCrudeOilPricesHeader(),
                DataSet = CreateCrudeOilPricesDataSet(crudeOilPrice)
            };

            return dataMessage;
        }

        private SdmxHeader CreateCrudeOilPricesHeader()
        {
            return new SdmxHeader
            {
                Id = $"CRUDE_OIL_PRICES_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_CRUDE_OIL_PRICES",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_CRUDE_OIL_PRICES",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreateCrudeOilPricesDataSet(CrudeOilPrice crudeOilPrice)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_CRUDE_OIL_PRICES",
                Series = new List<SdmxSeries>()
            };

            // Add monthly data series
            if (crudeOilPrice.MonthlyPrices != null && crudeOilPrice.MonthlyPrices.Any())
            {
                // Create three separate series for Brent, WTI, and CPC Import
                CreateCrudeOilMonthlyCommoditySeries(crudeOilPrice.MonthlyPrices, "BRENT", 
                    item => item.CrudeOilFuturesPricesBrent, "Crude Oil Futures Prices - Brent (Monthly)", dataSet);
                
                CreateCrudeOilMonthlyCommoditySeries(crudeOilPrice.MonthlyPrices, "WTI", 
                    item => item.CrudeOilFuturesPricesWTI, "Crude Oil Futures Prices - WTI (Monthly)", dataSet);
                
                CreateCrudeOilMonthlyCommoditySeries(crudeOilPrice.MonthlyPrices, "CPC_IMPORT", 
                    item => item.CPCImportPrices, "CPC Import Prices (Monthly)", dataSet);
            }

            // Add daily data series
            if (crudeOilPrice.DailyPrices != null && crudeOilPrice.DailyPrices.Any())
            {
                CreateCrudeOilDailyCommoditySeries(crudeOilPrice.DailyPrices, "BRENT", 
                    item => item.CrudeOilFuturesPricesBrent, "Crude Oil Futures Prices - Brent (Daily)", dataSet);
                
                CreateCrudeOilDailyCommoditySeries(crudeOilPrice.DailyPrices, "WTI", 
                    item => item.CrudeOilFuturesPricesWTI, "Crude Oil Futures Prices - WTI (Daily)", dataSet);
            }

            return dataSet;
        }

        private void CreateCrudeOilMonthlyCommoditySeries(List<CrudeOilPriceMonthlyItem> prices, string commodityCode, 
            Func<CrudeOilPriceMonthlyItem, string?> valueSelector, string title, SdmxDataSet dataSet)
        {
            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "M" }, // Monthly
                        new() { Id = "COMMODITY", Value = commodityCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = "USD_BBL" },
                        new() { Id = DecimalsKey, Value = "2" },
                        new() { Id = TitleKey, Value = title }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add observations for each month/year combination
            foreach (var item in prices)
            {
                var valueStr = valueSelector(item);
                if (!string.IsNullOrEmpty(valueStr) && valueStr != "-" && decimal.TryParse(valueStr, out var value))
                {
                    // Convert month name to number (01-12)
                    var monthNum = GetMonthNumber(item.MonthName ?? "");
                    var timePeriod = $"{item.Year}-{monthNum:D2}";

                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = timePeriod }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }
            }

            if (series.Observations.Any())
                dataSet.Series.Add(series);
        }

        private void CreateCrudeOilDailyCommoditySeries(List<CrudeOilPriceDailyItem> prices, string commodityCode, 
            Func<CrudeOilPriceDailyItem, string?> valueSelector, string title, SdmxDataSet dataSet)
        {
            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "D" }, // Daily
                        new() { Id = "COMMODITY", Value = commodityCode },
                        new() { Id = RefAreaKey, Value = _sdmxSettings.Common.ReferenceArea }
                    }
                },
                Attributes = new SdmxSeriesAttributes
                {
                    Values = new List<SdmxAttributeValue>
                    {
                        new() { Id = UnitMeasureKey, Value = "USD_BBL" },
                        new() { Id = "DECIMALS", Value = "2" },
                        new() { Id = "TITLE", Value = title }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add observations for each day/year combination
            foreach (var item in prices)
            {
                var valueStr = valueSelector(item);
                if (!string.IsNullOrEmpty(valueStr) && valueStr != "-" && decimal.TryParse(valueStr, out var value) && item.Year.HasValue)
                {
                    // Use DayLabel as the time period identifier (e.g., "14-Nov")
                    var timePeriod = $"{item.Year}-{item.DayLabel}";

                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = timePeriod }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }
            }

            if (series.Observations.Any())
                dataSet.Series.Add(series);
        }

        private static int GetMonthNumber(string monthName)
        {
            return monthName switch
            {
                "January" => 1,
                "February" => 2,
                "March" => 3,
                "April" => 4,
                "May" => 5,
                "June" => 6,
                "July" => 7,
                "August" => 8,
                "September" => 9,
                "October" => 10,
                "November" => 11,
                "December" => 12,
                _ => 1
            };
        }

        // Daily Electricity Generation SDMX Conversion
        public SdmxDataMessage ConvertDailyElectricityGenerationToSdmx(DailyElectricityGeneration electricityData, string period)
        {
            var dataMessage = new SdmxDataMessage
            {
                Header = CreateDailyElectricityGenerationHeader(),
                DataSet = CreateDailyElectricityGenerationDataSet(electricityData)
            };

            return dataMessage;
        }

        private SdmxHeader CreateDailyElectricityGenerationHeader()
        {
            return new SdmxHeader
            {
                Id = $"DAILY_ELECTRICITY_GENERATION_{DateTime.UtcNow:yyyyMMddHHmmss}",
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
                    StructureId = "DSD_DAILY_ELECTRICITY_GENERATION",
                    Namespace = $"urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure={_sdmxSettings.Agency.Id}:DSD_DAILY_ELECTRICITY_GENERATION",
                    DimensionAtObservation = _sdmxSettings.Common.DimensionAtObservation
                }
            };
        }

        private SdmxDataSet CreateDailyElectricityGenerationDataSet(DailyElectricityGeneration electricityData)
        {
            var dataSet = new SdmxDataSet
            {
                StructureRef = $"{_sdmxSettings.Agency.Id}:DSD_DAILY_ELECTRICITY_GENERATION",
                Series = new List<SdmxSeries>()
            };

            // Create separate series for each generation type
            CreateElectricityGenerationSeries(electricityData.PeakDemandMW, 
                "Peak Demand (MW)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.TotalEnergyGWh,  
                "Total Energy (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.HydroGWh,
                "Hydro Generation (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.ThermalCoalGWh,  
                "Thermal Coal Generation (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.ThermalOilGWh,  
                "Thermal Oil Generation (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.WindGWh, 
                "Wind Generation (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.SolarGWh, 
                "Solar Generation (GWh)", dataSet);
            
            CreateElectricityGenerationSeries(electricityData.BiomassGWh, 
                "Biomass Generation (GWh)", dataSet);

            return dataSet;
        }

        private void CreateElectricityGenerationSeries(
            Dictionary<string, string?> data,
            string generationType,
                        SdmxDataSet dataSet)
        {
            if (data == null || !data.Any())
                return;

            var series = new SdmxSeries
            {
                SeriesKey = new SdmxSeriesKey
                {
                    Values = new List<SdmxKeyValue>
                    {
                        new() { Id = "FREQ", Value = "D" },
                        new() { Id = IndicatorKey, Value = "ELECTRICITY_GENERATION" },
                        new() { Id = "GENERATION_TYPE", Value = generationType },
                        new() { Id = UnitMeasureKey, Value = generationType == "PEAK_DEMAND" ? "MW" : "GWH" }
                    }
                },
                Observations = new List<SdmxObservation>()
            };

            // Add observations for each day
            foreach (var kvp in data)
            {
                var valueStr = kvp.Value;
                if (!string.IsNullOrEmpty(valueStr) && valueStr != "-" && decimal.TryParse(valueStr, out var value))
                {
                    series.Observations.Add(new SdmxObservation
                    {
                        ObsKey = new SdmxObsKey
                        {
                            Values = new List<SdmxKeyValue>
                            {
                                new() { Id = TimePeriodKey, Value = kvp.Key }
                            }
                        },
                        ObsValue = new SdmxObsValue { Value = value },
                        Attributes = new SdmxObsAttributes
                        {
                            Values = new List<SdmxAttributeValue>
                            {
                                new() { Id = ObsStatusKey, Value = "A" }
                            }
                        }
                    });
                }
            }

            if (series.Observations.Any())
                dataSet.Series.Add(series);
        }
    }
}