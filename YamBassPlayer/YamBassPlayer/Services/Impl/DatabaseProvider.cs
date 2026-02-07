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
	}

	public void Dispose()
	{
		_connection.Dispose();
	}
}