using Microsoft.Data.Sqlite;
using System.Text.Json;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Преобразование между строками таблицы <c>Tracks</c> и моделями <see cref="Track"/>:
/// проекция SELECT, чтение строки и построение строки из модели.
/// </summary>
internal static class TrackRowMapper
{
	public const string TrackProjection =
		"TrackId, Artist, Title, Album, DurationMs, Year, CoverUrl, RemoteCoverUrl, LocalCoverPath, Genres, AlbumId, COALESCE(SourceType, 'yandex'), COALESCE(SourceTrackId, TrackId), LocalFilePath";

	public static TrackRow ReadTrackRow(SqliteDataReader reader) => new(
		TrackId: reader.GetString(0),
		Artist: reader.IsDBNull(1) ? "" : reader.GetString(1),
		Title: reader.IsDBNull(2) ? "" : reader.GetString(2),
		Album: reader.IsDBNull(3) ? "" : reader.GetString(3),
		DurationMs: reader.IsDBNull(4) ? null : reader.GetInt64(4),
		Year: reader.IsDBNull(5) ? null : reader.GetInt32(5),
		CoverUrl: reader.IsDBNull(6) ? null : reader.GetString(6),
		RemoteCoverUrl: reader.IsDBNull(7) ? null : reader.GetString(7),
		LocalCoverPath: reader.IsDBNull(8) ? null : reader.GetString(8),
		GenresJson: reader.IsDBNull(9) ? null : reader.GetString(9),
		AlbumId: reader.IsDBNull(10) ? null : reader.GetString(10),
		SourceType: reader.IsDBNull(11) ? SourceIds.Yandex : reader.GetString(11),
		SourceTrackId: reader.IsDBNull(12) ? reader.GetString(0) : reader.GetString(12),
		LocalFilePath: reader.IsDBNull(13) ? null : reader.GetString(13));

	/// <summary>
	/// Converts an in-memory Track to a TrackRow for use in the enrichment pipeline after an API fetch.
	/// </summary>
	public static TrackRow ToTrackRow(Track track) => new(
		TrackId: track.Id,
		Artist: track.Artist,
		Title: track.Title,
		Album: track.Album,
		DurationMs: track.DurationMs,
		Year: track.Year,
		CoverUrl: CoverMetadataResolver.ResolveLegacyCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl, track.LocalCoverPath),
		RemoteCoverUrl: CoverMetadataResolver.ResolveRemoteCoverUrl(track.SourceType, track.CoverUrl, track.RemoteCoverUrl),
		LocalCoverPath: CoverMetadataResolver.ResolveLocalCoverPath(track.SourceType, track.CoverUrl, track.LocalCoverPath),
		GenresJson: track.Genres?.Count > 0 ? JsonSerializer.Serialize(track.Genres) : null,
		AlbumId: track.AlbumInfo?.Id,
		SourceType: track.SourceType,
		SourceTrackId: track.SourceTrackId,
		LocalFilePath: track.LocalFilePath);
}
