using Microsoft.EntityFrameworkCore;

namespace ResourcePulse.Persistence;

/// <summary>
/// Classifies the database errors services translate into a <c>ServiceResult</c>.
/// </summary>
/// <remarks>
/// Message inspection rather than <c>PostgresException.SqlState</c>, which is the
/// documented convention: it keeps the Npgsql exception type out of the service
/// layer. ADR-0008 records moving to <c>SqlState</c>/<c>ConstraintName</c> as a
/// future improvement — the point of gathering the copies here is that when that
/// day comes there is one place to change instead of a dozen.
/// </remarks>
public static class DbUpdateExceptionExtensions
{
    /// <summary>A unique index or constraint refused the write.</summary>
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        Says(ex, "duplicate key") || Says(ex, "unique constraint");

    /// <summary>
    /// A foreign key refused the write — either an insert pointing at a row that
    /// is not there, or a delete of a row something still points at.
    /// </summary>
    public static bool IsForeignKeyViolation(this DbUpdateException ex) =>
        Says(ex, "foreign key");

    private static bool Says(DbUpdateException ex, string fragment) =>
        ex.InnerException?.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
}
