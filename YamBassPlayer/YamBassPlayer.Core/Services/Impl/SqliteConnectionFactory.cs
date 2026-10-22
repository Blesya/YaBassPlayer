using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
	private static readonly object InitLock = new();
	private static bool _initialized;

	private readonly string _dbPath;

	public SqliteConnectionFactory()
		: this(Path.Combine(AppContext.BaseDirectory, "tracks_cache.db"))
	{
	}

	public SqliteConnectionFactory(string dbPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);

		lock (InitLock)
		{
			if (!_initialized)
			{
				SQLitePCL.Batteries_V2.Init();
				_initialized = true;
			}
		}

		_dbPath = dbPath;
	}

	public SqliteConnection Create()
	{
		var connection = new SqliteConnection($"Data Source={_dbPath}");
		connection.Open();

		// WAL allows concurrent readers while a writer is active; busy_timeout waits instead
		// of failing immediately when another connection holds the write lock.
		using var cmd = connection.CreateCommand();
		cmd.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
		cmd.ExecuteNonQuery();

		return connection;
	}
}
