namespace YamBassPlayer.Services.Impl;

/// <summary>Сырая строка таблицы <c>Tracks</c> до обогащения исполнителями и альбомом.</summary>
internal sealed record TrackRow(
	string TrackId,
	string Artist,
	string Title,
	string Album,
	long? DurationMs,
	int? Year,
	string? CoverUrl,
	string? RemoteCoverUrl,
	string? LocalCoverPath,
	string? GenresJson,
	string? AlbumId,
	string SourceType,
	string SourceTrackId,
	string? LocalFilePath);
