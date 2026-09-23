using Microsoft.Extensions.Configuration;

namespace SNAP
{
    /// <summary>
    /// Base URL for SNAP.api, read from appsettings.json the same way
    /// SNAP.infrastructure.config.ConnectionStrings reads connection strings - defaults to the
    /// local dev profile in SNAP.api/Properties/launchSettings.json so a fresh checkout works
    /// without any config changes.
    /// </summary>
    public static class ApiConfig
    {
        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        public static string BaseUrl => Configuration["ApiBaseUrl"] ?? "http://localhost:5254";
    }
}
