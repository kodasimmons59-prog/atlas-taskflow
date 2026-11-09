using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Data;

/// <summary>
/// Chooses the database engine from configuration ("Database:Provider"):
/// <list type="bullet">
/// <item>SqlServer: LocalDB on a developer PC (appsettings.Development.json), or Azure SQL if one can be created.</item>
/// <item>Sqlite: a single database file. The live Azure site uses this (DR-0010), because Azure for Students refuses to
/// create an Azure SQL server. With no connection string, the file goes in /home/data on Azure App Service (the part of
/// the App Service disk that survives restarts and deployments), or App_Data next to the app anywhere else.</item>
/// </list>
/// The choice is read when each DbContext is created, not at startup, so tests can switch it.
/// </summary>
public static class DatabaseSetup
{
    public static IServiceCollection AddAtlasDatabase(this IServiceCollection services)
    {
        services.AddScoped<AtlasDbContext>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var connectionString = config.GetConnectionString("DefaultConnection");

            if (IsSqlite(config))
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                    connectionString = $"Data Source={DefaultSqliteFile(sp.GetRequiredService<IWebHostEnvironment>())}";
                var options = new DbContextOptionsBuilder<SqliteAtlasDbContext>()
                    .UseSqlite(connectionString)
                    .UseLoggerFactory(loggerFactory)
                    .Options;
                return new SqliteAtlasDbContext(options);
            }

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "No database connection string. Set ConnectionStrings:DefaultConnection (see docs/DEPLOYMENT.md).");

            // Retry brief connection drops, such as Azure SQL waking up from auto-pause.
            return new AtlasDbContext(new DbContextOptionsBuilder<AtlasDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                .UseLoggerFactory(loggerFactory)
                .Options);
        });
        return services;
    }

    public static bool IsSqlite(IConfiguration config) =>
        string.Equals(config["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

    private static string DefaultSqliteFile(IWebHostEnvironment env)
    {
        // WEBSITE_SITE_NAME is set only on Azure App Service, where HOME is the persistent /home share.
        var onAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        var home = Environment.GetEnvironmentVariable("HOME");
        var folder = onAppService && !string.IsNullOrEmpty(home)
            ? Path.Combine(home, "data")
            : Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "atlas-taskflow.db");
    }
}
