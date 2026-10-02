using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Impl;

public sealed class HistoryService : IHistoryService
{
	private readonly IDbConnectionFactory _connectionFactory;

	public HistoryService(IDbConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public void LogListen(string trackId, ListenSource source)
	{
		Logging.LogBeforeCall();

		var utcNow = DateTime.UtcNow;
		var offset = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			INSERT INTO listensHistory (trackId, utcTime, utcOffsetMinutes, source)
			VALUES ($t, $u, $o, $s);
			""";

		cmd.Parameters.AddWithValue("$t", trackId);
		cmd.Parameters.AddWithValue("$u", utcNow.ToString("O"));
		cmd.Parameters.AddWithValue("$o", offset);
		cmd.Parameters.AddWithValue("$s", source.ToString());

		cmd.ExecuteNonQuery();

		Logging.LogAfterCall();
	}

	public IReadOnlyList<(string trackId, int count)> GetTopTracks(int limit = 10)
	{
		var result = new List<(string trackId, int count)>();

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT trackId, COUNT(*) as cnt
			FROM listensHistory
			GROUP BY trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			result.Add((reader.GetString(0), reader.GetInt32(1)));
		}

		return result;
	}

	public IReadOnlyList<(string trackId, int count)> GetTopEveningTracks(int limit = 10)
	{
		var result = new List<(string trackId, int count)>();

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) >= 16
			  AND CAST(strftime('%H', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) < 24
			GROUP BY trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			result.Add((reader.GetString(0), reader.GetInt32(1)));
		}

		return result;
	}

	public IReadOnlyList<(string trackId, int count)> GetTopTracksByDayOfWeek(DayOfWeek day, int limit = 50)
	{
		var result = new List<(string trackId, int count)>();

		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT trackId, COUNT(*) as cnt
			FROM listensHistory
			WHERE CAST(strftime('%w', datetime(utcTime, '+' || utcOffsetMinutes || ' minutes')) AS INTEGER) = $dayOfWeek
			GROUP BY trackId
			ORDER BY cnt DESC
			LIMIT $limit;
			""";

		cmd.Parameters.AddWithValue("$dayOfWeek", (int)day);
		cmd.Parameters.AddWithValue("$limit", limit);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			result.Add((reader.GetString(0), reader.GetInt32(1)));
		}

		return result;
	}

	public int GetListenCount(string trackId)
	{
		using var connection = _connectionFactory.Create();
		using var cmd = connection.CreateCommand();
		cmd.CommandText =
			"""
			SELECT COUNT(*) FROM listensHistory WHERE trackId = $trackId;
			""";
		cmd.Parameters.AddWithValue("$trackId", trackId);
		return Convert.ToInt32(cmd.ExecuteScalar());
	}
}
