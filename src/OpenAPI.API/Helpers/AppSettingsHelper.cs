namespace OpenAPI.Helpers
{
    public static class AppSettingsHelper
    {
        private static IConfiguration? _configuration;

        // Initialize from Program.cs or Startup.cs
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Generic getter for any key (e.g., "AppConfig:ApiVersion")
        public static string? GetValue(string key)
        {
            if (_configuration == null)
                throw new InvalidOperationException("AppSettingsHelper not initialized.");

            return _configuration[key];
        }

        // Strongly typed getter
        public static T? GetSection<T>(string sectionName)
        {
            if (_configuration == null)
                throw new InvalidOperationException("AppSettingsHelper not initialized.");

            return _configuration.GetSection(sectionName).Get<T>();
        }
    }
}
