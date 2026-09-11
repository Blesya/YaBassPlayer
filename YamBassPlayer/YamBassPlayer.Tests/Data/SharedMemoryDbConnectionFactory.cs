using Microsoft.Data.Sqlite;
using YamBassPlayer.Services;

namespace YamBassPlayer.Tests.Data;

/// <summary>
/// Test double for <see cref="IDbConnectionFactory"/> backed by a shared-cache in-memory
/// SQLite database. An in-memory database is destroyed once every connection to it is closed,
/// so this factory keeps one "anchor" connection open for the lifetime of the test and hands
/// out additional connections to the same database via <see cref="Create"/>.
/// </summary>
internal sealed class SharedMemoryDbConnectionFactory : IDbConnectionFactory, IDisposable
{
	private readonly SqliteConnection _anchor;
	private readonly string _connectionString;

	public SharedMemoryDbConnectionFactory()
	{
		var builder = new SqliteConnectionStringBuilder
		{
			DataSource = $"testdb_{Guid.NewGuid():N}",
			Mode = SqliteOpenMode.Memory,
			Cache = SqliteCacheMode.Shared,
			Pooling = false,
		};
		_connectionString = builder.ConnectionString;
		_anchor = new SqliteConnection(_connectionString);
		_anchor.Open();
	}

	/// <summary>Connection kept open so the in-memory database survives between operations.</summary>
	public SqliteConnection Anchor => _anchor;

	public SqliteConnection Create()
	{
		var connection = new SqliteConnection(_connectionString);
		connection.Open();
		return connection;
	}

	public void Dispose()
	{
		_anchor.Dispose();
	}
}
