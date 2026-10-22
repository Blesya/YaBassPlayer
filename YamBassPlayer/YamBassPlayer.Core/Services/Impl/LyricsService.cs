using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Текст песни: загружает источник (<see cref="ILyricsSource"/>), если он это умеет,
/// и кэширует результат в своей БД. Источник отвечает только за загрузку.
/// </summary>
public sealed class LyricsService : ILyricsService
{
	private readonly IMusicSourceRegistry _sources;
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IDbWriteLock _writeLock;

	public LyricsService(IMusicSourceRegistry sources, IDbConnectionFactory connectionFactory, IDbWriteLock writeLock)
	{
		_sources = sources;
		_connectionFactory = connectionFactory;
		_writeLock = writeLock;
	}

	public async Task<string?> GetLyricsAsync(Track track)
	{
		Logging.LogBeforeCall();

		string? cached = await GetCachedLyricsAsync(track.Id);
		if (cached is not null)
			return cached;

		if (_sources.Get(track.SourceId) is not ILyricsSource source)
			return null;

		try
		{
			string? lyrics = await source.GetLyricsAsync(track);

			// Кэшируем и положительный результат: повторно тот же трек не запрашиваем.
			if (lyrics is not null)
				await SaveLyricsAsync(track.Id, lyrics);

			Logging.LogAfterCall();
			return lyrics;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return null;
		}
	}

	private async Task<string?> GetCachedLyricsAsync(string trackId)
	{
		try
		{
			using var connection = _connectionFactory.Create();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "SELECT Lyrics FROM Tracks WHERE TrackId = @id LIMIT 1";
			cmd.Parameters.AddWithValue("@id", trackId);

			var result = await cmd.ExecuteScalarAsync();
			return result as string;
		}
		catch (Exception ex)
		{
			ex.Handle();
			return null;
		}
	}

	private async Task SaveLyricsAsync(string trackId, string lyrics)
	{
		try
		{
			using var lockHandle = await _writeLock.AcquireAsync();
			using var connection = _connectionFactory.Create();
			using var cmd = connection.CreateCommand();
			cmd.CommandText =
				"""
				UPDATE Tracks
				SET Lyrics = @lyrics
				WHERE TrackId = @id
				""";
			cmd.Parameters.AddWithValue("@lyrics", lyrics);
			cmd.Parameters.AddWithValue("@id", trackId);
			await cmd.ExecuteNonQueryAsync();
		}
		catch (Exception ex)
		{
			ex.Handle();
		}
	}
}
