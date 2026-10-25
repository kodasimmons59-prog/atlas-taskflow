using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AtlasTaskFlow.Data;

/// <summary>Sorts database exceptions into the kinds Atlas reacts to differently.</summary>
public static class DataErrors
{
    // SQL Server error numbers for a rejected row: duplicate key (2601, 2627), foreign key or check (547),
    // NULL in a required column (515), and text too long for its column (2628, 8152).
    private static readonly HashSet<int> ConstraintErrors = new() { 2601, 2627, 547, 515, 2628, 8152 };

    /// <summary>True when the database rejected the data itself, as opposed to being unreachable.</summary>
    public static bool IsConstraintViolation(Exception ex) =>
        ex is DbUpdateException && Chain(ex).OfType<SqlException>().Any(s => ConstraintErrors.Contains(s.Number));

    /// <summary>True when the failure came from reaching or talking to the database.</summary>
    public static bool IsDatabaseUnavailable(Exception ex) =>
        !IsConstraintViolation(ex) && Chain(ex).Any(e => e is SqlException or RetryLimitExceededException or DbUpdateException
            || (e is InvalidOperationException && e.Message.Contains("transient failure")));

    private static IEnumerable<Exception> Chain(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException) yield return e;
    }
}
