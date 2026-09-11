using System.Text.Json;
using Microsoft.Data.Sqlite;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Upserts a local track into Tracks, Artists, and TrackArtists tables.
/// This step keeps TrackId compatible with existing playback while also persisting
/// explicit source-aware fields for future storage work.
/// </summary>
internal sealed class LocalTrackWriter : IDisposable
{
	private readonly SqliteCommand _upsertTrack;
	private readonly SqliteCommand _clearArtistLinks;
	private readonly SqliteCommand _upsertArtist;
	private readonly SqliteCommand _linkArtist;

	public LocalTrackWriter(SqliteConnection connection, SqliteTransaction transaction)
	{
		_upsertTrack = connection.CreateCommand();
		_upsertTrack.Transaction = transaction;
		_upsertTrack.CommandText = @"
			INSERT OR REPLACE INTO Tracks
				(TrackId, Artist, Title, Album, DurationMs, Year, TrackNumber, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, SourceType, SourceTrackId, LocalFilePath, FolderId, UpdatedAt)
			VALUES
				(@trackId, @artist, @title, @album, @durationMs, @year, @trackNumber, @coverUrl, @remoteCoverUrl, @localCoverPath, @genres, @albumId, 'local', @sourceTrackId, @localFilePath, @folderId, @updatedAt)";
		_upsertTrack.Parameters.Add("@trackId", SqliteType.Text);
		_upsertTrack.Parameters.Add("@artist", SqliteType.Text);
		_upsertTrack.Parameters.Add("@title", SqliteType.Text);
		_upsertTrack.Parameters.Add("@album", SqliteType.Text);
		_upsertTrack.Parameters.Add("@durationMs", SqliteType.Integer);
		_upsertTrack.Parameters.Add("@year", SqliteType.Integer);
		_upsertTrack.Parameters.Add("@trackNumber", SqliteType.Integer);
		_upsertTrack.Parameters.Add("@coverUrl", SqliteType.Text);
		_upsertTrack.Parameters.Add("@remoteCoverUrl", SqliteType.Text);
		_upsertTrack.Parameters.Add("@localCoverPath", SqliteType.Text);
		_upsertTrack.Parameters.Add("@genres", SqliteType.Text);
		_upsertTrack.Parameters.Add("@albumId", SqliteType.Text);
		_upsertTrack.Parameters.Add("@sourceTrackId", SqliteType.Text);
		_upsertTrack.Parameters.Add("@localFilePath", SqliteType.Text);
		_upsertTrack.Parameters.Add("@folderId", SqliteType.Integer);
		_upsertTrack.Parameters.Add("@updatedAt", SqliteType.Integer);

		_clearArtistLinks = connection.CreateCommand();
		_clearArtistLinks.Transaction = transaction;
		_clearArtistLinks.CommandText = "DELETE FROM TrackArtists WHERE TrackId = @trackId";
		_clearArtistLinks.Parameters.Add("@trackId", SqliteType.Text);

		// Local artists use their name as the ID since they have no external identifier.
		_upsertArtist = connection.CreateCommand();
		_upsertArtist.Transaction = transaction;
		_upsertArtist.CommandText = @"
			INSERT OR REPLACE INTO Artists (Id, Name, CoverUrl, Description, UpdatedAt)
			VALUES (@id, @name, NULL, NULL, @updatedAt)";
		_upsertArtist.Parameters.Add("@id", SqliteType.Text);
		_upsertArtist.Parameters.Add("@name", SqliteType.Text);
		_upsertArtist.Parameters.Add("@updatedAt", SqliteType.Integer);

		_linkArtist = connection.CreateCommand();
		_linkArtist.Transaction = transaction;
		_linkArtist.CommandText = @"
			INSERT OR IGNORE INTO TrackArtists (TrackId, ArtistId)
			VALUES (@trackId, @artistId)";
		_linkArtist.Parameters.Add("@trackId", SqliteType.Text);
		_linkArtist.Parameters.Add("@artistId", SqliteType.Text);
	}

	public void Save(Track track, int folderId, long updatedAt)
	{
		string? genresJson = track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null;
		string localFilePath = track.LocalFilePath ?? track.Id;
		string? localCoverPath = CoverMetadataResolver.ResolveLocalCoverPath(track.SourceType, track.CoverUrl, track.LocalCoverPath);
		string? coverUrl = CoverMetadataResolver.ResolveLegacyCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl, localCoverPath);

		_upsertTrack.Parameters["@trackId"].Value = track.Id;
		_upsertTrack.Parameters["@artist"].Value = track.Artist;
		_upsertTrack.Parameters["@title"].Value = track.Title;
		_upsertTrack.Parameters["@album"].Value = track.Album;
		_upsertTrack.Parameters["@durationMs"].Value = (object?)track.DurationMs ?? DBNull.Value;
		_upsertTrack.Parameters["@year"].Value = (object?)track.Year ?? DBNull.Value;
		_upsertTrack.Parameters["@trackNumber"].Value = (object?)track.TrackNumber ?? DBNull.Value;
		_upsertTrack.Parameters["@coverUrl"].Value = (object?)coverUrl ?? DBNull.Value;
		_upsertTrack.Parameters["@remoteCoverUrl"].Value = DBNull.Value;
		_upsertTrack.Parameters["@localCoverPath"].Value = (object?)localCoverPath ?? DBNull.Value;
		_upsertTrack.Parameters["@genres"].Value = (object?)genresJson ?? DBNull.Value;
		_upsertTrack.Parameters["@albumId"].Value = (object?)track.AlbumInfo?.Id ?? DBNull.Value;
		_upsertTrack.Parameters["@sourceTrackId"].Value = track.SourceTrackId;
		_upsertTrack.Parameters["@localFilePath"].Value = localFilePath;
		_upsertTrack.Parameters["@folderId"].Value = folderId;
		_upsertTrack.Parameters["@updatedAt"].Value = updatedAt;
		_upsertTrack.ExecuteNonQuery();

		_clearArtistLinks.Parameters["@trackId"].Value = track.Id;
		_clearArtistLinks.ExecuteNonQuery();

		if (track.Artists is { Count: > 0 } artists)
		{
			foreach (Artist artist in artists)
			{
				_upsertArtist.Parameters["@id"].Value = artist.Id;
				_upsertArtist.Parameters["@name"].Value = artist.Name;
				_upsertArtist.Parameters["@updatedAt"].Value = updatedAt;
				_upsertArtist.ExecuteNonQuery();

				_linkArtist.Parameters["@trackId"].Value = track.Id;
				_linkArtist.Parameters["@artistId"].Value = artist.Id;
				_linkArtist.ExecuteNonQuery();
			}
		}
	}

	public void Dispose()
	{
		_upsertTrack.Dispose();
		_clearArtistLinks.Dispose();
		_upsertArtist.Dispose();
		_linkArtist.Dispose();
	}
}
