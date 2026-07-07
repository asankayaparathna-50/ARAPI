using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace OpenAPI.Domain.Entities.Statistics.Sdmx
{
    /// <summary>
    /// Represents an SDMX Data Structure Definition (DSD)
    /// </summary>
    [XmlRoot("DataStructure")]
    public class SdmxDataStructure
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("version")]
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [XmlAttribute("agencyID")]
        [JsonPropertyName("agencyID")]
        public string AgencyId { get; set; } = string.Empty;

        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();

        [XmlElement("Description")]
        [JsonPropertyName("description")]
        public SdmxLocalizedText Description { get; set; } = new();

        [XmlArray("Dimensions")]
        [XmlArrayItem("Dimension")]
        [JsonPropertyName("dimensions")]
        public List<SdmxDimension> Dimensions { get; set; } = new();

        [XmlArray("Attributes")]
        [XmlArrayItem("Attribute")]
        [JsonPropertyName("attributes")]
        public List<SdmxAttribute> Attributes { get; set; } = new();
    }

    public class SdmxLocalizedText
    {
        [XmlAttribute("xml:lang")]
        [JsonPropertyName("lang")]
        public string Language { get; set; } = "en";

        [XmlText]
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class SdmxDimension
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlAttribute("position")]
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();

        [XmlElement("ConceptRef")]
        [JsonPropertyName("conceptRef")]
        public string ConceptRef { get; set; } = string.Empty;

        [XmlArray("CodeList")]
        [XmlArrayItem("Code")]
        [JsonPropertyName("codeList")]
        public List<SdmxCode> CodeList { get; set; } = new();
    }

    public class SdmxAttribute
    {
        [XmlAttribute("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();

        [XmlAttribute("assignmentStatus")]
        [JsonPropertyName("assignmentStatus")]
        public string AssignmentStatus { get; set; } = "Optional";
    }

    public class SdmxCode
    {
        [XmlAttribute("value")]
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [XmlElement("Name")]
        [JsonPropertyName("name")]
        public SdmxLocalizedText Name { get; set; } = new();
    }
}