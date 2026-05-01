using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl;

public sealed class DatabaseProvider : IDatabaseProvider
{
	private readonly SqliteConnection _connection;

	public SqliteConnection Connection => _connection;

	public DatabaseProvider()
	{
		SQLitePCL.Batteries_V2.Init();
		string dbPath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
		_connection = new SqliteConnection($"Data Source={dbPath}");
		_connection.Open();
		EnableWalMode();
		EnableBusyTimeout();
	}

	/// <summary>WAL mode allows concurrent readers while a writer is active.</summary>
	private void EnableWalMode()
	{
		using var cmd = new SqliteCommand("PRAGMA journal_mode=WAL", _connection);
		cmd.ExecuteNonQuery();
	}

	/// <summary>Wait up to 5 seconds when the database is busy instead of failing immediately.</summary>
	private void EnableBusyTimeout()
	{
		using var cmd = new SqliteCommand("PRAGMA busy_timeout=5000", _connection);
		cmd.ExecuteNonQuery();
	}

	public void Dispose()
	{
		_connection.Dispose();
	}
}