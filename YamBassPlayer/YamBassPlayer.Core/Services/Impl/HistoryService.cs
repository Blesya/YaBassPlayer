using Microsoft.Data.Sqlite;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class HistoryService : IHistoryService
{
	private readonly IDbConnectionFactory _connectionFactory;

	public HistoryService(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public void LogListen(TrackKey key, PlaylistContext context)
	{
		Logging.LogBeforeCall();

		var utcNow = DateTime.UtcNow;
		var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			INSERT INTO listensHistory (trackId, sourceId, context, utcTime, utcOffsetMinutes)
			VALUES ($t, $s, $c, $u, $o);
			""";

		cmd.Parameters.AddWithValue("$t", key.TrackId);
		cmd.Parameters.AddWithValue("$s", key.SourceId.Value);
		cmd.Parameters.AddWithValue("$c", context.ToString());
		cmd.Parameters.AddWithValue("$u", utcNow.ToString("O"));
		cmd.Parameters.AddWithValue("$o", offset);

		cmd.ExecuteNonQuery();

		Logging.LogAfterCall();
	}

	public IReadOnlyList<(TrackKey key, int count)> GetTopTracks(int limit = 10)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT sourceId, trackId, COUNT(*) as cnt
			FROM listensHistory
			GROUP BY sourceId, trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$limit", limit);

		return ReadKeys(cmd);
	}

	public IReadOnlyList<(TrackKey key, int count)> GetTopEveningTracks(int limit = 10)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT sourceId, trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) >= 16
			  AND CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) < 24
			GROUP BY sourceId, trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$limit", limit);

		return ReadKeys(cmd);
	}

	public IReadOnlyList<(TrackKey key, int count)> GetTopTracksByDayOfWeek(DayOfWeek day, int limit = 50)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT sourceId, trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%w', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) = $dayOfWeek
			GROUP BY sourceId, trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$dayOfWeek", (int)day);
		cmd.Parameters.AddWithValue("$limit", limit);

		return ReadKeys(cmd);
	}

	public int GetListenCount(TrackKey key)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*) FROM listensHistory WHERE sourceId = $sourceId AND trackId = $trackId;
			""";
		cmd.Parameters.AddWithValue("$sourceId", key.SourceId.Value);
		cmd.Parameters.AddWithValue("$trackId", key.TrackId);
		return Convert.ToInt32(cmd.ExecuteScalar());
	}

	private static IReadOnlyList<(TrackKey key, int count)> ReadKeys(SqliteCommand cmd)
	{
		var result = new List<(TrackKey key, int count)>();

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
			result.Add((new TrackKey(new SourceId(reader.GetString(0)), reader.GetString(1)), reader.GetInt32(2)));

		return result;
	}
}
