namespace YamBassPlayer.ListenBrainz;

/// <summary>Режим LB Radio: чем «твёрже», тем дальше от популярного.</summary>
public enum RadioMode
{
	Easy,
	Medium,
	Hard
}

/// <summary>
/// Прослушивание для отправки. <see cref="StartedAt"/> — время начала трека:
/// ListenBrainz ждёт именно его, а не время окончания.
/// </summary>
public sealed record ListenSubmission(
	DateTimeOffset StartedAt,
	string ArtistName,
	string TrackName,
	string? ReleaseName = null,
	int? DurationMs = null,
	double? PlayedSeconds = null,
	string? Isrc = null,
	string? RecordingMbid = null,
	string? MusicService = null,
	string? OriginUrl = null);

/// <summary>Одна рекомендация CF: сервер отдаёт только MBID и вес.</summary>
public sealed record RecommendationItem(
	string RecordingMbid,
	double Score,
	DateTimeOffset? LastListenedAt);

/// <summary>Ответ CF-рекомендаций. <see cref="Total"/> — сколько их всего у пользователя.</summary>
public sealed record RecommendationBatch(
	IReadOnlyList<RecommendationItem> Items,
	int Total,
	DateTimeOffset? UpdatedAt);

/// <summary>Запись, разрешённая сервером по MBID: имя, артист, ISRC, длина.</summary>
public sealed record RecordingMetadata(
	string RecordingMbid,
	string ArtistName,
	string? TrackName,
	int? DurationMs,
	string? Isrc);

/// <summary>Трек из JSPF-плейлиста LB Radio.</summary>
public sealed record RadioTrack(
	string Title,
	string Creator,
	string? Album,
	int? DurationMs,
	string? RecordingMbid);

/// <summary>
/// Готовый плейлист LB Radio. <see cref="Feedback"/> — человекочитаемые строки
/// вида «Using seed artist … and similar artists: …»: их можно показать в статусе.
/// </summary>
public sealed record RadioPlaylist(
	string Title,
	IReadOnlyList<RadioTrack> Tracks,
	IReadOnlyList<string> Feedback);
