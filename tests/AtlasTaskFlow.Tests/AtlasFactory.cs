using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AtlasTaskFlow.Tests;

public static class AtlasFactory
{
    /// <summary>Starts the real app pointed at the given database.</summary>
    public static WebApplicationFactory<Program> Create(string connectionString, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // Added last, so it wins over the LocalDB connection string in appsettings.Development.json.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            }));
        });
}
