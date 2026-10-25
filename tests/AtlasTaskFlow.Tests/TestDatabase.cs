using AtlasTaskFlow.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Tests;

/// <summary>
/// A throwaway SQL Server database for one test, built from the real migrations and dropped afterwards.
///
/// Uses LocalDB by default (Visual Studio on Windows). Set ATLAS_TEST_SQLSERVER to a connection string without a
/// database name to use another SQL Server, as the CI build does.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private const string LocalDb = "Server=(localdb)\\mssqllocaldb;Trusted_Connection=True;MultipleActiveResultSets=true";

    public TestDatabase()
    {
        var server = Environment.GetEnvironmentVariable("ATLAS_TEST_SQLSERVER");
        ConnectionString = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? LocalDb : server)
        {
            InitialCatalog = $"AtlasTaskFlow_Test_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public AtlasDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AtlasDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Creates the empty schema, for tests that don't start the whole app (the app migrates by itself).</summary>
    public AtlasDbContext CreateMigratedContext()
    {
        var db = CreateContext();
        db.Database.Migrate();
        return db;
    }

    public void Dispose()
    {
        using var db = CreateContext();
        SqlConnection.ClearAllPools();
        db.Database.EnsureDeleted();
    }
}
