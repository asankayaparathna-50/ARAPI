using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenAPI.Domain.Entities.Statistics.Sdmx
{
    /// <summary>
    /// Represents an SDMX Data Message containing statistical data
    /// </summary>
    [XmlRoot("GenericData", Namespace = "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/message")]
    public class SdmxDataMessage
    {
        [XmlElement("Header")]
        [JsonPropertyName("header")]
        public SdmxHeader Header { get; set; } = new();

        [XmlElement("DataSet")]
        [JsonPropertyName("dataSet")]
        public SdmxDataSet DataSet { get; set; } = new();
    }

    public class SdmxHeader
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("prepared")]
        [JsonPropertyName("prepared")]
        public DateTime Prepared { get; set; } = DateTime.UtcNow;

        [XmlElement("Sender")]
        [JsonPropertyName("sender")]
        public SdmxSender Sender { get; set; } = new();

        [XmlElement("Structure")]
        [JsonPropertyName("structure")]
        public SdmxStructureRef StructureRef { get; set; } = new();
    }

    public class SdmxSender
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();

        [XmlElement("Contact")]
        [JsonPropertyName("contact")]
        public SdmxContact Contact { get; set; } = new();
    }

    public class SdmxContact
    {
        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();

        [XmlElement("Email")]
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [XmlElement("URI")]
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = string.Empty;
    }

    public class SdmxStructureRef
    {
        [XmlAttribute("structureID")]
        [JsonPropertyName("structureID")]
        public string StructureId { get; set; } = string.Empty;

        [XmlAttribute("namespace")]
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = "urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure=CBSL:DSD_PRICE_INDICES";

        [XmlAttribute("dimensionAtObservation")]
        [JsonPropertyName("dimensionAtObservation")]
        public string DimensionAtObservation { get; set; } = "TIME_PERIOD";
    }

    public class SdmxDataSet
    {
        [XmlAttribute("structureRef")]
        [JsonPropertyName("structureRef")]
        public string StructureRef { get; set; } = "CBSL:DSD_PRICE_INDICES";

        [XmlElement("Series")]
        [JsonPropertyName("series")]
        public List<SdmxSeries> Series { get; set; } = new();
    }

    public class SdmxSeries
    {
        [XmlElement("SeriesKey")]
        [JsonPropertyName("seriesKey")]
        public SdmxSeriesKey SeriesKey { get; set; } = new();

        [XmlElement("Attributes")]
        [JsonPropertyName("attributes")]
        public SdmxSeriesAttributes Attributes { get; set; } = new();

        [XmlElement("Obs")]
        [JsonPropertyName("observations")]
        public List<SdmxObservation> Observations { get; set; } = new();
    }

    public class SdmxSeriesKey
    {
        [XmlElement("Value")]
        [JsonPropertyName("values")]
        public List<SdmxKeyValue> Values { get; set; } = new();
    }

    public class SdmxKeyValue
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("value")]
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public class SdmxSeriesAttributes
    {
        [XmlElement("Value")]
        [JsonPropertyName("values")]
        public List<SdmxAttributeValue> Values { get; set; } = new();
    }

    public class SdmxAttributeValue
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("value")]
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public class SdmxObservation
    {
        [XmlElement("ObsKey")]
        [JsonPropertyName("obsKey")]
        public SdmxObsKey ObsKey { get; set; } = new();

        [XmlElement("ObsValue")]
        [JsonPropertyName("obsValue")]
        public SdmxObsValue ObsValue { get; set; } = new();

        [XmlElement("Attributes")]
        [JsonPropertyName("attributes")]
        public SdmxObsAttributes Attributes { get; set; } = new();
    }

    public class SdmxObsKey
    {
        [XmlElement("Value")]
        [JsonPropertyName("values")]
        public List<SdmxKeyValue> Values { get; set; } = new();
    }

    public class SdmxObsValue
    {
        [XmlAttribute("value")]
        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }

    public class SdmxObsAttributes
    {
        [XmlElement("Value")]
        [JsonPropertyName("values")]
        public List<SdmxAttributeValue> Values { get; set; } = new();
    }
}