using Microsoft.Data.Sqlite;
using Yandex.Music.Api;
using Yandex.Music.Api.Common;
using Yandex.Music.Api.Models.Track;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class LyricsService : ILyricsService
{
	private readonly YandexMusicApi _api;
	private readonly AuthStorage _storage;
	private readonly SqliteConnection _connection;
	private readonly IDbWriteLock _writeLock;

	public LyricsService(YandexMusicApi api, AuthStorage storage, SqliteConnection connection, IDbWriteLock writeLock)
	{
		_api = api;
		_storage = storage;
		_connection = connection;
		_writeLock = writeLock;

		SqliteSchemaHelper.EnsureTrackColumn(_connection, "Lyrics", "TEXT");
	}

	public async Task<string?> GetLyricsAsync(Track track)
	{
		// Local tracks use file paths as IDs — the Yandex API cannot handle them
		if (track.SourceType == SourceIds.Local)
			return null;

		string? cached = await GetCachedLyricsAsync(track.Id);
		if (cached is not null)
			return cached;

		try
		{
			var response = await _api.Track.GetSupplementAsync(_storage, track.Id);
			var lyrics = response?.Result?.Lyrics;
			string? fullLyrics = string.IsNullOrWhiteSpace(lyrics?.FullLyrics) ? null : lyrics.FullLyrics;

			// Cache the result (including negative results) so the same track isn't re-fetched.
			if (fullLyrics is not null)
				await SaveLyricsAsync(track.Id, fullLyrics);

			return fullLyrics;
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
			using var cmd = _connection.CreateCommand();
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
			using var cmd = _connection.CreateCommand();
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
