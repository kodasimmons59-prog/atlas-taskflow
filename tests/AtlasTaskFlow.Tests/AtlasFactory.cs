using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AtlasTaskFlow.Tests;

public static class AtlasFactory
{
    public const string DemoPassword = "AtlasDemo2026";

    /// <summary>Starts the real app pointed at the given database (SQL Server unless another provider is named).</summary>
    public static WebApplicationFactory<Program> Create(string connectionString, string environment = "Development",
        string provider = "SqlServer") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // Added last, so it wins over the LocalDB connection string in appsettings.Development.json.
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:Provider"] = provider,
                // The sample accounts' password, the same in every environment the tests start.
                ["DemoAccounts:Password"] = DemoPassword
            }));
        });
}
