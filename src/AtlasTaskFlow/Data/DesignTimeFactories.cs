using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AtlasTaskFlow.Data;

// Used only by the "dotnet ef" tools to build migrations, never by the running app. Each engine keeps its own
// migrations: Data/Migrations (SQL Server) and Data/Migrations/Sqlite.

public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<AtlasDbContext>
{
    public AtlasDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AtlasDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AtlasTaskFlow;Trusted_Connection=True").Options);
}

public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAtlasDbContext>
{
    public SqliteAtlasDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteAtlasDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
