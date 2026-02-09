using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class DatabaseStatisticsService : IDatabaseStatisticsService
{
	private readonly SqliteConnection _connection;
	private readonly string _tracksFolder;
	private readonly string _dbFilePath;

	public DatabaseStatisticsService(SqliteConnection connection, string tracksFolder)
	{
		_connection = connection;
		_tracksFolder = tracksFolder;
		_dbFilePath = Path.Combine(AppContext.BaseDirectory, "tracks_cache.db");
	}

	public DatabaseStatistics CollectStatistics()
	{
		return new DatabaseStatistics
		{
			TracksCount = QueryScalarInt("SELECT COUNT(*) FROM Tracks"),
			TotalListens = QueryScalarInt("SELECT COUNT(*) FROM listensHistory"),
			UniqueListenedTracks = QueryScalarInt("SELECT COUNT(DISTINCT trackId) FROM listensHistory"),
			LocalFavoritesCount = QueryScalarInt("SELECT COUNT(*) FROM favoriteLocalTracks"),
			FirstListenDate = QueryScalarDateTime("SELECT MIN(utcTime) FROM listensHistory"),
			LastListenDate = QueryScalarDateTime("SELECT MAX(utcTime) FROM listensHistory"),
			CachedFilesCount = GetCachedFilesCount(),
			CachedFilesSize = GetCachedFilesSize(),
			DatabaseFileSize = GetDatabaseFileSize()
		};
	}

	private int QueryScalarInt(string sql)
	{
		try
		{
			using var cmd = _connection.CreateCommand();
			cmd.CommandText = sql;
			var result = cmd.ExecuteScalar();
			return result is long l ? (int)l : 0;
		}
		catch
		{
			return 0;
		}
	}

	private DateTime? QueryScalarDateTime(string sql)
	{
		try
		{
			using var cmd = _connection.CreateCommand();
			cmd.CommandText = sql;
			var result = cmd.ExecuteScalar();
			if (result is DBNull || result is null || result is string s && string.IsNullOrWhiteSpace(s))
				return null;

			return DateTime.TryParse(result.ToString(), out var dt) ? dt.ToLocalTime() : null;
		}
		catch
		{
			return null;
		}
	}

	private int GetCachedFilesCount()
	{
		try
		{
			if (!Directory.Exists(_tracksFolder))
				return 0;

			return Directory.GetFiles(_tracksFolder).Length;
		}
		catch
		{
			return 0;
		}
	}

	private long GetCachedFilesSize()
	{
		try
		{
			if (!Directory.Exists(_tracksFolder))
				return 0;

			return Directory.GetFiles(_tracksFolder)
				.Sum(f => new FileInfo(f).Length);
		}
		catch
		{
			return 0;
		}
	}

	private long GetDatabaseFileSize()
	{
		try
		{
			var fileInfo = new FileInfo(_dbFilePath);
			return fileInfo.Exists ? fileInfo.Length : 0;
		}
		catch
		{
			return 0;
		}
	}
}
