using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppointmentBooking.Infrastructure.Persistence;

internal static class UniqueViolation
{
    private const string PostgresUniqueViolation = "23505";
    private const int SqliteConstraintError = 19;

    public static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException switch
    {
        PostgresException pg => pg.SqlState == PostgresUniqueViolation,
        SqliteException sqlite => sqlite.SqliteErrorCode == SqliteConstraintError,
        _ => false
    };
}
