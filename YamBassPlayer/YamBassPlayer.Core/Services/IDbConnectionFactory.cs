using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services;

/// <summary>
/// Creates a new, already-opened SQLite connection for a single database operation.
/// <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> is not thread-safe, so services must
/// never share one instance across the UI thread and thread-pool callbacks.
/// </summary>
public interface IDbConnectionFactory
{
	/// <summary>Returns a new opened connection. The caller owns and must dispose it.</summary>
	SqliteConnection Create();
}
