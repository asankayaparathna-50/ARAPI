namespace OpenAPI.API.Constants
{
    public static class ApiErrorMessages
    {
        public const string PeriodRequired = "Period is required";
        public const string InvalidPeriodFormat = "Period must be in yyyy-MM-dd format";
        public const string NoPriceIndicesFound = "No price indices data found for period '{0}'";
        public const string InternalServerError = "Internal server error occurred while retrieving price indices.";
        public const string ProcessingError = "An error occurred while processing your request.";
        public const string MarketRequired = "Market is required";
    }

    public static class ContentType 
    {
        public const string ContentTypeApplicationJson = "application/json";
        public const string ContentTypeApplicationXml = "application/xml";
        public const string ContentTypeTextXml = "text/xml";

    }

    public static class DataTypes
    {
        public const string DateFormatYyyyMmDd = "yyyy-MM-dd";
    }





}
