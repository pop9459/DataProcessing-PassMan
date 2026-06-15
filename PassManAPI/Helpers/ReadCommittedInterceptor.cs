using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace PassManAPI.Helpers;

/// <summary>
/// Sets session isolation level to READ COMMITTED on every MySQL connection.
/// Registered only for the MySQL DbContext (not SQLite/test), so no provider check needed here.
/// READ COMMITTED prevents dirty reads while keeping write throughput higher than REPEATABLE READ.
/// </summary>
public class ReadCommittedInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
