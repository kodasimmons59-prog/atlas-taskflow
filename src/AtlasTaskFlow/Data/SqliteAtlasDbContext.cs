using Microsoft.EntityFrameworkCore;

namespace AtlasTaskFlow.Data;

/// <summary>
/// The same Atlas database, on SQLite instead of SQL Server. Used by the live Azure site, where Azure for Students
/// won't create an Azure SQL server (DR-0010). It exists as its own class only so that EF Core can keep a separate set
/// of migrations for SQLite (Data/Migrations/Sqlite); the tables and rules are the ones in <see cref="AtlasDbContext"/>.
/// </summary>
public class SqliteAtlasDbContext : AtlasDbContext
{
    public SqliteAtlasDbContext(DbContextOptions<SqliteAtlasDbContext> options) : base(options) { }
}
