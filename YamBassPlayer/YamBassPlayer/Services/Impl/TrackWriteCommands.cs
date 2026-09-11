using Microsoft.Data.Sqlite;
using System.Text.Json;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Reusable set of prepared commands that writes a track together with its artist links
/// and album. All commands share one connection (and optional transaction), so a batch of
/// tracks is persisted without opening a connection per track.
/// </summary>
internal sealed class TrackWriteCommands : IDisposable
{
	private readonly SqliteCommand _tracks;
	private readonly SqliteCommand _artists;
	private readonly SqliteCommand _trackArtists;
	private readonly SqliteCommand _albums;

	public TrackWriteCommands(SqliteConnection connection, SqliteTransaction? transaction)
	{
		_tracks = CreateCommand(connection, transaction, @"
			INSERT OR REPLACE INTO Tracks (TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, SourceTrackId, LocalFilePath, UpdatedAt)
			VALUES (@TrackId, @artist, @title, @album, @durationMs, @year, @coverUrl, @remoteCoverUrl, @localCoverPath, @genres, @albumId, @sourceType, @sourceTrackId, @localFilePath, @updatedAt)",
			"@TrackId", "@artist", "@title", "@album", "@durationMs", "@year", "@coverUrl", "@remoteCoverUrl", "@localCoverPath", "@genres", "@albumId", "@sourceType", "@sourceTrackId", "@localFilePath", "@updatedAt");

		_artists = CreateCommand(connection, transaction, @"
			INSERT OR REPLACE INTO Artists (Id, Name, CoverUrl, Description, UpdatedAt)
			VALUES (@id, @name, @coverUrl, @description, @updatedAt)",
			"@id", "@name", "@coverUrl", "@description", "@updatedAt");

		_trackArtists = CreateCommand(connection, transaction, @"
			INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
			VALUES (@trackId, @artistId)",
			"@trackId", "@artistId");

		_albums = CreateCommand(connection, transaction, @"
			INSERT OR REPLACE INTO Albums (Id, Title, Year, CoverUrl, Genre, TrackCount, UpdatedAt)
			VALUES (@id, @title, @year, @coverUrl, @genre, @trackCount, @updatedAt)",
			"@id", "@title", "@year", "@coverUrl", "@genre", "@trackCount", "@updatedAt");
	}

	public async Task WriteAsync(Track track)
	{
		long updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string? genresJson = track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null;
		string sourceType = track.SourceType ?? SourceIds.Yandex;
		string? remoteCoverUrl = CoverMetadataResolver.ResolveRemoteCoverUrl(sourceType, track.CoverUrl, track.RemoteCoverUrl);
		string? localCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(sourceType, track.CoverUrl, track.LocalCoverPath);
		string? coverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(sourceType, track.CoverUrl, remoteCoverUrl, localCoverPath);

		Set(_tracks, "@TrackId", track.Id ?? "");
		Set(_tracks, "@artist", track.Artist ?? "");
		Set(_tracks, "@title", track.Title ?? "");
		Set(_tracks, "@album", track.Album ?? "");
		Set(_tracks, "@durationMs", track.DurationMs);
		Set(_tracks, "@year", track.Year);
		Set(_tracks, "@coverUrl", coverUrl);
		Set(_tracks, "@remoteCoverUrl", remoteCoverUrl);
		Set(_tracks, "@localCoverPath", localCoverPath);
		Set(_tracks, "@genres", genresJson);
		Set(_tracks, "@albumId", track.AlbumInfo?.Id);
		Set(_tracks, "@sourceType", sourceType);
		Set(_tracks, "@sourceTrackId", track.SourceTrackId ?? track.Id ?? "");
		Set(_tracks, "@localFilePath", track.LocalFilePath);
		Set(_tracks, "@updatedAt", updatedAt);
		await _tracks.ExecuteNonQueryAsync();

		if (track.Artists != null)
		{
			foreach (Artist artist in track.Artists)
			{
				Set(_artists, "@id", artist.Id);
				Set(_artists, "@name", artist.Name);
				Set(_artists, "@coverUrl", artist.CoverUrl);
				Set(_artists, "@description", artist.Description);
				Set(_artists, "@updatedAt", updatedAt);
				await _artists.ExecuteNonQueryAsync();

				Set(_trackArtists, "@trackId", track.Id ?? "");
				Set(_trackArtists, "@artistId", artist.Id);
				await _trackArtists.ExecuteNonQueryAsync();
			}
		}

		if (track.AlbumInfo is { } albumInfo)
		{
			Set(_albums, "@id", albumInfo.Id);
			Set(_albums, "@title", albumInfo.Title);
			Set(_albums, "@year", albumInfo.Year);
			Set(_albums, "@coverUrl", albumInfo.CoverUrl);
			Set(_albums, "@genre", albumInfo.Genre);
			Set(_albums, "@trackCount", albumInfo.TrackCount);
			Set(_albums, "@updatedAt", updatedAt);
			await _albums.ExecuteNonQueryAsync();
		}
	}

	private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql, params string[] parameterNames)
	{
		var cmd = connection.CreateCommand();
		cmd.Transaction = transaction;
		cmd.CommandText = sql;
		foreach (string name in parameterNames)
			cmd.Parameters.Add(new SqliteParameter(name, DBNull.Value));
		return cmd;
	}

	private static void Set(SqliteCommand cmd, string name, object? value)
		=> cmd.Parameters[name].Value = value ?? DBNull.Value;

	public void Dispose()
	{
		_tracks.Dispose();
		_artists.Dispose();
		_trackArtists.Dispose();
		_albums.Dispose();
	}
}
